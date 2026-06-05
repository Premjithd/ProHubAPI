using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;
using BC = BCrypt.Net.BCrypt;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public UsersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<object>>> GetUsers()
    {
        var users = await _context.Users.ToListAsync();
        return Ok(users.Select(u => SafeUser(u)));
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<object>> GetUser(int id)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(callerIdStr, out int callerId);
        bool isAdmin = User.IsInRole("Admin");

        if (!isAdmin && callerId != id)
            return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        return Ok(SafeUser(user));
    }

    private static object SafeUser(User u) => new
    {
        u.Id, u.FirstName, u.LastName, u.Email, u.PhoneNumber,
        u.HouseNameNumber, u.Street1, u.Street2, u.City, u.State,
        u.Country, u.ZipPostalCode, u.CreatedAt, u.UpdatedAt,
        u.IsEmailVerified, u.IsPhoneVerified, u.UpiVpa
    };

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<User>> CreateUser(User user)
    {
        user.PasswordHash = BC.HashPassword(user.PasswordHash);
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "User")]
    public async Task<ActionResult<User>> UpdateUser(int id, User user)
    {
        if (id != user.Id)
        {
            return BadRequest();
        }

        var existingUser = await _context.Users.FindAsync(id);
        if (existingUser == null)
        {
            return NotFound();
        }

        existingUser.FirstName = user.FirstName;
        existingUser.LastName = user.LastName;

        if (!string.Equals(existingUser.Email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            existingUser.Email = user.Email;
            existingUser.IsEmailVerified = false;
        }

        if (existingUser.PhoneNumber != user.PhoneNumber)
        {
            existingUser.PhoneNumber = user.PhoneNumber;
            existingUser.IsPhoneVerified = false;
        }

        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            existingUser.PasswordHash = BC.HashPassword(user.PasswordHash);
        }
        existingUser.HouseNameNumber = user.HouseNameNumber;
        existingUser.Street1 = user.Street1;
        existingUser.Street2 = user.Street2;
        existingUser.City = user.City;
        existingUser.State = user.State;
        existingUser.Country = user.Country;
        existingUser.ZipPostalCode = user.ZipPostalCode;
        existingUser.UpiVpa = user.UpiVpa;
        existingUser.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!UserExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return Ok(SafeUser(existingUser));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id}/payment-details")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetPaymentDetails(int id)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (callerId != id) return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        return Ok(new { upiVpa = user.UpiVpa, hasPaymentDetails = !string.IsNullOrWhiteSpace(user.UpiVpa) });
    }

    [HttpPut("{id}/payment-details")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> UpdatePaymentDetails(int id, [FromBody] UpdateUserPaymentDetailsRequest request)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (callerId != id) return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.UpiVpa = string.IsNullOrWhiteSpace(request.UpiVpa) ? null : request.UpiVpa.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Payment details updated successfully", upiVpa = user.UpiVpa });
    }

    private bool UserExists(int id)
    {
        return _context.Users.Any(e => e.Id == id);
    }
}

public class UpdateUserPaymentDetailsRequest
{
    public string? UpiVpa { get; set; }
}
