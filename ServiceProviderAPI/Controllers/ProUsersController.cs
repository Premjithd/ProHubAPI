using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;

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

    // Get all users under a pro
    [HttpGet("pro/{proId}/users")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsersUnderPro(int proId)
    {
        var pro = await _context.Pros.FindAsync(proId);
        if (pro == null)
            return NotFound("Pro not found");

        var users = await _context.ProUsers
            .Where(pu => pu.ProId == proId)
            .Include(pu => pu.User)
            .Select(pu => new UserDto
            {
                Id = pu.User.Id,
                Name = $"{pu.User.FirstName} {pu.User.LastName}",
                Email = pu.User.Email,
                PhoneNumber = pu.User.PhoneNumber,
                IsEmailVerified = pu.User.IsEmailVerified,
                IsPhoneVerified = pu.User.IsPhoneVerified
            })
            .ToListAsync();

        return Ok(users);
    }

    // Add a user under a pro
    [HttpPost("pro/{proId}/users")]
    public async Task<ActionResult> AddUserUnderPro(int proId, [FromBody] AddUserRequest request)
    {
        var pro = await _context.Pros.FindAsync(proId);
        if (pro == null)
            return NotFound("Pro not found");

        var user = await _context.Users.FindAsync(request.UserId);
        if (user == null)
            return NotFound("User not found");

        // Check if relationship already exists
        var existingRelation = await _context.ProUsers
            .FirstOrDefaultAsync(pu => pu.ProId == proId && pu.UserId == request.UserId);

        if (existingRelation != null)
            return BadRequest("User is already added under this pro");

        var proUser = new ProUser
        {
            ProId = proId,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProUsers.Add(proUser);
        await _context.SaveChangesAsync();

        return Ok(new { message = "User added successfully under pro" });
    }

    // Remove a user from under a pro
    [HttpDelete("pro/{proId}/users/{userId}")]
    public async Task<ActionResult> RemoveUserFromPro(int proId, int userId)
    {
        var proUser = await _context.ProUsers
            .FirstOrDefaultAsync(pu => pu.ProId == proId && pu.UserId == userId);

        if (proUser == null)
            return NotFound("Relationship not found");

        _context.ProUsers.Remove(proUser);
        await _context.SaveChangesAsync();

        return Ok(new { message = "User removed successfully from pro" });
    }

    // Get all pros that a user is under
    [HttpGet("user/{userId}/pros")]
    public async Task<ActionResult<IEnumerable<ProDto>>> GetProsForUser(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return NotFound("User not found");

        var pros = await _context.ProUsers
            .Where(pu => pu.UserId == userId)
            .Include(pu => pu.Pro)
            .Select(pu => new ProDto
            {
                Id = pu.Pro.Id,
                Name = pu.Pro.ProName,
                Email = pu.Pro.Email,
                PhoneNumber = pu.Pro.PhoneNumber,
                BusinessName = pu.Pro.BusinessName,
                IsEmailVerified = pu.Pro.IsEmailVerified,
                IsPhoneVerified = pu.Pro.IsPhoneVerified
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
    public string Name { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
}

public class ProDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string BusinessName { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
}
