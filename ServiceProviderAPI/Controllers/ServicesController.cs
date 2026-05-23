using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.DTOs;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ServicesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/services?page=1&pageSize=20&categoryId=&search=
    [HttpGet]
    public async Task<ActionResult<PagedResult<Service>>> GetServices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? categoryId = null,
        [FromQuery] string? search = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Services
            .Include(s => s.Pro)
            .Include(s => s.ServiceCategory)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(s => s.ServiceCategoryId == categoryId.Value);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(s =>
                s.Name.Contains(search) ||
                (s.Description != null && s.Description.Contains(search)));

        query = query.OrderBy(s => s.Name);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<Service> { Items = items, Total = total, Page = page, PageSize = pageSize });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Service>> GetService(int id)
    {
        var service = await _context.Services
            .Include(s => s.Pro)
            .Include(s => s.ServiceCategory)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (service == null)
        {
            return NotFound();
        }

        return service;
    }

    [HttpGet("pro/{proId}")]
    public async Task<ActionResult<IEnumerable<Service>>> GetProServices(int proId)
    {
        return await _context.Services
            .Where(s => s.ProId == proId)
            .Include(s => s.ServiceCategory)
            .ToListAsync();
    }

    [HttpPost]
    [Authorize(Roles = "Pro")]
    public async Task<ActionResult<Service>> CreateService(Service service)
    {
        service.CreatedAt = DateTime.UtcNow;
        service.UpdatedAt = DateTime.UtcNow;

        _context.Services.Add(service);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetService), new { id = service.Id }, service);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Pro")]
    public async Task<IActionResult> UpdateService(int id, Service service)
    {
        if (id != service.Id)
        {
            return BadRequest();
        }

        var existingService = await _context.Services.FindAsync(id);
        if (existingService == null)
        {
            return NotFound();
        }

        existingService.Name = service.Name;
        existingService.Description = service.Description;
        existingService.ServiceCategoryId = service.ServiceCategoryId;
        existingService.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!ServiceExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Pro")]
    public async Task<IActionResult> DeleteService(int id)
    {
        var service = await _context.Services.FindAsync(id);
        if (service == null)
        {
            return NotFound();
        }

        _context.Services.Remove(service);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool ServiceExists(int id)
    {
        return _context.Services.Any(e => e.Id == id);
    }
}
