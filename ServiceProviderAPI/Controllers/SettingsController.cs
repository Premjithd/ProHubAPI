using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SettingsController(ApplicationDbContext context) => _context = context;

    // Settings safe to expose to anyone (used by public pages). Any other key — e.g. the
    // commission.* config — is readable only by admins, so the store can't be enumerated.
    private static readonly HashSet<string> PublicKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "show_our_team",
        "show_pro_count_on_categories",
    };

    [HttpGet("{key}")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> GetSetting(string key)
    {
        // Non-public keys are admin-only; return NotFound for others so we don't even confirm existence.
        if (!PublicKeys.Contains(key) && !User.IsInRole("Admin"))
            return NotFound();

        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null) return NotFound();
        return Ok(new { key = setting.Key, value = setting.Value });
    }

    [HttpPut("{key}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> UpdateSetting(string key, [FromBody] UpdateSettingRequest request)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            setting = new AppSetting { Key = key, Value = request.Value };
            _context.AppSettings.Add(setting);
        }
        else
        {
            setting.Value = request.Value;
            setting.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
        return Ok(new { key = setting.Key, value = setting.Value });
    }
}

public record UpdateSettingRequest(string Value);
