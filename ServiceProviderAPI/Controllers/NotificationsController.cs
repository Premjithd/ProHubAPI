using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;
using System.Security.Claims;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public NotificationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/notifications  — All notifications for the current Pro
    [HttpGet]
    public async Task<IActionResult> GetMyNotifications([FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var proId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (proId == 0) return Unauthorized();

        var query = _context.JobNotifications
            .Where(n => n.ProId == proId);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        var total = await query.CountAsync();
        var notifications = await query
            .Include(n => n.Job)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, notifications });
    }

    // GET: api/notifications/unread-count
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var proId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (proId == 0) return Unauthorized();

        var count = await _context.JobNotifications
            .CountAsync(n => n.ProId == proId && !n.IsRead);

        return Ok(new { count });
    }

    // PUT: api/notifications/{id}/read  — Mark a single notification as read
    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var proId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (proId == 0) return Unauthorized();

        var notification = await _context.JobNotifications.FindAsync(id);
        if (notification == null) return NotFound();
        if (notification.ProId != proId) return Forbid();

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Ok(notification);
    }

    // PUT: api/notifications/read-all  — Mark all as read
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var proId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (proId == 0) return Unauthorized();

        var unread = await _context.JobNotifications
            .Where(n => n.ProId == proId && !n.IsRead)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = $"{unread.Count} notifications marked as read" });
    }
}
