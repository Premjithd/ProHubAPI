using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceProviderAPI.Models;
using ServiceProviderAPI.Services;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/service-areas")]
public class ServiceAreaController : ControllerBase
{
    private readonly IServiceAreaService _service;
    private readonly ILogger<ServiceAreaController> _logger;

    public ServiceAreaController(IServiceAreaService service, ILogger<ServiceAreaController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET /api/service-areas — all areas (admin only)
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        if (!IsAdmin()) return Forbid();
        var areas = await _service.GetAllAsync();
        return Ok(areas);
    }

    // GET /api/service-areas/active — active areas (public, for frontend filtering)
    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var areas = await _service.GetActiveAsync();
        return Ok(areas);
    }

    // GET /api/service-areas/check — check if a location is in service area (public)
    [HttpGet("check")]
    public async Task<IActionResult> Check(
        [FromQuery] string country,
        [FromQuery] string? state,
        [FromQuery] string? district,
        [FromQuery] string? pinCode)
    {
        if (string.IsNullOrWhiteSpace(country))
            return BadRequest(new { message = "Country is required" });

        var inArea = await _service.IsInServiceAreaAsync(country, state, district, pinCode);
        return Ok(new { inServiceArea = inArea, country, state, district, pinCode });
    }

    // POST /api/service-areas — add area (admin only)
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Add([FromBody] ServiceArea area)
    {
        if (!IsAdmin()) return Forbid();
        if (string.IsNullOrWhiteSpace(area.Country))
            return BadRequest(new { message = "Country is required" });

        area.IsAutoEnrolled = false;
        var created = await _service.AddAsync(area);
        _logger.LogInformation("Admin added service area: {Country}/{State}/{District}/{PinCode}", area.Country, area.State, area.District, area.PinCode);
        return Ok(created);
    }

    // PUT /api/service-areas/{id} — update area (admin only)
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] ServiceArea area)
    {
        if (!IsAdmin()) return Forbid();
        if (string.IsNullOrWhiteSpace(area.Country))
            return BadRequest(new { message = "Country is required" });

        var updated = await _service.UpdateAsync(id, area);
        if (updated == null) return NotFound(new { message = "Service area not found" });
        return Ok(updated);
    }

    // DELETE /api/service-areas/{id} — delete area (admin only)
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdmin()) return Forbid();
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound(new { message = "Service area not found" });
        _logger.LogInformation("Admin deleted service area ID {Id}", id);
        return Ok(new { message = "Service area deleted" });
    }

    // POST /api/service-areas/{id}/toggle — toggle IsActive (admin only)
    [HttpPost("{id}/toggle")]
    [Authorize]
    public async Task<IActionResult> Toggle(int id)
    {
        if (!IsAdmin()) return Forbid();
        var newState = await _service.ToggleActiveAsync(id);
        _logger.LogInformation("Admin toggled service area ID {Id} → IsActive={IsActive}", id, newState);
        return Ok(new { id, isActive = newState });
    }

    private bool IsAdmin()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        return role == "Admin";
    }
}
