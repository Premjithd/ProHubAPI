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

    private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    /// <summary>
    /// Sum of completed payments' principal for a job. Legacy completed rows (PrincipalAmount == 0)
    /// are treated as covering the full bid amount.
    /// </summary>
    private async Task<decimal> SumCompletedPrincipalAsync(int jobId, decimal bidAmount)
    {
        var completed = await _context.Payments
            .Where(p => p.JobId == jobId && p.Status == "Completed")
            .Select(p => new { p.PrincipalAmount })
            .ToListAsync();

        decimal paid = 0m;
        foreach (var p in completed)
            paid += p.PrincipalAmount > 0 ? p.PrincipalAmount : bidAmount;

        return Math.Min(paid, bidAmount);
    }

    private static PaymentRequestDto MapRequest(PaymentRequest r, decimal remaining)
    {
        var minAmount = r.RequestType switch
        {
            "Partial" => Math.Min(decimal.Round(r.RequestedAmount * r.MinPercent / 100m, 2), remaining),
            "Full"    => remaining,
            _         => 0m
        };

        return new PaymentRequestDto
        {
            Id = r.Id,
            JobId = r.JobId,
            BidId = r.BidId,
            ProId = r.ProId,
            RequestType = r.RequestType,
            RequestedAmount = r.RequestedAmount,
            MinPercent = r.MinPercent,
            MinAmount = minAmount,
            Status = r.Status,
            Note = r.Note,
            CreatedAt = r.CreatedAt,
            FulfilledAt = r.FulfilledAt
        };
    }

    private async Task<PaymentSummaryDto> BuildPaymentSummaryAsync(int jobId)
    {
        var acceptedBid = await _context.JobBids
            .Where(b => b.JobId == jobId && b.Status == "Accepted")
            .OrderByDescending(b => b.UpdatedAt)
            .FirstOrDefaultAsync();

        var bidAmount = acceptedBid?.BidAmount ?? 0m;

        var payments = await _context.Payments
            .Where(p => p.JobId == jobId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

        decimal paid = 0m;
        foreach (var p in payments.Where(p => p.Status == "Completed"))
            paid += p.PrincipalAmount > 0 ? p.PrincipalAmount : bidAmount;
        if (paid > bidAmount) paid = bidAmount;

        var remaining = Math.Max(0m, bidAmount - paid);

        var activeRequest = await _context.PaymentRequests
            .Where(r => r.JobId == jobId && r.Status == "Pending")
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        return new PaymentSummaryDto
        {
            JobId = jobId,
            BidAmount = bidAmount,
            TotalPaidPrincipal = paid,
            Remaining = remaining,
            IsFullyPaid = bidAmount > 0 && remaining <= 0,
            Payments = payments.Select(p => new PaymentHistoryItemDto
            {
                Id = p.Id,
                PrincipalAmount = p.PrincipalAmount,
                Amount = p.Amount,
                PlatformFee = p.PlatformFee,
                ProPayout = p.ProPayout,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                CompletedAt = p.CompletedAt
            }).ToList(),
            ActiveRequest = activeRequest == null ? null : MapRequest(activeRequest, remaining)
        };
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
    /// GET: api/payments/job/{jobId}/summary - Payment tracking for a job (consumer or assigned pro).
    /// </summary>
    [HttpGet("job/{jobId}/summary")]
    public async Task<ActionResult<PaymentSummaryDto>> GetPaymentSummary(int jobId)
    {
        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null)
            return NotFound(new { message = "Job not found" });

        var userId = GetUserId();
        if (job.UserId != userId && job.AssignedProId != userId)
            return Forbid();

        var summary = await BuildPaymentSummaryAsync(jobId);
        return Ok(summary);
    }

    /// <summary>
    /// POST: api/payments/request - Assigned Pro raises a payment request (None/Partial/Full).
    /// Only one Pending request may exist per job at a time.
    /// </summary>
    [HttpPost("request")]
    public async Task<IActionResult> CreatePaymentRequest([FromBody] CreatePaymentRequestRequest request)
    {
        try
        {
            var userId = GetUserId();

            var job = await _context.Jobs
                .Include(j => j.User)
                .FirstOrDefaultAsync(j => j.Id == request.JobId);

            if (job == null)
                return NotFound(new { message = "Job not found" });

            if (job.AssignedProId != userId)
                return Forbid();

            var acceptedBid = await _context.JobBids
                .Where(b => b.JobId == request.JobId && b.Status == "Accepted")
                .OrderByDescending(b => b.UpdatedAt)
                .FirstOrDefaultAsync();

            if (acceptedBid == null || (acceptedBid.BidAmount ?? 0) <= 0)
                return BadRequest(new { message = "No accepted bid with an amount for this job" });

            var bidAmount = acceptedBid.BidAmount!.Value;

            var existing = await _context.PaymentRequests
                .FirstOrDefaultAsync(r => r.JobId == request.JobId && r.Status == "Pending");
            if (existing != null)
                return BadRequest(new { message = "There is already an active payment request for this job" });

            var alreadyPaid = await SumCompletedPrincipalAsync(request.JobId, bidAmount);
            var remaining = Math.Max(0m, bidAmount - alreadyPaid);

            var type = request.RequestType;
            decimal requestedAmount;
            decimal minPercent = 0m;

            switch (type)
            {
                case "None":
                    requestedAmount = 0m;
                    break;
                case "Full":
                    if (remaining <= 0)
                        return BadRequest(new { message = "This job is already fully paid" });
                    requestedAmount = remaining;
                    break;
                case "Partial":
                    if (remaining <= 0)
                        return BadRequest(new { message = "This job is already fully paid" });
                    if (request.RequestedAmount <= 0)
                        return BadRequest(new { message = "Requested amount must be greater than zero" });
                    if (request.RequestedAmount > remaining + 0.01m)
                        return BadRequest(new { message = $"Requested amount exceeds the remaining balance of ₹{remaining:N2}" });
                    requestedAmount = request.RequestedAmount;
                    minPercent = Math.Clamp(request.MinPercent, 0m, 100m);
                    break;
                default:
                    return BadRequest(new { message = "Invalid request type" });
            }

            var paymentRequest = new PaymentRequest
            {
                JobId = request.JobId,
                BidId = acceptedBid.Id,
                ProId = userId,
                RequestType = type,
                RequestedAmount = requestedAmount,
                MinPercent = minPercent,
                Note = request.Note,
                Status = type == "None" ? "Fulfilled" : "Pending",
                CreatedAt = DateTime.UtcNow,
                FulfilledAt = type == "None" ? DateTime.UtcNow : null
            };
            _context.PaymentRequests.Add(paymentRequest);

            // "No payment" lets the job proceed so the Pro can confirm and start work.
            if (type == "None" && job.Status == "Bid Accepted")
            {
                job.Status = "Payment Made";
                job.UpdatedAt = DateTime.UtcNow;
            }

            _context.JobNotifications.Add(new JobNotification
            {
                JobId = job.Id,
                UserId = job.UserId,
                NotificationType = "PaymentRequested",
                Message = type == "None"
                    ? $"The professional waived upfront payment for \"{job.Title}\" — work can begin."
                    : $"The professional requested a payment of ₹{requestedAmount:N0} for \"{job.Title}\"."
            });

            await _context.SaveChangesAsync();

            if (job.User != null)
                await _notificationService.NotifyUserPaymentRequestedAsync(job, job.User, type, requestedAmount);

            var summary = await BuildPaymentSummaryAsync(request.JobId);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating payment request: {ex.Message}");
            return StatusCode(500, new { message = "Error creating payment request", error = ex.Message });
        }
    }

    /// <summary>
    /// POST: api/payments/request/{id}/cancel - Pro cancels their Pending payment request.
    /// </summary>
    [HttpPost("request/{id}/cancel")]
    public async Task<IActionResult> CancelPaymentRequest(int id)
    {
        var pr = await _context.PaymentRequests.Include(r => r.Job).FirstOrDefaultAsync(r => r.Id == id);
        if (pr == null)
            return NotFound(new { message = "Payment request not found" });

        if (pr.Job?.AssignedProId != GetUserId())
            return Forbid();

        if (pr.Status != "Pending")
            return BadRequest(new { message = "Only a pending request can be cancelled" });

        pr.Status = "Cancelled";
        await _context.SaveChangesAsync();

        var summary = await BuildPaymentSummaryAsync(pr.JobId);
        return Ok(summary);
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

            // Quote expiry only matters before the bid is accepted. Once accepted, payment can happen
            // later (and across multiple partial installments), so the original expiry no longer applies.
            if (bid.Status != "Accepted" &&
                (bid.Status == "Expired" || (bid.ExpiresAt.HasValue && DateTime.UtcNow > bid.ExpiresAt)))
            {
                bid.Status = "Expired";
                await _context.SaveChangesAsync();
                return BadRequest(new { message = "Quote has expired" });
            }

            // Authoritative agreed total is the accepted bid amount.
            var bidAmount = bid.BidAmount ?? request.Amount;
            if (bidAmount <= 0)
                return BadRequest(new { message = "Bid amount is not set for this job" });

            // How much of the agreed bid is being paid right now (principal). Defaults to the full remaining.
            var alreadyPaid = await SumCompletedPrincipalAsync(request.JobId, bidAmount);
            var remaining = Math.Max(0m, bidAmount - alreadyPaid);

            if (remaining <= 0)
                return BadRequest(new { message = "This job is already fully paid" });

            var principal = request.PrincipalAmount > 0 ? request.PrincipalAmount : remaining;
            if (principal <= 0)
                return BadRequest(new { message = "Payment amount must be greater than zero" });
            if (principal > remaining + 0.01m)
                return BadRequest(new { message = $"Payment exceeds the remaining balance of ₹{remaining:N2}" });

            // Enforce the floor from an active Partial request, if any.
            var activeRequest = await _context.PaymentRequests
                .Where(r => r.JobId == request.JobId && r.Status == "Pending")
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            if (activeRequest != null && activeRequest.RequestType == "Partial")
            {
                var floor = Math.Min(decimal.Round(activeRequest.RequestedAmount * activeRequest.MinPercent / 100m, 2), remaining);
                if (principal + 0.01m < floor)
                    return BadRequest(new { message = $"Minimum payment for this request is ₹{floor:N2}" });
            }

            // Remove any stale Pending order for this job/user before creating a fresh one.
            var stalePending = await _context.Payments
                .Where(p => p.JobId == request.JobId && p.UserId == userId && p.Status == "Pending")
                .ToListAsync();
            if (stalePending.Count > 0)
            {
                _context.Payments.RemoveRange(stalePending);
                await _context.SaveChangesAsync();
            }

            // Calculate the full-bid split, then prorate to the principal portion being paid now.
            var fullSplit = await _rateSplitService.CalculateSplitAsync(bidAmount);
            var rateSplit = _rateSplitService.ProrateForPrincipal(fullSplit, principal);
            var totalAmountToPay = rateSplit.TotalAmountUserPays;

            // Create Razorpay order with the prorated total
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
                PrincipalAmount = principal,
                Amount = totalAmountToPay,
                PlatformFee = rateSplit.UserCommission,
                ProPayout = rateSplit.ProPayout,
                RazorpayOrderId = orderResponse.OrderId,
                ProviderId = "razorpay",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Payment order created: {orderResponse.OrderId} for Job:{request.JobId}, Principal:₹{principal:N2}");

            // Return Razorpay checkout details (field names align with the Angular PaymentOrder model)
            return Ok(new
            {
                orderId = orderResponse.OrderId,
                amount = orderResponse.Amount,
                currency = orderResponse.Currency,
                key = orderResponse.Key,
                consumerName = $"{job.User?.FirstName} {job.User?.LastName}",
                consumerEmail = job.User?.Email,
                consumerPhone = job.User?.PhoneNumber,
                principalAmount = principal,
                bidAmount,
                remainingBefore = remaining,
                platformFee = rateSplit.UserCommission,
                gstOnPlatformFee = rateSplit.GstOnUserCommission,
                proDeduction = rateSplit.ProDeduction,
                totalAmount = totalAmountToPay,
                proPayout = rateSplit.ProPayout,
                effectivePlatformFeePercent = rateSplit.EffectiveUserChargePercent,
                effectiveProPayoutPercent = rateSplit.EffectiveProPayoutPercent
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

            // Mark the active payment request (if any) as fulfilled.
            var fulfilledRequest = await _context.PaymentRequests
                .Where(r => r.JobId == payment.JobId && r.Status == "Pending")
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();
            if (fulfilledRequest != null)
            {
                fulfilledRequest.Status = "Fulfilled";
                fulfilledRequest.FulfilledAt = DateTime.UtcNow;
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

            // Notify user that their payment was confirmed
            if (job != null)
            {
                _context.JobNotifications.Add(new JobNotification
                {
                    JobId = job.Id,
                    UserId = payment.UserId,
                    NotificationType = "PaymentConfirmed",
                    Message = $"Your payment of ₹{payment.Amount:N0} for \"{job.Title}\" has been confirmed."
                });
                await _context.SaveChangesAsync();
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
    /// GET: api/payments/admin - All payments with full refund details (Admin only).
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllPaymentsAdmin(
        [FromQuery] string? status = null,
        [FromQuery] int? userId = null,
        [FromQuery] int? proId = null)
    {
        var query = _context.Payments
            .Include(p => p.Job)
            .Include(p => p.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);

        if (userId.HasValue)
            query = query.Where(p => p.UserId == userId.Value);

        if (proId.HasValue)
            query = query.Where(p => p.Job != null && p.Job.AssignedProId == proId.Value);

        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                id              = p.Id,
                jobId           = p.JobId,
                jobTitle        = p.Job != null ? p.Job.Title : null,
                userId          = p.UserId,
                consumerName    = p.User != null ? p.User.FirstName + " " + p.User.LastName : null,
                consumerEmail   = p.User != null ? p.User.Email : null,
                amount          = p.Amount,
                platformFee     = p.PlatformFee,
                proPayOut       = p.ProPayout,
                status          = p.Status,
                razorpayOrderId = p.RazorpayOrderId,
                razorpayPaymentId = p.RazorpayPaymentId,
                createdAt       = p.CreatedAt,
                completedAt     = p.CompletedAt,
                refundedAt      = p.RefundedAt,
                refundAmount    = p.RefundAmount,
                refundReason    = p.RefundReason,
                failureReason   = p.FailureReason
            })
            .ToListAsync();

        return Ok(payments);
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
            payment.RefundAmount = payment.Amount;
            payment.RefundReason = reason;

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

                    var fulfilledRequest = await _context.PaymentRequests
                        .Where(r => r.JobId == payment.JobId && r.Status == "Pending")
                        .OrderByDescending(r => r.CreatedAt)
                        .FirstOrDefaultAsync();
                    if (fulfilledRequest != null)
                    {
                        fulfilledRequest.Status = "Fulfilled";
                        fulfilledRequest.FulfilledAt = DateTime.UtcNow;
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

            case "refund.processed":
            {
                var entity = root.GetProperty("payload").GetProperty("refund").GetProperty("entity");
                var razorpayPaymentId = entity.GetProperty("payment_id").GetString();
                var refundId = entity.GetProperty("id").GetString();

                var payment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.RazorpayPaymentId == razorpayPaymentId);

                if (payment != null && payment.Status == "Refunded")
                {
                    _logger.LogInformation(
                        "Razorpay confirmed refund processed: RefundId:{RefundId} for Payment:{PaymentId}",
                        refundId, payment.Id);
                }
                break;
            }

            case "refund.failed":
            {
                var entity = root.GetProperty("payload").GetProperty("refund").GetProperty("entity");
                var razorpayPaymentId = entity.GetProperty("payment_id").GetString();
                var errorDesc = entity.TryGetProperty("description", out var ed) ? ed.GetString() : "Refund failed at payment gateway";

                var payment = await _context.Payments
                    .Include(p => p.Job)
                    .FirstOrDefaultAsync(p => p.RazorpayPaymentId == razorpayPaymentId);

                if (payment != null && payment.Status == "Refunded")
                {
                    payment.Status = "Completed";
                    payment.RefundedAt = null;
                    payment.RefundAmount = null;
                    payment.RefundReason = null;
                    payment.FailureReason = $"Refund failed: {errorDesc}";

                    if (payment.Job != null && payment.Job.Status == "Open")
                    {
                        payment.Job.Status = "Payment Made";
                        payment.Job.UpdatedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogError(
                        "Razorpay refund failed for Payment:{PaymentId}: {Reason} — payment reverted to Completed",
                        payment.Id, errorDesc);
                }
                break;
            }

            case "payout.processed":
            {
                var entity = root.GetProperty("payload").GetProperty("payout").GetProperty("entity");
                var razorpayPayoutId = entity.GetProperty("id").GetString();

                var payout = await _context.Payouts
                    .Include(p => p.Pro)
                    .FirstOrDefaultAsync(p => p.RazorpayPayoutId == razorpayPayoutId);

                if (payout != null && payout.Status == "Processing")
                {
                    payout.Status = "Processed";
                    payout.ProcessedAt = DateTime.UtcNow;
                    payout.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Payout:{PayoutId} processed — RazorpayId:{RazorpayPayoutId}", payout.Id, razorpayPayoutId);

                    if (payout.Pro != null)
                        await _notificationService.NotifyAsync(
                            payout.Pro.Email,
                            payout.Pro.PhoneNumber,
                            $"Payout of ₹{payout.Amount:F2} delivered",
                            $"Hi {payout.Pro.ProName}, your payout of ₹{payout.Amount:F2} for job #{payout.JobId} " +
                            "has been successfully delivered to your bank account / UPI.");
                }
                break;
            }

            case "payout.failed":
            {
                var entity = root.GetProperty("payload").GetProperty("payout").GetProperty("entity");
                var razorpayPayoutId = entity.GetProperty("id").GetString();
                var errorDesc = entity.TryGetProperty("failure_reason", out var fr)
                    ? fr.GetString()
                    : "Payout failed at payment gateway";

                var payout = await _context.Payouts
                    .Include(p => p.Pro)
                    .FirstOrDefaultAsync(p => p.RazorpayPayoutId == razorpayPayoutId);

                if (payout != null && payout.Status == "Processing")
                {
                    payout.Status = "Failed";
                    payout.FailureReason = errorDesc;
                    payout.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogError("Payout:{PayoutId} failed — {Reason}", payout.Id, errorDesc);

                    if (payout.Pro != null)
                        await _notificationService.NotifyAsync(
                            payout.Pro.Email,
                            payout.Pro.PhoneNumber,
                            $"Payout failed — Job #{payout.JobId}",
                            $"Hi {payout.Pro.ProName}, your payout of ₹{payout.Amount:F2} could not be delivered. " +
                            "Please verify your bank details or contact support.");
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
