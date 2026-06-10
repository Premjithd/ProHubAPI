using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.DTOs;
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
        var users = await _context.Users.Include(u => u.Address).ToListAsync();
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

        var user = await _context.Users.Include(u => u.Address).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        return Ok(SafeUser(user));
    }

    private static object SafeUser(User u) => new
    {
        u.Id, u.FirstName, u.LastName, u.Email, u.PhoneNumber,
        HouseNameNumber = u.Address != null ? u.Address.HouseNameNumber : null,
        Street1 = u.Address != null ? u.Address.Street1 : null,
        Street2 = u.Address != null ? u.Address.Street2 : null,
        City = u.Address != null ? u.Address.City : null,
        District = u.Address != null ? u.Address.District : null,
        State = u.Address != null ? u.Address.State : null,
        Country = u.Address != null ? u.Address.Country : null,
        ZipPostalCode = u.Address != null ? u.Address.ZipPostalCode : null,
        Latitude = u.Address != null ? u.Address.Latitude : (double?)null,
        Longitude = u.Address != null ? u.Address.Longitude : (double?)null,
        u.CreatedAt, u.UpdatedAt,
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
    [Authorize(Roles = "User,Admin")]
    public async Task<ActionResult<object>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        if (id != request.Id)
            return BadRequest();

        var existingUser = await _context.Users.Include(u => u.Address).FirstOrDefaultAsync(u => u.Id == id);
        if (existingUser == null)
            return NotFound();

        if (request.FirstName != null) existingUser.FirstName = request.FirstName;
        if (request.LastName != null) existingUser.LastName = request.LastName;

        if (request.Email != null && !string.Equals(existingUser.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            existingUser.Email = request.Email;
            existingUser.IsEmailVerified = false;
        }

        if (request.PhoneNumber != null && existingUser.PhoneNumber != request.PhoneNumber)
        {
            existingUser.PhoneNumber = request.PhoneNumber;
            existingUser.IsPhoneVerified = false;
        }

        if (!string.IsNullOrEmpty(request.PasswordHash))
            existingUser.PasswordHash = BC.HashPassword(request.PasswordHash);

        existingUser.UpiVpa = request.UpiVpa;
        existingUser.UpdatedAt = DateTime.UtcNow;

        // Update or create address record
        if (existingUser.Address == null)
        {
            var newAddr = new Address { AddressType = "User", CreatedAt = DateTime.UtcNow };
            _context.Addresses.Add(newAddr);
            existingUser.Address = newAddr;
        }
        var addr = existingUser.Address;
        addr.HouseNameNumber = request.HouseNameNumber;
        addr.Street1 = request.Street1;
        addr.Street2 = request.Street2;
        addr.City = request.City;
        addr.District = request.District;
        addr.State = request.State;
        addr.Country = request.Country;
        addr.ZipPostalCode = request.ZipPostalCode;
        if (request.Latitude.HasValue) addr.Latitude = request.Latitude;
        if (request.Longitude.HasValue) addr.Longitude = request.Longitude;
        addr.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!UserExists(id))
                return NotFound();
            throw;
        }

        return Ok(SafeUser(existingUser));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.Include(u => u.Address).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
            return NotFound();

        // Delete the associated address first to avoid orphans
        if (user.Address != null)
            _context.Addresses.Remove(user.Address);

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
