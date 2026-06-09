using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;
using ServiceProviderAPI.Services;
using ServiceProviderAPI.Services.Abstractions;
using System.Text.Json.Serialization;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEmailService _emailService;
    private readonly ILogger<AdminController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPaymentProvider _paymentProvider;
    private readonly INotificationService _notificationService;
    private readonly IPayoutService _payoutService;
    private readonly IRateSplitService _rateSplitService;

    public AdminController(
        ApplicationDbContext context,
        IJwtService jwtService,
        IHttpContextAccessor httpContextAccessor,
        IEmailService emailService,
        ILogger<AdminController> logger,
        IHttpClientFactory httpClientFactory,
        IPaymentProvider paymentProvider,
        INotificationService notificationService,
        IPayoutService payoutService,
        IRateSplitService rateSplitService)
    {
        _context = context;
        _jwtService = jwtService;
        _httpContextAccessor = httpContextAccessor;
        _emailService = emailService;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _paymentProvider = paymentProvider;
        _notificationService = notificationService;
        _payoutService = payoutService;
        _rateSplitService = rateSplitService;
    }

    // Search for users by email or name
    [HttpGet("users/search")]
    public async Task<ActionResult<IEnumerable<object>>> SearchUsers([FromQuery] string query)
    {
        if (string.IsNullOrEmpty(query))
            return BadRequest(new { message = "Query parameter is required" });

        var users = await _context.Users
            .Where(u => (u.Email != null && u.Email.Contains(query)) || 
                       (u.FirstName != null && u.FirstName.Contains(query)) || 
                       (u.LastName != null && u.LastName.Contains(query)))
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.PhoneNumber,
                u.IsEmailVerified,
                u.IsPhoneVerified,
                u.CreatedAt,
                u.UpdatedAt
            })
            .Take(50)
            .ToListAsync();

        return Ok(users);
    }

    // Search for professionals by email, name, or business name
    [HttpGet("pros/search")]
    public async Task<ActionResult<IEnumerable<object>>> SearchPros([FromQuery] string query)
    {
        if (string.IsNullOrEmpty(query))
            return BadRequest(new { message = "Query parameter is required" });

        var pros = await _context.Pros
            .Where(p => (p.Email != null && p.Email.Contains(query)) || 
                       (p.ProName != null && p.ProName.Contains(query)) || 
                       (p.BusinessName != null && p.BusinessName.Contains(query)))
            .Select(p => new
            {
                p.Id,
                p.ProName,
                p.Email,
                p.PhoneNumber,
                p.BusinessName,
                p.IsEmailVerified,
                p.IsPhoneVerified,
                p.CreatedAt,
                p.UpdatedAt
            })
            .Take(50)
            .ToListAsync();

        return Ok(pros);
    }

    // Impersonate a user or pro
    [HttpPost("impersonate")]
    public async Task<ActionResult> ImpersonateUser([FromBody] ImpersonateRequest request)
    {
        if (request.TargetUserId <= 0 || string.IsNullOrEmpty(request.TargetUserType))
            return BadRequest(new { message = "Invalid target user information" });

        if (request.TargetUserType != "User" && request.TargetUserType != "Pro")
            return BadRequest(new { message = "TargetUserType must be 'User' or 'Pro'" });

        try
        {
            string impersonationToken = string.Empty;

            if (request.TargetUserType == "User")
            {
                var user = await _context.Users.FindAsync(request.TargetUserId);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                (impersonationToken, _) = _jwtService.GenerateToken(user, "User", expiryMinutes: 60);
            }
            else
            {
                var pro = await _context.Pros.FindAsync(request.TargetUserId);
                if (pro == null)
                    return NotFound(new { message = "Professional not found" });

                (impersonationToken, _) = _jwtService.GenerateToken(pro, "Pro", expiryMinutes: 60);
            }

            return Ok(new
            {
                token = impersonationToken,
                userId = request.TargetUserId,
                userType = request.TargetUserType,
                impersonatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Error generating impersonation token", error = ex.Message });
        }
    }

    // Invite a new admin
    [HttpPost("invite")]
    public async Task<ActionResult> InviteAdmin([FromBody] InviteAdminRequest request)
    {
        Console.WriteLine("=== InviteAdmin called ===");
        Console.WriteLine($"Request received for email: {request?.Email}");
        
        if (request == null || string.IsNullOrEmpty(request.Email))
            return BadRequest(new { message = "Email is required" });

        // Validate email format
        try
        {
            var addr = new System.Net.Mail.MailAddress(request.Email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Email validation failed: {ex.Message}");
            return BadRequest(new { message = "Invalid email format", error = ex.Message });
        }

        try
        {
            // Check if email is already used by an admin
            var existingAdmin = await _context.AdminUsers
                .FirstOrDefaultAsync(a => a.Email == request.Email);

            if (existingAdmin != null)
                return BadRequest(new { message = "This email is already associated with an admin account" });

            // Check for existing pending invitation
            var existingInvitation = await _context.AdminInvitations
                .FirstOrDefaultAsync(ai => ai.Email == request.Email && !ai.IsUsed && ai.ExpiresAt > DateTime.UtcNow);

            if (existingInvitation != null)
                return BadRequest(new { message = "An active invitation already exists for this email" });

            // Create invitation
            var invitationToken = Guid.NewGuid().ToString();
            var expiresAt = DateTime.UtcNow.AddDays(7);

            var invitation = new AdminInvitation
            {
                Email = request.Email,
                Token = invitationToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsUsed = false
            };

            _context.AdminInvitations.Add(invitation);
            await _context.SaveChangesAsync();

            // Send invitation email
            var request_scheme = _httpContextAccessor.HttpContext?.Request.Scheme ?? "https";
            var request_host = _httpContextAccessor.HttpContext?.Request.Host.ToString() ?? "localhost:3000";
            var invitationLink = $"{request_scheme}://{request_host.Replace("7042", "4200")}/accept-admin-invite?token={invitationToken}";

            try
            {
                await _emailService.SendAdminInvitationAsync(request.Email, invitationLink);
                _logger.LogInformation($"✓ Admin invitation email sent to {request.Email}");
            }
            catch (Exception emailEx)
            {
                _logger.LogWarning($"⚠ Invitation created but email sending failed: {emailEx.Message}");
                // Don't fail the invitation creation if email fails - admin can resend later
            }

            return Ok(new
            {
                message = "Invitation sent successfully",
                invitationId = invitation.Id,
                email = request.Email,
                expiresAt = expiresAt,
                invitationToken = invitationToken
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"✗ Error in InviteAdmin: {ex.Message}\n{ex.StackTrace}");
            return BadRequest(new { message = "Error processing invitation", error = ex.Message });
        }
    }

    // Get pending admin invitations
    [HttpGet("invitations")]
    public async Task<ActionResult<IEnumerable<object>>> GetInvitations([FromQuery] bool pendingOnly = true)
    {
        try
        {
            var query = _context.AdminInvitations.AsQueryable();

            if (pendingOnly)
            {
                query = query.Where(ai => !ai.IsUsed && ai.ExpiresAt > DateTime.UtcNow);
            }

            var invitations = await query
                .OrderByDescending(ai => ai.CreatedAt)
                .Select(ai => new
                {
                    ai.Id,
                    ai.Email,
                    ai.Token,
                    ai.CreatedAt,
                    ai.ExpiresAt,
                    ai.IsUsed,
                    ai.UsedAt,
                    IsExpired = ai.ExpiresAt <= DateTime.UtcNow,
                    DaysUntilExpiry = Math.Ceiling((ai.ExpiresAt - DateTime.UtcNow).TotalDays)
                })
                .ToListAsync();

            return Ok(invitations);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching invitations: {ex.Message}");
            return BadRequest(new { message = "Error fetching invitations", error = ex.Message });
        }
    }

    // Resend invitation email
    [HttpPost("invitations/{id}/resend")]
    public async Task<ActionResult> ResendInvitation(int id)
    {
        try
        {
            var invitation = await _context.AdminInvitations.FindAsync(id);

            if (invitation == null)
                return NotFound(new { message = "Invitation not found" });

            if (invitation.IsUsed)
                return BadRequest(new { message = "This invitation has already been used" });

            if (invitation.ExpiresAt <= DateTime.UtcNow)
                return BadRequest(new { message = "This invitation has expired" });

            var request_scheme = _httpContextAccessor.HttpContext?.Request.Scheme ?? "https";
            var request_host = _httpContextAccessor.HttpContext?.Request.Host.ToString() ?? "localhost:3000";
            var invitationLink = $"{request_scheme}://{request_host.Replace("7042", "4200")}/accept-admin-invite?token={invitation.Token}";

            try
            {
                await _emailService.SendAdminInvitationAsync(invitation.Email, invitationLink);
                _logger.LogInformation($"✓ Admin invitation resent to {invitation.Email}");
            }
            catch (Exception emailEx)
            {
                _logger.LogWarning($"Error resending email: {emailEx.Message}");
                return BadRequest(new { message = "Failed to send email", error = emailEx.Message });
            }

            return Ok(new
            {
                message = "Invitation resent successfully",
                email = invitation.Email,
                expiresAt = invitation.ExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error resending invitation: {ex.Message}");
            return BadRequest(new { message = "Error resending invitation", error = ex.Message });
        }
    }

    // GET: api/admin/commission-config
    [HttpGet("commission-config")]
    public async Task<ActionResult> GetCommissionConfig()
    {
        var config = await _rateSplitService.GetConfigAsync();
        return Ok(config);
    }

    // PUT: api/admin/commission-config
    [HttpPut("commission-config")]
    public async Task<ActionResult> UpdateCommissionConfig([FromBody] UpdateCommissionConfigRequest request)
    {
        if (request.UserCommissionPercent < 0 || request.UserCommissionPercent > 50)
            return BadRequest(new { message = "User commission must be between 0% and 50%." });
        if (request.ProCommissionPercent < 0 || request.ProCommissionPercent > 50)
            return BadRequest(new { message = "Pro commission must be between 0% and 50%." });
        if (request.GstPercent < 0 || request.GstPercent > 30)
            return BadRequest(new { message = "GST percent must be between 0% and 30%." });
        if (request.MinPlatformFee < 0 || request.MinPlatformFee > 1000)
            return BadRequest(new { message = "Minimum platform fee must be between ₹0 and ₹1000." });
        if (request.MaxCommissionPercent < request.UserCommissionPercent || request.MaxCommissionPercent > 50)
            return BadRequest(new { message = "Max commission cap must be ≥ user commission % and ≤ 50%." });

        await UpsertSettingAsync("commission.user_percent", request.UserCommissionPercent.ToString());
        await UpsertSettingAsync("commission.pro_percent",  request.ProCommissionPercent.ToString());
        await UpsertSettingAsync("commission.gst_percent",  request.GstPercent.ToString());
        await UpsertSettingAsync("commission.min_fee",      request.MinPlatformFee.ToString());
        await UpsertSettingAsync("commission.max_percent",  request.MaxCommissionPercent.ToString());
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Commission config updated by admin: User={UserPct}%, Pro={ProPct}%, GST={GstPct}%",
            request.UserCommissionPercent, request.ProCommissionPercent, request.GstPercent);

        return Ok(new { message = "Commission configuration updated successfully." });
    }

    private async Task UpsertSettingAsync(string key, string value)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
            _context.AppSettings.Add(new AppSetting { Key = key, Value = value });
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }
    }

    [HttpPatch("pros/{id}/service-radius")]
    public async Task<ActionResult> UpdateProServiceRadius(int id, [FromBody] UpdateServiceRadiusRequest request)
    {
        if (request.ServiceRadiusKm < 1 || request.ServiceRadiusKm > 500)
            return BadRequest(new { message = "Service radius must be between 1 and 500 km." });

        var pro = await _context.Pros.FindAsync(id);
        if (pro == null)
            return NotFound(new { message = "Professional not found." });

        pro.ServiceRadiusKm = request.ServiceRadiusKm;
        pro.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Service radius updated.", serviceRadiusKm = pro.ServiceRadiusKm });
    }

    [HttpPost("users/geocode-backfill")]
    public async Task<ActionResult> GeocodeBackfillUsers()
    {
        var users = await _context.Users
            .Where(u => u.Latitude == null && u.City != null && u.City != "")
            .ToListAsync();

        if (!users.Any())
            return Ok(new { message = "No users need geocoding.", updated = 0, failed = 0, total = 0 });

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ProHub-AddressService/1.0");

        int updated = 0, failed = 0;

        foreach (var user in users)
        {
            try
            {
                var coords = await TryGeocodeAsync(httpClient,
                    user.HouseNameNumber, user.Street1, user.City, user.State, user.Country);

                if (coords.HasValue)
                {
                    user.Latitude = coords.Value.Lat;
                    user.Longitude = coords.Value.Lon;
                    user.UpdatedAt = DateTime.UtcNow;
                    updated++;
                }
                else
                {
                    _logger.LogWarning("Geocoding returned no results for user {Id} ({Email})", user.Id, user.Email);
                    failed++;
                }

                await Task.Delay(1100);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Geocoding failed for user {Id} ({Email}): {Error}", user.Id, user.Email, ex.Message);
                failed++;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"Geocoding complete. {updated} updated, {failed} could not be geocoded.",
            updated,
            failed,
            total = users.Count
        });
    }

    [HttpPost("pros/geocode-backfill")]
    public async Task<ActionResult> GeocodeBackfillPros()
    {
        var pros = await _context.Pros
            .Where(p => p.Latitude == null && p.City != null && p.City != "")
            .ToListAsync();

        if (!pros.Any())
            return Ok(new { message = "No pros need geocoding.", updated = 0, failed = 0, total = 0 });

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ProHub-AddressService/1.0");

        int updated = 0, failed = 0;

        foreach (var pro in pros)
        {
            try
            {
                var coords = await TryGeocodeAsync(httpClient,
                    pro.HouseNameNumber, pro.Street1, pro.City, pro.State, pro.Country);

                if (coords.HasValue)
                {
                    pro.Latitude = coords.Value.Lat;
                    pro.Longitude = coords.Value.Lon;
                    pro.UpdatedAt = DateTime.UtcNow;
                    updated++;
                }
                else
                {
                    _logger.LogWarning("Geocoding returned no results for pro {Id} ({Name})", pro.Id, pro.ProName);
                    failed++;
                }

                await Task.Delay(1100);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Geocoding failed for pro {Id} ({Name}): {Error}", pro.Id, pro.ProName, ex.Message);
                failed++;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"Geocoding complete. {updated} updated, {failed} could not be geocoded.",
            updated,
            failed,
            total = pros.Count
        });
    }

    // Tries progressively simpler queries so fake/generic street numbers don't block city-level geocoding
    private async Task<(double Lat, double Lon)?> TryGeocodeAsync(
        HttpClient httpClient,
        string? houseNumber, string? street, string? city, string? state, string? country)
    {
        var candidates = new[]
        {
            // Full address
            Parts(houseNumber, street, city, state, country),
            // Without house number
            Parts(street, city, state, country),
            // City + state + country only
            Parts(city, state, country),
        };

        foreach (var query in candidates.Where(q => !string.IsNullOrWhiteSpace(q)))
        {
            var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query!)}&format=json&limit=1";
            var json = await httpClient.GetStringAsync(url);
            var results = System.Text.Json.JsonSerializer.Deserialize<NominatimGeoResult[]>(json);

            if (results != null && results.Length > 0 &&
                double.TryParse(results[0].Lat, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat) &&
                double.TryParse(results[0].Lon, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lon))
            {
                return (lat, lon);
            }

            await Task.Delay(1100);
        }

        return null;
    }

    private static string? Parts(params string?[] values)
    {
        var joined = string.Join(", ", values.Where(v => !string.IsNullOrWhiteSpace(v)));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    // GET: api/admin/disputes — list all disputed job completions
    [HttpGet("disputes")]
    public async Task<ActionResult<IEnumerable<object>>> GetDisputes()
    {
        var disputes = await _context.JobCompletions
            .Where(c => c.Status == "Disputed")
            .Include(c => c.Job)
                .ThenInclude(j => j!.User)
            .Include(c => c.Job)
                .ThenInclude(j => j!.AssignedPro)
            .OrderByDescending(c => c.DisputedAt)
            .Select(c => new
            {
                completionId = c.Id,
                jobId = c.JobId,
                jobTitle = c.Job!.Title,
                disputeReason = c.DisputeReason,
                disputedAt = c.DisputedAt,
                completionNotes = c.CompletionNotes,
                consumer = c.Job.User == null ? null : new
                {
                    id = c.Job.User.Id,
                    name = c.Job.User.FirstName + " " + c.Job.User.LastName,
                    email = c.Job.User.Email
                },
                pro = c.Job.AssignedPro == null ? null : new
                {
                    id = c.Job.AssignedPro.Id,
                    name = c.Job.AssignedPro.ProName,
                    businessName = c.Job.AssignedPro.BusinessName,
                    email = c.Job.AssignedPro.Email
                },
                paymentAmount = _context.Payments
                    .Where(p => p.JobId == c.JobId && p.Status == "Completed")
                    .Select(p => (decimal?)p.Amount)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(disputes);
    }

    // POST: api/admin/jobs/{jobId}/completion/resolve
    [HttpPost("jobs/{jobId}/completion/resolve")]
    public async Task<ActionResult> ResolveDispute(int jobId, [FromBody] ResolveDisputeRequest request)
    {
        if (request.Resolution != "complete" && request.Resolution != "refund")
            return BadRequest(new { message = "Resolution must be 'complete' or 'refund'" });

        var completion = await _context.JobCompletions
            .Include(c => c.Job)
                .ThenInclude(j => j!.User)
            .Include(c => c.Job)
                .ThenInclude(j => j!.AssignedPro)
            .FirstOrDefaultAsync(c => c.JobId == jobId);

        if (completion == null)
            return NotFound(new { message = "Completion record not found" });

        if (completion.Status != "Disputed")
            return BadRequest(new { message = $"Completion is not in Disputed status (current: '{completion.Status}')" });

        var job = completion.Job!;
        var consumer = job.User;
        var pro = job.AssignedPro;

        if (!string.IsNullOrWhiteSpace(request.Notes))
            completion.CompletionNotes = (completion.CompletionNotes ?? "") + $"\n[Admin resolution note: {request.Notes}]";

        if (request.Resolution == "complete")
        {
            completion.Status = "Verified";
            completion.VerifiedAt = DateTime.UtcNow;
            completion.VerifiedByConsumer = true;
            job.Status = "Completed";
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin resolved dispute for Job:{JobId} as Completed", jobId);

            // Trigger payout to the pro
            if (job.AssignedProId.HasValue)
            {
                var payment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.JobId == jobId && p.Status == "Completed");
                if (payment != null)
                {
                    try
                    {
                        await _payoutService.CreateAndProcessPayoutAsync(
                            payment.Id, jobId, job.AssignedProId.Value, payment.ProPayout);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Payout creation failed for admin-resolved Job:{JobId}", jobId);
                    }
                }
            }

            if (consumer != null)
                await _notificationService.NotifyAsync(
                    consumer.Email ?? "",
                    consumer.PhoneNumber,
                    $"Dispute resolved — Job #{jobId} marked complete",
                    $"Hi {consumer.FirstName}, your dispute for job \"{job.Title}\" has been reviewed. " +
                    "Our admin team has determined the work was completed satisfactorily. " +
                    "No refund will be issued. Thank you for using yProHub.");

            if (pro != null)
                await _notificationService.NotifyAsync(
                    pro.Email ?? "",
                    pro.PhoneNumber,
                    $"Dispute resolved in your favour — Job #{jobId}",
                    $"Hi {pro.ProName}, the dispute for job \"{job.Title}\" has been reviewed and resolved in your favour. " +
                    "The job is marked as Completed. Thank you for your service on yProHub.");
        }
        else // refund
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.JobId == jobId && p.Status == "Completed");

            if (payment == null)
            {
                _logger.LogWarning("ResolveDispute refund: no completed payment found for Job:{JobId}", jobId);
                return BadRequest(new { message = "No completed payment found for this job — cannot process refund" });
            }

            if (string.IsNullOrEmpty(payment.RazorpayPaymentId))
                return BadRequest(new { message = "Payment has no Razorpay payment ID — cannot process refund" });

            var refundReason = string.IsNullOrWhiteSpace(request.Notes)
                ? "Admin-resolved dispute refund"
                : $"Admin dispute resolution: {request.Notes}";

            var refundId = await _paymentProvider.ProcessRefundAsync(
                payment.RazorpayOrderId ?? "",
                payment.RazorpayPaymentId,
                payment.Amount,
                refundReason);

            if (refundId == null)
            {
                _logger.LogError("Razorpay refund failed for Job:{JobId}, Payment:{PaymentId}", jobId, payment.Id);
                return StatusCode(502, new { message = "Refund request to Razorpay failed — please retry or refund manually in the Razorpay dashboard" });
            }

            payment.Status = "Refunded";
            payment.RefundedAt = DateTime.UtcNow;
            payment.RefundAmount = payment.Amount;
            payment.RefundReason = refundReason;
            completion.Status = "Refunded";
            job.Status = "Open";
            job.AssignedProId = null;
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin refund processed: RefundId:{RefundId} for Job:{JobId}", refundId, jobId);

            if (consumer != null)
                await _notificationService.NotifyAsync(
                    consumer.Email ?? "",
                    consumer.PhoneNumber,
                    $"Refund initiated — Job #{jobId}",
                    $"Hi {consumer.FirstName}, your dispute for job \"{job.Title}\" has been reviewed. " +
                    $"A refund of ₹{payment.Amount:F2} has been initiated to your original payment method. " +
                    "Please allow 5–7 business days for the funds to appear. Thank you for using yProHub.");

            if (pro != null)
                await _notificationService.NotifyAsync(
                    pro.Email ?? "",
                    pro.PhoneNumber,
                    $"Dispute resolved — Job #{jobId} refunded to consumer",
                    $"Hi {pro.ProName}, the dispute for job \"{job.Title}\" has been reviewed. " +
                    "Our admin team has determined that a refund to the consumer is appropriate. " +
                    "The job has been reopened for new bids. If you have questions, please contact support.");

            return Ok(new
            {
                message = "Dispute resolved: refund processed and job reopened for rebidding",
                jobId,
                jobStatus = job.Status,
                completionStatus = completion.Status,
                refundId
            });
        }

        return Ok(new
        {
            message = "Dispute resolved: job marked as Completed",
            jobId,
            jobStatus = job.Status,
            completionStatus = completion.Status
        });
    }
}

file record NominatimGeoResult(
    [property: System.Text.Json.Serialization.JsonPropertyName("lat")] string Lat,
    [property: System.Text.Json.Serialization.JsonPropertyName("lon")] string Lon
);

public class ImpersonateRequest
{
    public int TargetUserId { get; set; }
    public string? TargetUserType { get; set; }
}

public class InviteAdminRequest
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

public class UpdateServiceRadiusRequest
{
    [JsonPropertyName("serviceRadiusKm")]
    public int ServiceRadiusKm { get; set; }
}

public class ResolveDisputeRequest
{
    [JsonPropertyName("resolution")]
    public string Resolution { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class UpdateCommissionConfigRequest
{
    [JsonPropertyName("userCommissionPercent")]
    public decimal UserCommissionPercent { get; set; }

    [JsonPropertyName("proCommissionPercent")]
    public decimal ProCommissionPercent { get; set; }

    [JsonPropertyName("gstPercent")]
    public decimal GstPercent { get; set; }

    [JsonPropertyName("minPlatformFee")]
    public decimal MinPlatformFee { get; set; }

    [JsonPropertyName("maxCommissionPercent")]
    public decimal MaxCommissionPercent { get; set; }
}
