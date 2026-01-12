using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;
using System.Security.Claims;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<JobsController> _logger;

    public JobsController(ApplicationDbContext context, ILogger<JobsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/jobs/my-jobs
    [Authorize]
    [HttpGet("my-jobs")]
    public async Task<ActionResult<IEnumerable<Job>>> GetMyJobs()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation($"User claim found: {userIdClaim}");
            
            var userId = int.Parse(userIdClaim ?? "0");
            
            if (userId == 0)
            {
                _logger.LogWarning("User ID not found in claims");
                return Unauthorized("User not found");
            }

            var jobs = await _context.Jobs
                .Where(j => j.UserId == userId)
                .Include(j => j.User)
                .Include(j => j.AssignedPro)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            _logger.LogInformation($"Retrieved {jobs.Count} jobs for user {userId}");
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving user jobs: {ex.Message}");
            return StatusCode(500, new { message = "Error retrieving jobs", error = ex.Message });
        }
    }

    // GET: api/jobs/{id}
    [Authorize]
    [HttpGet("{id}")]
    public async Task<ActionResult<Job>> GetJob(int id)
    {
        try
        {
            var job = await _context.Jobs
                .Include(j => j.User)
                .Include(j => j.AssignedPro)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
                return NotFound("Job not found");

            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving job: {ex.Message}");
            return StatusCode(500, new { message = "Error retrieving job", error = ex.Message });
        }
    }

    // POST: api/jobs
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Job>> PostJob([FromBody] CreateJobRequest request)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            if (userId == 0)
                return Unauthorized("User not found");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var job = new Job
            {
                UserId = userId,
                Title = request.Title,
                Category = request.Category,
                Description = request.Description,
                Location = request.Location,
                Budget = request.Budget,
                Timeline = request.Timeline,
                Attachments = request.Attachments,
                Status = "Open",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Jobs.Add(job);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetJob), new { id = job.Id }, job);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating job: {ex.Message}");
            return StatusCode(500, new { message = "Error creating job", error = ex.Message });
        }
    }

    // PUT: api/jobs/{id}
    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult<Job>> UpdateJob(int id, [FromBody] UpdateJobRequest request)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var job = await _context.Jobs.FindAsync(id);

            if (job == null)
                return NotFound("Job not found");

            if (job.UserId != userId)
                return Forbid();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            job.Title = request.Title ?? job.Title;
            job.Category = request.Category ?? job.Category;
            job.Description = request.Description ?? job.Description;
            job.Location = request.Location ?? job.Location;
            job.Budget = request.Budget ?? job.Budget;
            job.Timeline = request.Timeline ?? job.Timeline;
            job.Status = request.Status ?? job.Status;
            job.UpdatedAt = DateTime.UtcNow;

            _context.Jobs.Update(job);
            await _context.SaveChangesAsync();

            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating job: {ex.Message}");
            return StatusCode(500, new { message = "Error updating job", error = ex.Message });
        }
    }

    // DELETE: api/jobs/{id}
    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteJob(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var job = await _context.Jobs.FindAsync(id);

            if (job == null)
                return NotFound("Job not found");

            if (job.UserId != userId)
                return Forbid();

            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Job deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting job: {ex.Message}");
            return StatusCode(500, new { message = "Error deleting job", error = ex.Message });
        }
    }

    // GET: api/jobs/category/{category}
    [AllowAnonymous]
    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<Job>>> GetJobsByCategory(string category)
    {
        try
        {
            var jobs = await _context.Jobs
                .Where(j => j.Category == category && j.Status == "Open")
                .Include(j => j.User)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving jobs by category: {ex.Message}");
            return StatusCode(500, new { message = "Error retrieving jobs", error = ex.Message });
        }
    }
}

public class CreateJobRequest
{
    public string? Title { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Budget { get; set; }
    public string? Timeline { get; set; }
    public string? Attachments { get; set; }
}

public class UpdateJobRequest
{
    public string? Title { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Budget { get; set; }
    public string? Timeline { get; set; }
    public string? Status { get; set; }
}
