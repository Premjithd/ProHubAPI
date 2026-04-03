using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.DTOs;
using ServiceProviderAPI.Models;
using System.Security.Claims;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MaterialsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MaterialsController> _logger;

    public MaterialsController(ApplicationDbContext context, ILogger<MaterialsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// GET: api/materials - Get all active materials (optionally filter by category)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<MaterialDto>>> GetMaterials(
        [FromQuery] int? categoryId = null,
        [FromQuery] bool activeOnly = true)
    {
        try
        {
            var query = _context.Materials
                .Include(m => m.Category)
                .AsQueryable();

            if (activeOnly)
                query = query.Where(m => m.IsActive);

            if (categoryId.HasValue)
                query = query.Where(m => m.ServiceCategoryId == categoryId.Value);

            var materials = await query
                .OrderBy(m => m.Category!.Name)
                .ThenBy(m => m.Name)
                .ToListAsync();

            var dtos = materials.Select(m => new MaterialDto
            {
                Id = m.Id,
                ServiceCategoryId = m.ServiceCategoryId,
                CategoryName = m.Category?.Name,
                Name = m.Name,
                Brand = m.Brand,
                UnitPrice = m.UnitPrice,
                Description = m.Description,
                IsActive = m.IsActive,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            });

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching materials: {ex.Message}");
            return StatusCode(500, new { message = "Error fetching materials", error = ex.Message });
        }
    }

    /// <summary>
    /// GET: api/materials/{id} - Get material by ID
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<MaterialDto>> GetMaterial(int id)
    {
        try
        {
            var material = await _context.Materials
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (material == null)
                return NotFound(new { message = "Material not found" });

            var dto = new MaterialDto
            {
                Id = material.Id,
                ServiceCategoryId = material.ServiceCategoryId,
                CategoryName = material.Category?.Name,
                Name = material.Name,
                Brand = material.Brand,
                UnitPrice = material.UnitPrice,
                Description = material.Description,
                IsActive = material.IsActive,
                CreatedAt = material.CreatedAt,
                UpdatedAt = material.UpdatedAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching material: {ex.Message}");
            return StatusCode(500, new { message = "Error fetching material", error = ex.Message });
        }
    }

    /// <summary>
    /// POST: api/materials - Create new material (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<MaterialDto>> CreateMaterial([FromBody] CreateMaterialRequest request)
    {
        try
        {
            // Verify user is admin (basic check - enhance with role-based authorization)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized("User not found");

            // Verify category exists
            var category = await _context.ServiceCategories.FindAsync(request.ServiceCategoryId);
            if (category == null)
                return BadRequest(new { message = "Service category not found" });

            var material = new Material
            {
                ServiceCategoryId = request.ServiceCategoryId,
                Name = request.Name,
                Brand = request.Brand,
                UnitPrice = request.UnitPrice,
                Description = request.Description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Materials.Add(material);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Material created: {material.Id} - {material.Name}");

            var dto = new MaterialDto
            {
                Id = material.Id,
                ServiceCategoryId = material.ServiceCategoryId,
                CategoryName = category.Name,
                Name = material.Name,
                Brand = material.Brand,
                UnitPrice = material.UnitPrice,
                Description = material.Description,
                IsActive = material.IsActive,
                CreatedAt = material.CreatedAt,
                UpdatedAt = material.UpdatedAt
            };

            return CreatedAtAction(nameof(GetMaterial), new { id = material.Id }, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating material: {ex.Message}");
            return StatusCode(500, new { message = "Error creating material", error = ex.Message });
        }
    }

    /// <summary>
    /// PUT: api/materials/{id} - Update material
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateMaterial(int id, [FromBody] UpdateMaterialRequest request)
    {
        try
        {
            var material = await _context.Materials.FindAsync(id);
            if (material == null)
                return NotFound(new { message = "Material not found" });

            if (!string.IsNullOrEmpty(request.Name))
                material.Name = request.Name;

            if (!string.IsNullOrEmpty(request.Brand))
                material.Brand = request.Brand;

            if (request.UnitPrice.HasValue)
                material.UnitPrice = request.UnitPrice.Value;

            if (!string.IsNullOrEmpty(request.Description))
                material.Description = request.Description;

            if (request.IsActive.HasValue)
                material.IsActive = request.IsActive.Value;

            material.UpdatedAt = DateTime.UtcNow;

            _context.Materials.Update(material);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Material updated: {id}");
            return Ok(new { message = "Material updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating material: {ex.Message}");
            return StatusCode(500, new { message = "Error updating material", error = ex.Message });
        }
    }

    /// <summary>
    /// DELETE: api/materials/{id} - Delete material (soft delete by setting IsActive to false)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteMaterial(int id)
    {
        try
        {
            var material = await _context.Materials.FindAsync(id);
            if (material == null)
                return NotFound(new { message = "Material not found" });

            material.IsActive = false;
            material.UpdatedAt = DateTime.UtcNow;

            _context.Materials.Update(material);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Material deleted: {id}");
            return Ok(new { message = "Material deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting material: {ex.Message}");
            return StatusCode(500, new { message = "Error deleting material", error = ex.Message });
        }
    }
}
