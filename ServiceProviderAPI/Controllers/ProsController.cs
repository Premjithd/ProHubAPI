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
    public async Task<ActionResult<IEnumerable<Pro>>> GetPros()
    {
        return await _context.Pros
            .Include(p => p.Services)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Pro>> GetPro(int id)
    {
        var pro = await _context.Pros
            .Include(p => p.Services)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pro == null)
        {
            return NotFound();
        }

        return pro;
    }

    [HttpPost]
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
