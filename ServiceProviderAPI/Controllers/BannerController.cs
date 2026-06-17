using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ServiceProviderAPI.Controllers;

/// <summary>
/// Exposes a configurable announcement banner (appsettings Banner:Enabled / Banner:Message)
/// shown on the home page. Anonymous and read-only.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class BannerController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public BannerController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>GET: api/banner — { enabled, message }</summary>
    [HttpGet]
    public IActionResult GetBanner()
    {
        return Ok(new
        {
            enabled = _configuration.GetValue<bool>("Banner:Enabled"),
            message = _configuration["Banner:Message"] ?? string.Empty
        });
    }
}
