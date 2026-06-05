using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.DTOs;
using ServiceProviderAPI.Services;
using System.Security.Claims;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PayoutsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPayoutService _payoutService;
    private readonly ILogger<PayoutsController> _logger;

    public PayoutsController(
        ApplicationDbContext context,
        IPayoutService payoutService,
        ILogger<PayoutsController> logger)
    {
        _context = context;
        _payoutService = payoutService;
        _logger = logger;
    }

    // GET: api/payouts — pro's own earnings
    [HttpGet]
    [Authorize(Roles = "Pro")]
    public async Task<IActionResult> GetMyPayouts()
    {
        var proId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (proId == 0) return Unauthorized();

        var payouts = await _context.Payouts
            .Where(p => p.ProId == proId)
            .Include(p => p.Job)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PayoutDto
            {
                Id = p.Id,
                ProId = p.ProId,
                PaymentId = p.PaymentId,
                JobId = p.JobId,
                JobTitle = p.Job != null ? p.Job.Title : null,
                Amount = p.Amount,
                Status = p.Status,
                Mode = p.Mode,
                RazorpayPayoutId = p.RazorpayPayoutId,
                FailureReason = p.FailureReason,
                CreatedAt = p.CreatedAt,
                ProcessedAt = p.ProcessedAt
            })
            .ToListAsync();

        return Ok(payouts);
    }

    // GET: api/payouts/admin — admin view of all payouts
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAdminPayouts(
        [FromQuery] int? proId = null,
        [FromQuery] string? status = null)
    {
        var query = _context.Payouts
            .Include(p => p.Pro)
            .Include(p => p.Job)
            .AsQueryable();

        if (proId.HasValue)
            query = query.Where(p => p.ProId == proId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);

        var payouts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PayoutDto
            {
                Id = p.Id,
                ProId = p.ProId,
                ProName = p.Pro != null ? p.Pro.ProName : null,
                PaymentId = p.PaymentId,
                JobId = p.JobId,
                JobTitle = p.Job != null ? p.Job.Title : null,
                Amount = p.Amount,
                Status = p.Status,
                Mode = p.Mode,
                RazorpayPayoutId = p.RazorpayPayoutId,
                FailureReason = p.FailureReason,
                CreatedAt = p.CreatedAt,
                ProcessedAt = p.ProcessedAt
            })
            .ToListAsync();

        return Ok(payouts);
    }

    // POST: api/payouts/{id}/retry — admin retries a failed/pending payout
    [HttpPost("{id}/retry")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RetryPayout(int id)
    {
        var success = await _payoutService.ProcessPendingPayoutAsync(id);
        if (!success)
            return BadRequest(new { message = "Payout retry failed. Check that the pro has bank details configured and review logs." });

        return Ok(new { message = "Payout retry initiated successfully" });
    }
}
