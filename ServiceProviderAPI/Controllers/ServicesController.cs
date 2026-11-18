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

    private int CallerId() => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");

    // GET: api/services?page=1&pageSize=100&categoryId=&search=&city=&pin=&lat=&lng=&radiusKm=&sortBy=name
    [HttpGet]
    public async Task<ActionResult<PagedResult<ServiceBrowseDto>>> GetServices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] int? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? city = null,
        [FromQuery] string? pin = null,
        [FromQuery] double? lat = null,
        [FromQuery] double? lng = null,
        [FromQuery] double? radiusKm = null,
        [FromQuery] string? sortBy = "name")
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        // Proximity mode: when the searcher's coordinates are supplied, rank by distance
        // to the pro rather than restricting to an exact PIN.
        bool proximity = lat.HasValue && lng.HasValue;

        var query = _context.Services
            .Include(s => s.Pro).ThenInclude(p => p!.Address)
            .Include(s => s.ServiceCategory)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(s => s.ServiceCategoryId == categoryId.Value);

        if (!string.IsNullOrEmpty(search))
        {
            // Partial word search with OR semantics: a service matches if ANY whitespace-
            // separated term appears in any searchable field. This lets "bathroom leak"
            // match records containing either word, while still matching partial words
            // ("plumb" → "plumber").
            var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            query = query.Where(s => terms.Any(t =>
                s.Name.Contains(t) ||
                (s.Description != null && s.Description.Contains(t)) ||
                (s.Pro != null && s.Pro.BusinessName != null && s.Pro.BusinessName.Contains(t)) ||
                (s.Pro != null && s.Pro.ProName != null && s.Pro.ProName.Contains(t))));
        }

        if (!string.IsNullOrEmpty(city))
            query = query.Where(s => s.Pro != null && s.Pro.Address != null &&
                s.Pro.Address.City != null && s.Pro.Address.City.Contains(city));

        // Area filter: restrict to pros located in the given PIN / postal code. Skipped in
        // proximity mode — the client geocodes the PIN into lat/lng so we rank by distance
        // instead (pros in adjacent PINs should still surface).
        if (!proximity && !string.IsNullOrEmpty(pin))
            query = query.Where(s => s.Pro != null && s.Pro.Address != null &&
                s.Pro.Address.ZipPostalCode == pin);

        if (proximity)
        {
            // Pull the filtered set (no Skip/Take) so Haversine can run in memory, then
            // rank nearest-first and paginate.
            var rows = await query
                .Select(s => new
                {
                    Dto = new ServiceBrowseDto
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
                    },
                    Lat = s.Pro != null && s.Pro.Address != null ? s.Pro.Address.Latitude : (double?)null,
                    Lng = s.Pro != null && s.Pro.Address != null ? s.Pro.Address.Longitude : (double?)null,
                })
                .ToListAsync();

            var withDistance = rows.Select(r =>
            {
                if (r.Lat.HasValue && r.Lng.HasValue)
                    r.Dto.DistanceKm = Math.Round(HaversineKm(lat!.Value, lng!.Value, r.Lat.Value, r.Lng.Value), 1);
                return r.Dto;
            });

            // Optional hard radius filter: keep services within radius OR those whose pro has
            // no coordinates (can't position them, so don't silently drop them).
            if (radiusKm.HasValue)
                withDistance = withDistance.Where(d => d.DistanceKm == null || d.DistanceKm <= radiusKm.Value);

            var ordered = withDistance
                .OrderBy(d => d.DistanceKm ?? double.MaxValue)
                .ThenBy(d => d.Name)
                .ToList();

            var itemsP = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Ok(new PagedResult<ServiceBrowseDto> { Items = itemsP, Total = ordered.Count, Page = page, PageSize = pageSize });
        }

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

    // Haversine formula — returns distance in kilometres between two lat/lng points.
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
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
        // The service belongs to the authenticated pro — never trust a ProId from the body.
        service.ProId = CallerId();
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

        // A pro may only edit their own service.
        if (existingService.ProId != CallerId())
            return Forbid();

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

        // A pro may only delete their own service.
        if (service.ProId != CallerId())
            return Forbid();

        _context.Services.Remove(service);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool ServiceExists(int id)
    {
        return _context.Services.Any(e => e.Id == id);
    }
}
