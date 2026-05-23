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
public class ProsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<object>>> GetPros()
    {
        var pros = await _context.Pros.Include(p => p.Services).ToListAsync();
        return Ok(pros.Select(p => SafePro(p)));
    }

    [HttpGet("browse")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> BrowsePros(
        [FromQuery] string? search = null,
        [FromQuery] int? categoryId = null)
    {
        var query = _context.Pros.Include(p => p.Services).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(p =>
                p.ProName.ToLower().Contains(term) ||
                p.BusinessName.ToLower().Contains(term) ||
                (p.City != null && p.City.ToLower().Contains(term)) ||
                p.Services.Any(s => s.Name.ToLower().Contains(term)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.Services.Any(s => s.ServiceCategoryId == categoryId.Value));
        }

        var pros = await query.ToListAsync();

        return Ok(pros.Select(p => new
        {
            p.Id, p.ProName, p.BusinessName,
            p.City, p.State, p.Country,
            p.Latitude, p.Longitude, p.ServiceRadiusKm,
            p.IsEmailVerified,
            Services = p.Services?.Select(s => new { s.Id, s.Name, s.Price })
        }));
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<object>> GetPro(int id)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(callerIdStr, out int callerId);
        bool isAdmin = User.IsInRole("Admin");
        bool isPro = User.IsInRole("Pro");

        // Pros can only view their own full profile; users and admins can view any pro's public profile
        if (isPro && !isAdmin && callerId != id)
            return Forbid();

        var pro = await _context.Pros
            .Include(p => p.Services)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pro == null) return NotFound();

        return Ok(SafePro(pro));
    }

    private static object SafePro(Pro p) => new
    {
        p.Id, p.ProName, p.BusinessName, p.Email, p.PhoneNumber,
        p.HouseNameNumber, p.Street1, p.Street2, p.City, p.State,
        p.Country, p.ZipPostalCode, p.ServiceRadiusKm,
        p.Latitude, p.Longitude, p.CreatedAt, p.UpdatedAt,
        p.IsEmailVerified, p.IsPhoneVerified,
        Services = p.Services?.Select(s => new { s.Id, s.Name, s.Description, s.Price })
    };

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Pro>> CreatePro(Pro pro)
    {
        pro.PasswordHash = BC.HashPassword(pro.PasswordHash);
        pro.CreatedAt = DateTime.UtcNow;
        pro.UpdatedAt = DateTime.UtcNow;

        _context.Pros.Add(pro);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPro), new { id = pro.Id }, pro);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Pro")]
    public async Task<ActionResult<Pro>> UpdatePro(int id, Pro pro)
    {
        if (id != pro.Id)
        {
            return BadRequest();
        }

        var existingPro = await _context.Pros.FindAsync(id);
        if (existingPro == null)
        {
            return NotFound();
        }

        existingPro.ProName = pro.ProName;
        existingPro.Email = pro.Email;
        if (!string.IsNullOrEmpty(pro.PasswordHash))
        {
            existingPro.PasswordHash = BC.HashPassword(pro.PasswordHash);
        }
        existingPro.PhoneNumber = pro.PhoneNumber;
        existingPro.BusinessName = pro.BusinessName;
        existingPro.HouseNameNumber = pro.HouseNameNumber;
        existingPro.Street1 = pro.Street1;
        existingPro.Street2 = pro.Street2;
        existingPro.City = pro.City;
        existingPro.State = pro.State;
        existingPro.Country = pro.Country;
        existingPro.ZipPostalCode = pro.ZipPostalCode;
        existingPro.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!ProExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return Ok(existingPro);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Pro")]
    public async Task<IActionResult> DeletePro(int id)
    {
        var pro = await _context.Pros.FindAsync(id);
        if (pro == null)
        {
            return NotFound();
        }

        _context.Pros.Remove(pro);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool ProExists(int id)
    {
        return _context.Pros.Any(e => e.Id == id);
    }
}
