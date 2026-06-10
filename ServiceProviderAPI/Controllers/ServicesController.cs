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

    // GET: api/services?page=1&pageSize=100&categoryId=&search=&city=&sortBy=name
    [HttpGet]
    public async Task<ActionResult<PagedResult<ServiceBrowseDto>>> GetServices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] int? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? city = null,
        [FromQuery] string? sortBy = "name")
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.Services
            .Include(s => s.Pro).ThenInclude(p => p!.Address)
            .Include(s => s.ServiceCategory)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(s => s.ServiceCategoryId == categoryId.Value);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(s =>
                s.Name.Contains(search) ||
                (s.Description != null && s.Description.Contains(search)) ||
                (s.Pro != null && s.Pro.BusinessName != null && s.Pro.BusinessName.Contains(search)) ||
                (s.Pro != null && s.Pro.ProName != null && s.Pro.ProName.Contains(search)));

        if (!string.IsNullOrEmpty(city))
            query = query.Where(s => s.Pro != null && s.Pro.Address != null &&
                s.Pro.Address.City != null && s.Pro.Address.City.Contains(city));

        query = sortBy switch
        {
            "price-low"  => query.OrderBy(s => s.Price),
            "price-high" => query.OrderByDescending(s => s.Price),
            _            => query.OrderBy(s => s.Name)
        };

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ServiceBrowseDto
            {
                Id                = s.Id,
                Name              = s.Name,
                Description       = s.Description,
                Price             = s.Price,
                ImageUrl          = s.ImageUrl,
                ProId             = s.ProId,
                ProName           = s.Pro != null ? s.Pro.ProName : null,
                BusinessName      = s.Pro != null ? s.Pro.BusinessName : null,
                City              = s.Pro != null && s.Pro.Address != null ? s.Pro.Address.City : null,
                State             = s.Pro != null && s.Pro.Address != null ? s.Pro.Address.State : null,
                ServiceCategoryId = s.ServiceCategoryId,
                CategoryName      = s.ServiceCategory != null ? s.ServiceCategory.Name : null,
                CategoryIcon      = s.ServiceCategory != null ? s.ServiceCategory.Icon : null,
            })
            .ToListAsync();

        return Ok(new PagedResult<ServiceBrowseDto> { Items = items, Total = total, Page = page, PageSize = pageSize });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ServiceBrowseDto>> GetService(int id)
    {
        var dto = await _context.Services
            .Where(s => s.Id == id)
            .Select(s => new ServiceBrowseDto
            {
                Id                = s.Id,
                Name              = s.Name,
                Description       = s.Description,
                Price             = s.Price,
                ImageUrl          = s.ImageUrl,
                ProId             = s.ProId,
                ProName           = s.Pro != null ? s.Pro.ProName : null,
                BusinessName      = s.Pro != null ? s.Pro.BusinessName : null,
                City              = s.Pro != null && s.Pro.Address != null ? s.Pro.Address.City : null,
                State             = s.Pro != null && s.Pro.Address != null ? s.Pro.Address.State : null,
                ServiceCategoryId = s.ServiceCategoryId,
                CategoryName      = s.ServiceCategory != null ? s.ServiceCategory.Name : null,
                CategoryIcon      = s.ServiceCategory != null ? s.ServiceCategory.Icon : null,
            })
            .FirstOrDefaultAsync();

        if (dto == null)
            return NotFound();

        return Ok(dto);
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
        existingService.Price = service.Price;
        existingService.ImageUrl = service.ImageUrl;
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
