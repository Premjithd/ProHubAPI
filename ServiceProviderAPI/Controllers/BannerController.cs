using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;

namespace ServiceProviderAPI.Controllers;

/// <summary>
/// Exposes a configurable announcement banner shown on the home page. Admins edit it
/// via the AppSettings keys <c>banner_enabled</c> / <c>banner_message</c>
/// (PUT api/settings/{key}); when those are unset it falls back to the appsettings
/// Banner:Enabled / Banner:Message values. Anonymous and read-only.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class BannerController : ControllerBase
{
    private const string EnabledKey = "banner_enabled";
    private const string MessageKey = "banner_message";

    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;

    public BannerController(IConfiguration configuration, ApplicationDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    /// <summary>GET: api/banner — { enabled, message }</summary>
    [HttpGet]
    public async Task<IActionResult> GetBanner()
    {
        var settings = await _context.AppSettings
            .Where(s => s.Key == EnabledKey || s.Key == MessageKey)
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        var enabled = settings.TryGetValue(EnabledKey, out var enabledValue)
            ? string.Equals(enabledValue, "true", StringComparison.OrdinalIgnoreCase)
            : _configuration.GetValue<bool>("Banner:Enabled");

        var message = settings.TryGetValue(MessageKey, out var messageValue)
            ? messageValue
            : _configuration["Banner:Message"] ?? string.Empty;

        return Ok(new { enabled, message });
    }
}
