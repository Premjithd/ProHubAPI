using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServiceCategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ServiceCategoriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceCategory>>> GetCategories()
    {
        return await _context.ServiceCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ServiceCategory>> GetCategory(int id)
    {
        var category = await _context.ServiceCategories.FindAsync(id);

        if (category == null)
        {
            return NotFound();
        }

        return category;
    }

    [HttpPost]
    public async Task<ActionResult<ServiceCategory>> CreateCategory(ServiceCategory category)
    {
        category.CreatedAt = DateTime.UtcNow;
        category.UpdatedAt = DateTime.UtcNow;

        _context.ServiceCategories.Add(category);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCategory(int id, ServiceCategory category)
    {
        if (id != category.Id)
        {
            return BadRequest();
        }

        var existingCategory = await _context.ServiceCategories.FindAsync(id);
        if (existingCategory == null)
        {
            return NotFound();
        }

        existingCategory.Name = category.Name;
        existingCategory.Description = category.Description;
        existingCategory.Icon = category.Icon;
        existingCategory.ServiceCount = category.ServiceCount;
        existingCategory.IsActive = category.IsActive;
        existingCategory.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!CategoryExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return Ok(existingCategory);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.ServiceCategories.FindAsync(id);
        if (category == null)
        {
            return NotFound();
        }

        _context.ServiceCategories.Remove(category);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool CategoryExists(int id)
    {
        return _context.ServiceCategories.Any(e => e.Id == id);
    }
}
