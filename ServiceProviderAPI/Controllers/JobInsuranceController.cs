using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;
using ServiceProviderAPI.Services.Abstractions;
using System.Security.Claims;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/jobs/{jobId}/insurance")]
[Authorize]
public class JobInsuranceController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IInsuranceProvider _insurance;

    public JobInsuranceController(ApplicationDbContext context, IInsuranceProvider insurance)
    {
        _context = context;
        _insurance = insurance;
    }

    // GET: api/jobs/{jobId}/insurance
    [HttpGet]
    public async Task<IActionResult> GetInsurance(int jobId)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null) return NotFound(new { message = "Job not found" });

        // Only the job owner, the assigned pro, or an admin may view insurance
        if (role != "Admin" && job.UserId != callerId && job.AssignedProId != callerId)
            return Forbid();

        var insurance = await _context.JobInsurances.FirstOrDefaultAsync(i => i.JobId == jobId);
        if (insurance == null)
            return NotFound(new { message = "No insurance record found for this job" });

        return Ok(insurance);
    }

    // GET: api/jobs/{jobId}/insurance/options — available coverage tiers for this job
    [HttpGet("options")]
    public async Task<IActionResult> GetOptions(int jobId)
    {
        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null) return NotFound(new { message = "Job not found" });

        var amount = job.EstimatedBudget ?? 0m;
        var options = await _insurance.GetCoverageOptionsAsync("general", amount);
        return Ok(new { provider = _insurance.ProviderName, amount, options });
    }

    // POST: api/jobs/{jobId}/insurance/purchase — job owner activates a coverage tier
    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase(int jobId, [FromBody] PurchaseInsuranceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CoverageType))
            return BadRequest(new { message = "A coverage type is required" });

        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null) return NotFound(new { message = "Job not found" });

        // Only the job owner may insure their own job.
        if (job.UserId != callerId)
            return Forbid();

        var existing = await _context.JobInsurances.FirstOrDefaultAsync(i => i.JobId == jobId);
        if (existing != null && existing.Status == "Active")
            return BadRequest(new { message = "This job is already insured" });

        var amount = job.EstimatedBudget ?? 0m;
        var options = await _insurance.GetCoverageOptionsAsync("general", amount);
        var chosen = options.FirstOrDefault(o =>
            string.Equals(o.CoverageType, request.CoverageType, StringComparison.OrdinalIgnoreCase));
        if (chosen == null)
            return BadRequest(new { message = "Unknown coverage type" });

        var consumer = await _context.Users.FindAsync(job.UserId);
        var policy = await _insurance.CreatePolicyAsync(
            jobId,
            chosen.CoverageType!,
            chosen.CoverageAmount,
            consumer != null ? $"{consumer.FirstName} {consumer.LastName}".Trim() : "Consumer",
            consumer?.Email ?? string.Empty);

        if (policy == null)
            return StatusCode(502, new { message = "The insurance provider could not issue a policy. Please try again." });

        var record = existing ?? new JobInsurance { JobId = jobId, CreatedAt = DateTime.UtcNow };
        record.ProviderId = _insurance.ProviderName;
        record.CoverageType = chosen.CoverageType;
        record.Amount = chosen.CoverageAmount;
        record.Status = policy.Status ?? "Active";
        record.PolicyNumber = policy.PolicyNumber;
        record.ExpiresAt = policy.ExpiresAt;
        record.Notes = $"Premium ₹{chosen.PremiumAmount:F2} — {chosen.Description}";
        record.UpdatedAt = DateTime.UtcNow;
        if (existing == null) _context.JobInsurances.Add(record);

        await _context.SaveChangesAsync();
        return Ok(new { message = "Insurance activated", insurance = record, premium = chosen.PremiumAmount });
    }

    // POST: api/jobs/{jobId}/insurance  — Admin creates/updates insurance record
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpsertInsurance(int jobId, [FromBody] UpsertInsuranceRequest request)
    {
        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null) return NotFound(new { message = "Job not found" });

        var existing = await _context.JobInsurances.FirstOrDefaultAsync(i => i.JobId == jobId);

        if (existing != null)
        {
            existing.ProviderId = request.ProviderId;
            existing.CoverageType = request.CoverageType;
            existing.Amount = request.Amount;
            existing.Status = request.Status ?? existing.Status;
            existing.PolicyNumber = request.PolicyNumber;
            existing.ExpiresAt = request.ExpiresAt;
            existing.Notes = request.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.JobInsurances.Add(new JobInsurance
            {
                JobId = jobId,
                ProviderId = request.ProviderId,
                CoverageType = request.CoverageType,
                Amount = request.Amount,
                Status = request.Status ?? "Pending",
                PolicyNumber = request.PolicyNumber,
                ExpiresAt = request.ExpiresAt,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Insurance record saved" });
    }
}

public class PurchaseInsuranceRequest
{
    public string? CoverageType { get; set; }  // "basic", "premium", "comprehensive"
}

public class UpsertInsuranceRequest
{
    public string? ProviderId { get; set; }
    public string? CoverageType { get; set; }
    public decimal Amount { get; set; }
    public string? Status { get; set; }
    public string? PolicyNumber { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Notes { get; set; }
}
