using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;
using System.Security.Claims;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProUsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProUsersController(ApplicationDbContext context)
    {
        _context = context;
    }

    private string? CallerId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    private bool IsAdmin => User.IsInRole("Admin");

    // Get all users under a pro — caller must be that Pro or an Admin
    [HttpGet("pro/{proId}/users")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsersUnderPro(int proId)
    {
        if (!IsAdmin && CallerId != proId.ToString())
            return Forbid();

        var pro = await _context.Pros.FindAsync(proId);
        if (pro == null)
            return NotFound("Pro not found");

        var users = await _context.AdminUsers
            .Where(au => au.ProId == proId && au.UserId != null)
            .Include(au => au.User)
            .Select(au => new UserDto
            {
                Id = au.User!.Id,
                Name = $"{au.User.FirstName} {au.User.LastName}",
                Email = au.User.Email,
                PhoneNumber = au.User.PhoneNumber,
                IsEmailVerified = au.User.IsEmailVerified,
                IsPhoneVerified = au.User.IsPhoneVerified
            })
            .ToListAsync();

        return Ok(users);
    }

    // Add a user under a pro — Admin only
    [HttpPost("pro/{proId}/users")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> AddUserUnderPro(int proId, [FromBody] AddUserRequest request)
    {
        var pro = await _context.Pros.FindAsync(proId);
        if (pro == null)
            return NotFound("Pro not found");

        var user = await _context.Users.FindAsync(request.UserId);
        if (user == null)
            return NotFound("User not found");

        var existingRelation = await _context.AdminUsers
            .FirstOrDefaultAsync(au => au.ProId == proId && au.UserId == request.UserId);

        if (existingRelation != null)
            return BadRequest(new { message = "User is already linked to this pro" });

        _context.AdminUsers.Add(new AdminUser
        {
            ProId = proId,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        return Ok(new { message = "User linked successfully" });
    }

    // Remove a user from a pro — caller must be that Pro or an Admin
    [HttpDelete("pro/{proId}/users/{userId}")]
    public async Task<ActionResult> RemoveUserFromPro(int proId, int userId)
    {
        if (!IsAdmin && CallerId != proId.ToString())
            return Forbid();

        var adminUser = await _context.AdminUsers
            .FirstOrDefaultAsync(au => au.ProId == proId && au.UserId == userId);

        if (adminUser == null)
            return NotFound("Relationship not found");

        _context.AdminUsers.Remove(adminUser);
        await _context.SaveChangesAsync();

        return Ok(new { message = "User unlinked successfully" });
    }

    // Get all pros for a user — caller must be that User or an Admin
    [HttpGet("user/{userId}/pros")]
    public async Task<ActionResult<IEnumerable<ProDto>>> GetProsForUser(int userId)
    {
        if (!IsAdmin && CallerId != userId.ToString())
            return Forbid();

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return NotFound("User not found");

        var pros = await _context.AdminUsers
            .Where(au => au.UserId == userId && au.ProId != null)
            .Include(au => au.Pro)
            .Select(au => new ProDto
            {
                Id = au.Pro!.Id,
                Name = au.Pro.ProName,
                Email = au.Pro.Email,
                PhoneNumber = au.Pro.PhoneNumber,
                BusinessName = au.Pro.BusinessName,
                IsEmailVerified = au.Pro.IsEmailVerified,
                IsPhoneVerified = au.Pro.IsPhoneVerified
            })
            .ToListAsync();

        return Ok(pros);
    }
}

public class AddUserRequest
{
    public int UserId { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
}

public class ProDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
}
