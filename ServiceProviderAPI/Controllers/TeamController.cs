using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeamController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TeamController(ApplicationDbContext context) => _context = context;

    // Public: active team members for the /about page.
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> GetPublic()
    {
        var members = await _context.TeamMembers
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id)
            .Select(m => new { m.Id, m.Name, m.Role, m.Bio, m.Initials })
            .ToListAsync();
        return Ok(members);
    }

    // Admin: full list (including inactive) for management.
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        var members = await _context.TeamMembers
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id)
            .ToListAsync();
        return Ok(members);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TeamMember>> Create([FromBody] TeamMemberRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Name is required." });

        var member = new TeamMember
        {
            Name = req.Name.Trim(),
            Role = req.Role?.Trim(),
            Bio = req.Bio?.Trim(),
            Initials = string.IsNullOrWhiteSpace(req.Initials) ? Initials(req.Name) : req.Initials.Trim().ToUpperInvariant(),
            DisplayOrder = req.DisplayOrder,
            IsActive = req.IsActive,
        };
        _context.TeamMembers.Add(member);
        await _context.SaveChangesAsync();
        return Ok(member);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TeamMember>> Update(int id, [FromBody] TeamMemberRequest req)
    {
        var member = await _context.TeamMembers.FindAsync(id);
        if (member == null) return NotFound();
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Name is required." });

        member.Name = req.Name.Trim();
        member.Role = req.Role?.Trim();
        member.Bio = req.Bio?.Trim();
        member.Initials = string.IsNullOrWhiteSpace(req.Initials) ? Initials(req.Name) : req.Initials.Trim().ToUpperInvariant();
        member.DisplayOrder = req.DisplayOrder;
        member.IsActive = req.IsActive;
        member.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(member);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var member = await _context.TeamMembers.FindAsync(id);
        if (member == null) return NotFound();
        _context.TeamMembers.Remove(member);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Team member removed." });
    }

    private static string Initials(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "";
        var s = parts[0][..1] + (parts.Length > 1 ? parts[^1][..1] : "");
        return s.ToUpperInvariant();
    }
}

public class TeamMemberRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Bio { get; set; }
    public string? Initials { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
