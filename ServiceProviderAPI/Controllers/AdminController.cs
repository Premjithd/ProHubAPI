using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;
using ServiceProviderAPI.Services;
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

    public AdminController(ApplicationDbContext context, IJwtService jwtService, IHttpContextAccessor httpContextAccessor, IEmailService emailService, ILogger<AdminController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _httpContextAccessor = httpContextAccessor;
        _emailService = emailService;
        _logger = logger;
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

                impersonationToken = _jwtService.GenerateToken(user, "User");
            }
            else
            {
                var pro = await _context.Pros.FindAsync(request.TargetUserId);
                if (pro == null)
                    return NotFound(new { message = "Professional not found" });

                impersonationToken = _jwtService.GenerateToken(pro, "Pro");
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
}

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
