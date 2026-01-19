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
                .Include(j => j.Category)
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

    // GET: api/jobs/assigned - Get jobs assigned to the current Pro
    [Authorize]
    [HttpGet("assigned")]
    public async Task<ActionResult<IEnumerable<Job>>> GetAssignedJobs()
    {
        try
        {
            var proIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation($"Pro claim found: {proIdClaim}");
            
            var proId = int.Parse(proIdClaim ?? "0");
            
            if (proId == 0)
            {
                _logger.LogWarning("Pro ID not found in claims");
                return Unauthorized("Pro user not found");
            }

            var jobs = await _context.Jobs
                .Where(j => j.AssignedProId == proId)
                .Include(j => j.User)
                .Include(j => j.Category)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            _logger.LogInformation($"Retrieved {jobs.Count} assigned jobs for pro {proId}");
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving assigned jobs: {ex.Message}");
            return StatusCode(500, new { message = "Error retrieving assigned jobs", error = ex.Message });
        }
    }

    // GET: api/jobs/available
    [Authorize]
    [HttpGet("available")]
    public async Task<ActionResult<IEnumerable<Job>>> GetAvailableJobs()
    {
        try
        {
            var jobs = await _context.Jobs
                .Where(j => j.Status == "Open" && (j.AssignedProId == null || j.AssignedProId == 0))
                .Include(j => j.User)
                .Include(j => j.AssignedPro)
                .Include(j => j.Category)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving available jobs: {ex.Message}");
            return StatusCode(500, new { message = "Error retrieving available jobs", error = ex.Message });
        }
    }

    // GET: api/jobs/{id}/bids
    [Authorize]
    [HttpGet("{id}/bids")]
    public async Task<ActionResult<IEnumerable<JobBid>>> GetJobBids(int id)
    {
        try
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job == null)
                return NotFound("Job not found");

            // Get bids for this job
            var bids = await _context.JobBids
                .Where(jb => jb.JobId == id)
                .Include(jb => jb.Pro)
                .OrderByDescending(jb => jb.CreatedAt)
                .ToListAsync();

            return Ok(bids);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving job bids: {ex.Message}");
            return StatusCode(500, new { message = "Error retrieving bids", error = ex.Message });
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
                .Include(j => j.Category)
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
            _logger.LogInformation("📝 POST /api/jobs - Attempting to create job");
            
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation($"User ID from claim: {userIdClaim}");
            
            var userId = int.Parse(userIdClaim ?? "0");
            
            if (userId == 0)
            {
                _logger.LogWarning("❌ User ID not found in claims");
                return Unauthorized(new { message = "User not found in authentication" });
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning($"❌ Invalid model state: {string.Join("; ", ModelState.Values.SelectMany(v => v.Errors))}");
                return BadRequest(ModelState);
            }

            _logger.LogInformation($"📋 Creating job with title: {request.Title}, categoryId: {request.CategoryId}");

            var job = new Job
            {
                UserId = userId,
                Title = request.Title,
                CategoryId = request.CategoryId,
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
            
            _logger.LogInformation($"✅ Job created successfully with ID: {job.Id}");

            return CreatedAtAction(nameof(GetJob), new { id = job.Id }, job);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError($"❌ Database error creating job: {dbEx.Message}, Inner: {dbEx.InnerException?.Message}");
            return StatusCode(500, new { message = "Database error creating job", error = dbEx.InnerException?.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error creating job: {ex.Message}, Stack: {ex.StackTrace}");
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
            if (request.CategoryId.HasValue)
                job.CategoryId = request.CategoryId.Value;
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

    // PUT: api/jobs/{id}/complete - Mark a job as completed
    [Authorize]
    [HttpPut("{id}/complete")]
    public async Task<ActionResult<Job>> MarkJobCompleted(int id)
    {
        try
        {
            var proIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var proId = int.Parse(proIdClaim ?? "0");

            if (proId == 0)
                return Unauthorized("Pro user not found");

            var job = await _context.Jobs.FindAsync(id);

            if (job == null)
                return NotFound("Job not found");

            // Verify that the job is assigned to this Pro
            if (job.AssignedProId != proId)
                return Forbid("This job is not assigned to you");

            job.Status = "Completed";
            _context.Jobs.Update(job);
            await _context.SaveChangesAsync();

            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error marking job as completed: {ex.Message}");
            return StatusCode(500, new { message = "Error marking job as completed", error = ex.Message });
        }
    }

    // POST: api/jobs/{id}/bid
    [Authorize]
    [HttpPost("{id}/bid")]
    public async Task<ActionResult<JobBid>> SubmitJobBid(int id, [FromBody] CreateJobBidRequest request)
    {
        try
        {
            var proIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var proId = int.Parse(proIdClaim ?? "0");

            if (proId == 0)
                return Unauthorized("Pro user not found");

            // Check if job exists
            var job = await _context.Jobs.FindAsync(id);
            if (job == null)
                return NotFound("Job not found");

            // Check if Pro has already bid on this job
            var existingBid = await _context.JobBids
                .FirstOrDefaultAsync(jb => jb.JobId == id && jb.ProId == proId);

            if (existingBid != null)
                return BadRequest("You have already submitted a bid for this job");

            var jobBid = new JobBid
            {
                JobId = id,
                ProId = proId,
                BidMessage = request.BidMessage,
                BidAmount = request.BidAmount,
                Status = "Pending"
            };

            _context.JobBids.Add(jobBid);

            // Set IsBid to true on the job when a bid is received
            job.IsBid = true;
            _context.Jobs.Update(job);

            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetJob), new { id = job.Id }, jobBid);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error submitting job bid: {ex.Message}");
            return StatusCode(500, new { message = "Error submitting bid", error = ex.Message });
        }
    }

    // POST: api/jobs/{jobId}/bids/{bidId}/accept
    [Authorize]
    [HttpPost("{jobId}/bids/{bidId}/accept")]
    public async Task<ActionResult<JobBid>> AcceptBid(int jobId, int bidId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = int.Parse(userIdClaim ?? "0");

            // Check if job exists and belongs to user
            var job = await _context.Jobs.FindAsync(jobId);
            if (job == null)
                return NotFound("Job not found");

            if (job.UserId != userId)
                return Forbid("You can only manage bids for your own jobs");

            // Find the bid
            var bid = await _context.JobBids.FindAsync(bidId);
            if (bid == null)
                return NotFound("Bid not found");

            if (bid.JobId != jobId)
                return BadRequest("Bid does not belong to this job");

            // Update bid status
            bid.Status = "Accepted";
            _context.JobBids.Update(bid);

            // Update job to assign to this pro
            job.AssignedProId = bid.ProId;
            job.Status = "In Progress";
            _context.Jobs.Update(job);

            // Reject all other bids for this job
            var otherBids = await _context.JobBids
                .Where(jb => jb.JobId == jobId && jb.Id != bidId && jb.Status == "Pending")
                .ToListAsync();

            foreach (var otherBid in otherBids)
            {
                otherBid.Status = "Rejected";
            }
            _context.JobBids.UpdateRange(otherBids);

            await _context.SaveChangesAsync();

            return Ok(bid);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error accepting bid: {ex.Message}");
            return StatusCode(500, new { message = "Error accepting bid", error = ex.Message });
        }
    }

    // POST: api/jobs/{jobId}/bids/{bidId}/reject
    [Authorize]
    [HttpPost("{jobId}/bids/{bidId}/reject")]
    public async Task<ActionResult<JobBid>> RejectBid(int jobId, int bidId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = int.Parse(userIdClaim ?? "0");

            // Check if job exists and belongs to user
            var job = await _context.Jobs.FindAsync(jobId);
            if (job == null)
                return NotFound("Job not found");

            if (job.UserId != userId)
                return Forbid("You can only manage bids for your own jobs");

            // Find the bid
            var bid = await _context.JobBids.FindAsync(bidId);
            if (bid == null)
                return NotFound("Bid not found");

            if (bid.JobId != jobId)
                return BadRequest("Bid does not belong to this job");

            // Update bid status
            bid.Status = "Rejected";
            _context.JobBids.Update(bid);

            await _context.SaveChangesAsync();

            return Ok(bid);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error rejecting bid: {ex.Message}");
            return StatusCode(500, new { message = "Error rejecting bid", error = ex.Message });
        }
    }

    // GET: api/jobs/category/{categoryId}
    [AllowAnonymous]
    [HttpGet("category/{categoryId}")]
    public async Task<ActionResult<IEnumerable<Job>>> GetJobsByCategory(int categoryId)
    {
        try
        {
            var jobs = await _context.Jobs
                .Where(j => j.CategoryId == categoryId && j.Status == "Open")
                .Include(j => j.User)
                .Include(j => j.Category)
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
    public int? CategoryId { get; set; }  // Foreign key to ServiceCategory
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Budget { get; set; }
    public string? Timeline { get; set; }
    public string? Attachments { get; set; }
}

public class UpdateJobRequest
{
    public string? Title { get; set; }
    public int? CategoryId { get; set; }  // Foreign key to ServiceCategory
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Budget { get; set; }
    public string? Timeline { get; set; }
    public string? Status { get; set; }
}
public class CreateJobBidRequest
{
    public string? BidMessage { get; set; }
    public decimal? BidAmount { get; set; }
}