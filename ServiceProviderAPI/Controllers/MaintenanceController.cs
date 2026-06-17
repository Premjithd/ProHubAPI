using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ServiceProviderAPI.Controllers;

/// <summary>
/// Exposes the current maintenance state so the client can show a maintenance page.
/// Always reachable (allow-listed in the maintenance middleware) and anonymous.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class MaintenanceController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public MaintenanceController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>GET: api/maintenance/status — { enabled, message }</summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            enabled = _configuration.GetValue<bool>("Maintenance:Enabled"),
            message = _configuration["Maintenance:Message"]
                      ?? "yProHub is temporarily unavailable for maintenance."
        });
    }
}
