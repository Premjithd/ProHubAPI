using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.DTOs;
using ServiceProviderAPI.Models;
using ServiceProviderAPI.Services;
using ServiceProviderAPI.Services.Abstractions;
using System.Security.Claims;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IRateSplitService _rateSplitService;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;

    public PaymentsController(
        ApplicationDbContext context,
        ILogger<PaymentsController> logger,
        IPaymentProvider paymentProvider,
        IRateSplitService rateSplitService,
        INotificationService notificationService,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _paymentProvider = paymentProvider;
        _rateSplitService = rateSplitService;
        _notificationService = notificationService;
        _configuration = configuration;
    }

    /// <summary>
    /// GET: api/payments/job/{jobId} - Get payment status for a job
    /// </summary>
    [HttpGet("job/{jobId}")]
    public async Task<ActionResult<PaymentDto>> GetPaymentByJob(int jobId)
    {
        try
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.JobId == jobId);

            if (payment == null)
                return NotFound(new { message = "Payment not found for this job" });

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = int.Parse(userIdClaim ?? "0");

            // Verify ownership (consumer or pro assigned to job)
            var job = await _context.Jobs.FindAsync(jobId);
            if (job?.UserId != userId && job?.AssignedProId != userId)
                return Forbid();

            var dto = new PaymentDto
            {
                Id = payment.Id,
                JobId = payment.JobId,
                BidId = payment.BidId,
                UserId = payment.UserId,
                Amount = payment.Amount,
                PlatformFee = payment.PlatformFee,
                ProPayout = payment.ProPayout,
                Status = payment.Status,
                RazorpayOrderId = payment.RazorpayOrderId,
                CreatedAt = payment.CreatedAt,
                CompletedAt = payment.CompletedAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching payment: {ex.Message}");
            return StatusCode(500, new { message = "Error fetching payment", error = ex.Message });
        }
    }

    /// <summary>
    /// POST: api/payments/create-order - Create a Razorpay order for a job bid
    /// </summary>
    [HttpPost("create-order")]
    public async Task<IActionResult> CreatePaymentOrder([FromBody] CreatePaymentRequest request)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = int.Parse(userIdClaim ?? "0");

            // Verify job exists and consumer owns it
            var job = await _context.Jobs
                .Include(j => j.User)
                .FirstOrDefaultAsync(j => j.Id == request.JobId);

            if (job == null)
                return NotFound(new { message = "Job not found" });

            if (job.UserId != userId)
                return Forbid();

            // Verify bid exists and is not expired
            var bid = await _context.JobBids
                .Include(b => b.Pro)
                .FirstOrDefaultAsync(b => b.Id == request.BidId && b.JobId == request.JobId);

            if (bid == null)
                return BadRequest(new { message = "Bid not found for this job" });

            if (bid.Status == "Expired" || (bid.ExpiresAt.HasValue && DateTime.UtcNow > bid.ExpiresAt))
            {
                bid.Status = "Expired";
                await _context.SaveChangesAsync();
                return BadRequest(new { message = "Quote has expired" });
            }

            // Check if payment already exists for this job by the current user
            var existingPayment = await _context.Payments
                .FirstOrDefaultAsync(p => p.JobId == request.JobId && p.UserId == userId);

            if (existingPayment != null)
            {
                _logger.LogInformation($"Payment already exists for Job:{request.JobId}, User:{userId}. Status: {existingPayment.Status}");

                // If payment is already completed, don't allow another payment
                if (existingPayment.Status == "Completed")
                    return BadRequest(new { message = "Payment already completed for this job" });

                // If payment is pending, delete it and create a new order (old order might be stale)
                if (existingPayment.Status == "Pending")
                {
                    _logger.LogInformation($"Deleting stale pending payment {existingPayment.Id} to create fresh order");
                    _context.Payments.Remove(existingPayment);
                    await _context.SaveChangesAsync();
                }
            }

            // Calculate rate split
            var rateSplit = _rateSplitService.CalculateSplit(request.Amount);

            // Total amount user pays (bid + fees + tax)
            var totalAmountToPay = request.Amount + rateSplit.PlatformFee + rateSplit.GstOnPlatformFee;

            // Create Razorpay order with total amount
            var orderResponse = await _paymentProvider.CreateOrderAsync(
                request.JobId,
                request.BidId,
                totalAmountToPay,
                $"{job.User?.FirstName} {job.User?.LastName}",
                job.User?.Email ?? "",
                job.User?.PhoneNumber ?? "");

            if (orderResponse?.OrderId == null)
                return BadRequest(new { message = "Failed to create payment order" });

            // Create Payment record
            var payment = new Payment
            {
                JobId = request.JobId,
                BidId = request.BidId,
                UserId = userId,
                Amount = totalAmountToPay,  // User pays the total amount including fees
                PlatformFee = rateSplit.PlatformFee,
                ProPayout = rateSplit.ProPayout,
                RazorpayOrderId = orderResponse.OrderId,
                ProviderId = "razorpay",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Payment order created: {orderResponse.OrderId} for Job:{request.JobId}");

            // Return Razorpay checkout details
            return Ok(new
            {
                orderId = orderResponse.OrderId,
                amount = orderResponse.Amount,
                currency = orderResponse.Currency,
                key = orderResponse.Key,
                consumerName = $"{job.User?.FirstName} {job.User?.LastName}",
                consumerEmail = job.User?.Email,
                consumerPhone = job.User?.PhoneNumber,
                platformFee = rateSplit.PlatformFee,
                gstOnPlatformFee = rateSplit.GstOnPlatformFee,
                totalAmount = totalAmountToPay,
                proPayout = rateSplit.ProPayout,
                effectivePlatformFeePercent = rateSplit.EffectivePlatformFeePercent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating payment order: {ex.Message}");
            return StatusCode(500, new { message = "Error creating payment order", error = ex.Message });
        }
    }

    /// <summary>
    /// POST: api/payments/verify - Verify Razorpay payment and activate job
    /// </summary>
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = int.Parse(userIdClaim ?? "0");

            // Find payment by Razorpay Order ID
            var payment = await _context.Payments
                .Include(p => p.Job)
                .ThenInclude(j => j!.Category)
                .Include(p => p.Bid)
                .ThenInclude(b => b!.Pro)
                .FirstOrDefaultAsync(p => p.RazorpayOrderId == request.RazorpayOrderId);

            if (payment == null)
                return NotFound(new { message = "Payment not found" });

            // Verify user is consumer who initiated payment
            if (payment.UserId != userId)
                return Forbid();

            // Validate request parameters are not null before verification
            if (string.IsNullOrEmpty(request.RazorpayOrderId) || 
                string.IsNullOrEmpty(request.RazorpayPaymentId) || 
                string.IsNullOrEmpty(request.RazorpaySignature))
            {
                return BadRequest(new { message = "Missing required payment verification parameters" });
            }

            // Verify payment with provider
            var isVerified = await _paymentProvider.VerifyPaymentAsync(
                request.RazorpayOrderId,
                request.RazorpayPaymentId,
                request.RazorpaySignature);

            if (!isVerified)
            {
                payment.Status = "Failed";
                payment.FailureReason = "Payment verification failed";
                _context.Payments.Update(payment);
                await _context.SaveChangesAsync();

                _logger.LogWarning($"Payment verification failed: {request.RazorpayOrderId}");
                return BadRequest(new { message = "Payment verification failed" });
            }

            // Update payment status
            payment.RazorpayPaymentId = request.RazorpayPaymentId;
            payment.Status = "Completed";
            payment.CompletedAt = DateTime.UtcNow;

            var job = payment.Job;
            if (job != null && job.Status == "Bid Accepted")
            {
                job.Status = "Payment Made";
                job.UpdatedAt = DateTime.UtcNow;
            }

            // Update bid status to Accepted
            if (payment.Bid != null)
            {
                payment.Bid.Status = "Accepted";
                payment.Bid.UpdatedAt = DateTime.UtcNow;
                _context.JobBids.Update(payment.Bid);
            }

            _context.Payments.Update(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Payment verified successfully: {request.RazorpayOrderId}");

            // Notify pro that payment received and needs confirmation
            if (job != null && payment.Bid?.Pro != null && job.User != null)
            {
                await _notificationService.NotifyProPaymentReceivedAsync(
                    job, payment.Bid, payment.Bid.Pro, payment.Amount);
            }

            return Ok(new
            {
                message = "Payment verified successfully",
                paymentId = payment.Id,
                status = payment.Status,
                jobStatus = job?.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error verifying payment: {ex.Message}");
            return StatusCode(500, new { message = "Error verifying payment", error = ex.Message });
        }
    }

    /// <summary>
    /// POST: api/payments/{paymentId}/refund - Process refund for a payment (Admin only).
    /// Consumer-initiated refunds must go through the dispute flow: POST /api/jobs/{jobId}/completion/dispute.
    /// Admins then resolve via POST /api/admin/jobs/{jobId}/completion/resolve with resolution="refund".
    /// </summary>
    [HttpPost("{paymentId}/refund")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RefundPayment(int paymentId, [FromBody] Dictionary<string, string> reasonDict)
    {
        try
        {
            var payment = await _context.Payments
                .Include(p => p.Job)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
                return NotFound(new { message = "Payment not found" });

            if (payment.Status == "Refunded")
                return BadRequest(new { message = "Payment already refunded" });

            if (string.IsNullOrEmpty(payment.RazorpayPaymentId))
                return BadRequest(new { message = "Payment has no Razorpay payment ID — cannot process refund" });

            var reason = reasonDict.ContainsKey("reason") ? reasonDict["reason"] : "Admin-initiated refund";

            var refundId = await _paymentProvider.ProcessRefundAsync(
                payment.RazorpayOrderId ?? "",
                payment.RazorpayPaymentId,
                payment.Amount,
                reason);

            if (refundId == null)
                return BadRequest(new { message = "Failed to process refund" });

            payment.Status = "Refunded";
            payment.RefundedAt = DateTime.UtcNow;

            if (payment.Job != null)
            {
                payment.Job.Status = "Open";
                payment.Job.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin refund: {RefundId} for Payment:{PaymentId}", refundId, paymentId);

            return Ok(new
            {
                message = "Refund processed successfully",
                refundId,
                status = payment.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund for Payment:{PaymentId}", paymentId);
            return StatusCode(500, new { message = "Error processing refund", error = ex.Message });
        }
    }

    /// <summary>
    /// POST: api/payments/webhook — Razorpay server-to-server webhook.
    /// Handles payment.captured and payment.failed events as a reliable fallback to the
    /// client-side /verify flow (e.g. browser crash after payment, network drop).
    /// Must be AllowAnonymous — Razorpay calls this without a JWT.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> RazorpayWebhook()
    {
        // Read raw body for signature verification
        Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
            rawBody = await reader.ReadToEndAsync();

        var signature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault() ?? "";
        var webhookSecret = _configuration["Payment:Razorpay:WebhookSecret"] ?? "";

        // Verify HMAC-SHA256 signature when a secret is configured
        if (!string.IsNullOrEmpty(webhookSecret))
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
            var computed = BitConverter.ToString(
                    hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)))
                .Replace("-", "").ToLower();

            if (!computed.Equals(signature, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Razorpay webhook: signature mismatch — request rejected");
                return Unauthorized(new { message = "Invalid webhook signature" });
            }
        }
        else
        {
            _logger.LogWarning("Razorpay webhook: WebhookSecret not configured — skipping signature check");
        }

        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;
        var eventType = root.TryGetProperty("event", out var ev) ? ev.GetString() : null;
        _logger.LogInformation($"Razorpay webhook received: {eventType}");

        switch (eventType)
        {
            case "payment.captured":
            {
                var entity = root.GetProperty("payload").GetProperty("payment").GetProperty("entity");
                var orderId = entity.GetProperty("order_id").GetString();
                var paymentId = entity.GetProperty("id").GetString();

                var payment = await _context.Payments
                    .Include(p => p.Job)
                    .Include(p => p.Bid).ThenInclude(b => b!.Pro)
                    .FirstOrDefaultAsync(p => p.RazorpayOrderId == orderId);

                if (payment != null && payment.Status != "Completed")
                {
                    payment.RazorpayPaymentId = paymentId;
                    payment.Status = "Completed";
                    payment.CompletedAt = DateTime.UtcNow;

                    if (payment.Job?.Status == "Bid Accepted")
                    {
                        payment.Job.Status = "Payment Made";
                        payment.Job.UpdatedAt = DateTime.UtcNow;
                    }
                    if (payment.Bid != null)
                    {
                        payment.Bid.Status = "Accepted";
                        payment.Bid.UpdatedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Webhook: payment captured for order {orderId}");

                    if (payment.Job != null && payment.Bid?.Pro != null)
                    {
                        await _notificationService.NotifyProPaymentReceivedAsync(
                            payment.Job, payment.Bid, payment.Bid.Pro, payment.Amount);
                    }
                }
                break;
            }

            case "payment.failed":
            {
                var entity = root.GetProperty("payload").GetProperty("payment").GetProperty("entity");
                var orderId = entity.GetProperty("order_id").GetString();
                var errorDesc = entity.TryGetProperty("error_description", out var ed)
                    ? ed.GetString() : "Payment failed";

                var payment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.RazorpayOrderId == orderId);

                if (payment != null && payment.Status == "Pending")
                {
                    payment.Status = "Failed";
                    payment.FailureReason = errorDesc;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Webhook: payment failed for order {orderId}");
                }
                break;
            }

            default:
                _logger.LogInformation($"Razorpay webhook: unhandled event type '{eventType}' — ignored");
                break;
        }

        return Ok(new { status = "ok" });
    }
}
