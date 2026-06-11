using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;
using ServiceProviderAPI.Services;
using System.Security.Claims;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/pro-users")]
[Authorize(Roles = "Pro")]
public class ProUsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<ProUsersController> _logger;

    public ProUsersController(
        ApplicationDbContext context,
        IEmailService emailService,
        ILogger<ProUsersController> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    private int GetProId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    [HttpGet]
    public async Task<IActionResult> GetMyUsers()
    {
        var proId = GetProId();
        var rows = await _context.ProUserRelationships
            .Where(r => r.ProId == proId && r.Status != "Revoked")
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Status,
                r.InviteEmail,
                r.CreatedAt,
                user = r.User == null ? null : new
                {
                    r.User.Id,
                    r.User.FirstName,
                    r.User.LastName,
                    r.User.Email,
                    r.User.PhoneNumber,
                    r.User.IsEmailVerified
                }
            })
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost("invite")]
    public async Task<IActionResult> InviteUser([FromBody] InviteProUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email is required" });

        var proId = GetProId();
        var pro = await _context.Pros.FindAsync(proId);
        if (pro == null) return NotFound(new { message = "Pro not found" });

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existing = await _context.ProUserRelationships
            .FirstOrDefaultAsync(r => r.ProId == proId
                && r.InviteEmail == normalizedEmail
                && r.Status != "Revoked");

        if (existing != null)
            return BadRequest(new { message = "An invitation is already active for this email" });

        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var relationship = new ProUserRelationship
        {
            ProId = proId,
            InviteEmail = normalizedEmail,
            InviteToken = token,
            InviteExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.ProUserRelationships.Add(relationship);
        await _context.SaveChangesAsync();

        try
        {
            var baseUrl = request.BaseUrl?.TrimEnd('/') ?? "https://app.yprohub.com";
            var inviteUrl = $"{baseUrl}/accept-pro-user-invite?token={token}";
            await _emailService.SendEmailAsync(
                normalizedEmail,
                $"You're invited to join {pro.BusinessName} on yProHub",
                $@"<p>Hi,</p>
                   <p><strong>{pro.ProName}</strong> from <strong>{pro.BusinessName}</strong> has invited you to join their team on yProHub.</p>
                   <p>Click below to create your account. This invite expires in 7 days.</p>
                   <p><a href=""{inviteUrl}"">Accept Invitation</a></p>
                   <p>If you weren't expecting this, you can safely ignore this email.</p>");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Invite email failed for {Email}: {Msg}", normalizedEmail, ex.Message);
        }

        return Ok(new { message = "Invitation sent", id = relationship.Id });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RevokeUser(int id)
    {
        var proId = GetProId();
        var relationship = await _context.ProUserRelationships
            .FirstOrDefaultAsync(r => r.Id == id && r.ProId == proId);

        if (relationship == null)
            return NotFound(new { message = "Relationship not found" });

        relationship.Status = "Revoked";
        relationship.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "User removed from your team" });
    }
}

public class InviteProUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
}
