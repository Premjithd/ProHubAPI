using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;
using ServiceProviderAPI.Services;
using System.Security.Claims;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/jobs/{jobId}/completion")]
[Authorize]
public class JobCompletionController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPayoutService _payoutService;
    private readonly ILogger<JobCompletionController> _logger;

    public JobCompletionController(
        ApplicationDbContext context,
        IPayoutService payoutService,
        ILogger<JobCompletionController> logger)
    {
        _context = context;
        _payoutService = payoutService;
        _logger = logger;
    }

    // GET: api/jobs/{jobId}/completion
    [HttpGet]
    public async Task<IActionResult> GetCompletion(int jobId)
    {
        var completion = await _context.JobCompletions
            .Include(c => c.Job)
            .FirstOrDefaultAsync(c => c.JobId == jobId);

        if (completion == null)
            return NotFound(new { message = "No completion record found for this job" });

        return Ok(completion);
    }

    // POST: api/jobs/{jobId}/completion/verify  — Consumer confirms work is done
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyCompletion(int jobId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (userId == 0) return Unauthorized();

        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null) return NotFound(new { message = "Job not found" });

        if (job.UserId != userId)
            return Forbid("Only the job owner can verify completion");

        var completion = await _context.JobCompletions.FirstOrDefaultAsync(c => c.JobId == jobId);
        if (completion == null)
            return BadRequest(new { message = "No completion submission found. The professional must submit completion first." });

        if (completion.Status == "Verified")
            return BadRequest(new { message = "Completion already verified" });

        completion.Status = "Verified";
        completion.VerifiedByConsumer = true;
        completion.VerifiedAt = DateTime.UtcNow;

        job.Status = "Completed";
        job.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Trigger payout to the pro
        if (job.AssignedProId.HasValue)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.JobId == jobId && p.Status == "Completed");
            if (payment != null)
            {
                try
                {
                    await _payoutService.CreateAndProcessPayoutAsync(
                        payment.Id, jobId, job.AssignedProId.Value, payment.ProPayout);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Payout creation failed for Job:{JobId} — payout can be retried manually", jobId);
                }
            }
        }

        return Ok(new { message = "Job completion verified successfully", job, completion });
    }

    // POST: api/jobs/{jobId}/completion/dispute  — Consumer raises a dispute
    [HttpPost("dispute")]
    public async Task<IActionResult> DisputeCompletion(int jobId, [FromBody] DisputeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "A dispute reason is required" });

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (userId == 0) return Unauthorized();

        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null) return NotFound(new { message = "Job not found" });

        if (job.UserId != userId)
            return Forbid("Only the job owner can raise a dispute");

        var completion = await _context.JobCompletions.FirstOrDefaultAsync(c => c.JobId == jobId);
        if (completion == null)
            return BadRequest(new { message = "No completion submission found to dispute" });

        if (completion.Status is "Verified" or "Refunded")
            return BadRequest(new { message = "Cannot dispute a completion that is already verified or refunded" });

        completion.Status = "Disputed";
        completion.DisputeReason = request.Reason;
        completion.DisputedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Dispute raised successfully", completion });
    }
}

public class DisputeRequest
{
    public string? Reason { get; set; }
}
