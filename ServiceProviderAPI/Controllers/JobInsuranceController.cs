using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;
using System.Security.Claims;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/jobs/{jobId}/insurance")]
[Authorize]
public class JobInsuranceController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public JobInsuranceController(ApplicationDbContext context)
    {
        _context = context;
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
