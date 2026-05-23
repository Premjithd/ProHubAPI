using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.DTOs;
using ServiceProviderAPI.Hubs;
using ServiceProviderAPI.Models;
using System.Security.Claims;
using System.Linq;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<JobsController> _logger;
    private readonly IHubContext<NotificationHub> _hubContext;

    public JobsController(ApplicationDbContext context, ILogger<JobsController> logger, IHubContext<NotificationHub> hubContext)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
    }

    // GET: api/jobs/my-jobs?page=1&pageSize=20&status=Open
    [Authorize]
    [HttpGet("my-jobs")]
    public async Task<ActionResult<PagedResult<Job>>> GetMyJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
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

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Jobs
                .Where(j => j.UserId == userId)
                .Include(j => j.User)
                .Include(j => j.AssignedPro)
                .Include(j => j.Category)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(j => j.Status == status);

            query = query.OrderByDescending(j => j.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            _logger.LogInformation($"Retrieved {items.Count}/{total} jobs for user {userId} (page {page})");
            return Ok(new PagedResult<Job> { Items = items, Total = total, Page = page, PageSize = pageSize });
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

    // GET: api/jobs/available?page=1&pageSize=20&categoryId=&search=&filterRadiusKm=25
    [Authorize]
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] int? filterRadiusKm = null)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            // Load the calling pro's location
            var proIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(proIdStr, out int proId);
            var pro = proId > 0 ? await _context.Pros.FindAsync(proId) : null;

            bool hasProLocation = pro?.Latitude != null && pro?.Longitude != null;
            bool applyProximity = hasProLocation && filterRadiusKm.HasValue;
            double radiusKm = filterRadiusKm.HasValue ? (double)filterRadiusKm.Value : 0;

            // DB query — filtered by category/search, full set (no Skip/Take) so Haversine can run in memory
            var query = _context.Jobs
                .Where(j => j.Status == "Open" && (j.AssignedProId == null || j.AssignedProId == 0))
                .Include(j => j.User)
                .Include(j => j.Category)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(j => j.CategoryId == categoryId.Value);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(j =>
                    j.Title.Contains(search) ||
                    (j.Description != null && j.Description.Contains(search)));

            var allJobs = await query.ToListAsync();

            // Compute distance for every job (null when either side has no coordinates)
            var withDistance = allJobs.Select(j =>
            {
                double? dist = null;
                if (hasProLocation && j.Latitude.HasValue && j.Longitude.HasValue)
                    dist = Math.Round(HaversineKm(pro!.Latitude!.Value, pro!.Longitude!.Value, j.Latitude.Value, j.Longitude.Value), 1);
                return (Job: j, DistanceKm: dist);
            }).ToList();

            // Proximity filter: keep jobs within radius OR jobs with no coordinates (can't exclude them)
            if (applyProximity)
                withDistance = withDistance
                    .Where(x => x.DistanceKm == null || x.DistanceKm <= radiusKm)
                    .ToList();

            // Sort: known distances first (ascending), then jobs without coordinates
            withDistance = withDistance
                .OrderBy(x => x.DistanceKm ?? double.MaxValue)
                .ThenByDescending(x => x.Job.CreatedAt)
                .ToList();

            var total = withDistance.Count;
            var paged = withDistance
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Job.Id,
                    x.Job.UserId,
                    x.Job.Title,
                    x.Job.CategoryId,
                    Category = x.Job.Category == null ? null : new { x.Job.Category.Id, x.Job.Category.Name, x.Job.Category.Icon },
                    x.Job.Description,
                    x.Job.Location,
                    x.Job.ServiceAddressCity,
                    x.Job.ServiceAddressState,
                    x.Job.ServiceAddressCountry,
                    x.Job.ServiceAddressPIN,
                    x.Job.Budget,
                    x.Job.EstimatedBudget,
                    x.Job.Timeline,
                    x.Job.Status,
                    x.Job.IsBid,
                    x.Job.AssignedProId,
                    x.Job.Latitude,
                    x.Job.Longitude,
                    x.Job.JobPhases,
                    x.Job.Attachments,
                    x.Job.CreatedAt,
                    x.Job.UpdatedAt,
                    User = x.Job.User == null ? null : new { x.Job.User.Id, x.Job.User.FirstName, x.Job.User.LastName, x.Job.User.Email },
                    DistanceKm = x.DistanceKm
                })
                .ToList();

            return Ok(new
            {
                items = paged,
                total,
                page,
                pageSize,
                proximityFilterApplied = applyProximity,
                proLocationSet = hasProLocation,
                radiusKm = applyProximity ? (double?)radiusKm : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving available jobs: {ex.Message}");
            return StatusCode(500, new { message = "Error retrieving available jobs", error = ex.Message });
        }
    }

    // Haversine formula — returns distance in kilometres between two lat/lng points
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
    }

    // GET: api/admin/users/{userId}/jobs - Get jobs posted by a specific user
    [Authorize(Roles = "Admin")]
    [HttpGet("users/{userId}/jobs")]
    public async Task<ActionResult<IEnumerable<Job>>> GetUserJobs(int userId)
    {
        try
        {
            var jobs = await _context.Jobs
                .Where(j => j.UserId == userId)
                .Include(j => j.User)
                .Include(j => j.Category)
                .Include(j => j.AssignedPro)
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => new
                {
                    j.Id,
                    j.UserId,
                    UserName = j.User != null ? (j.User.FirstName + " " + j.User.LastName) : "Unknown",
                    j.Title,
                    j.Description,
                    j.Location,
                    j.Budget,
                    j.Timeline,
                    j.Status,
                    j.IsBid,
                    j.AssignedProId,
                    AssignedProBusinessName = j.AssignedPro != null ? (j.AssignedPro.BusinessName ?? j.AssignedPro.ProName) : null,
                    j.JobPhases,
                    j.Attachments,
                    j.CreatedAt,
                    j.UpdatedAt,
                    CategoryId = j.CategoryId,
                    Category = j.Category != null ? new { j.Category.Id, j.Category.Name } : null
                })
                .ToListAsync();

            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving user jobs: {ex.Message}");
            return StatusCode(500, new { message = "Error retrieving user jobs", error = ex.Message });
        }
    }

    // GET: api/admin/pros/{proId}/jobs - Get jobs assigned to a specific pro
    [Authorize(Roles = "Admin")]
    [HttpGet("pros/{proId}/jobs")]
    public async Task<ActionResult<IEnumerable<Job>>> GetProJobs(int proId)
    {
        try
        {
            var jobs = await _context.Jobs
                .Where(j => j.AssignedProId == proId)
                .Include(j => j.User)
                .Include(j => j.Category)
                .Include(j => j.AssignedPro)
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => new
                {
                    j.Id,
                    j.UserId,
                    UserName = j.User != null ? (j.User.FirstName + " " + j.User.LastName) : "Unknown",
                    j.Title,
                    j.Description,
                    j.Location,
                    j.Budget,
                    j.Timeline,
                    j.Status,
                    j.IsBid,
                    j.AssignedProId,
                    AssignedProBusinessName = j.AssignedPro != null ? (j.AssignedPro.BusinessName ?? j.AssignedPro.ProName) : null,
                    j.JobPhases,
                    j.Attachments,
                    j.CreatedAt,
                    j.UpdatedAt,
                    CategoryId = j.CategoryId,
                    Category = j.Category != null ? new { j.Category.Id, j.Category.Name } : null
                })
                .ToListAsync();

            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving pro jobs: {ex.Message}");
            return StatusCode(500, new { message = "Error retrieving pro jobs", error = ex.Message });
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

            // Notify pros whose services match this job's category
            if (job.CategoryId.HasValue)
            {
                var matchingProIds = await _context.Services
                    .Where(s => s.ServiceCategoryId == job.CategoryId)
                    .Select(s => s.ProId)
                    .Distinct()
                    .ToListAsync();

                if (matchingProIds.Count > 0)
                {
                    var notifications = matchingProIds.Select(proId => new JobNotification
                    {
                        JobId = job.Id,
                        ProId = proId,
                        NotificationType = "JobPosted",
                        Message = $"New job matching your services: {job.Title}",
                        DeliveryStatus = "Sent",
                        DeliveredAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    });
                    _context.JobNotifications.AddRange(notifications);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"📢 Notified {matchingProIds.Count} pros about job {job.Id}");

                    // Push real-time notification via SignalR
                    var pushTasks = matchingProIds.Select(proId =>
                        _hubContext.Clients.Group($"pro-{proId}").SendAsync("NewNotification"));
                    await Task.WhenAll(pushTasks);
                }
            }

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

    // PUT: api/jobs/{id}/complete - Pro submits completion (awaits consumer verification)
    [Authorize]
    [HttpPut("{id}/complete")]
    public async Task<ActionResult<Job>> MarkJobCompleted(int id, [FromBody] SubmitCompletionRequest? request)
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

            if (job.AssignedProId != proId)
                return Forbid("This job is not assigned to you");

            if (job.Status == "Completion Submitted" || job.Status == "Completed")
                return BadRequest(new { message = "Completion already submitted for this job" });

            // Remove any prior completion record before creating new one
            var existing = await _context.JobCompletions.FirstOrDefaultAsync(c => c.JobId == id);
            if (existing != null)
                _context.JobCompletions.Remove(existing);

            var completion = new JobCompletion
            {
                JobId = id,
                CompletionNotes = request?.CompletionNotes,
                Status = "Submitted",
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            _context.JobCompletions.Add(completion);
            job.Status = "Completion Submitted";
            job.UpdatedAt = DateTime.UtcNow;
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
                // Phase 1A fields
                CommenceDate = request.CommenceDate,
                ExpectedDurationDays = request.ExpectedDurationDays,
                MaterialsDescription = request.MaterialsDescription,
                ExpiresAt = request.ExpiresAt ?? DateTime.UtcNow.AddDays(30), // Default to 30 days if not provided
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

    // POST: api/jobs/{id}/cancel
    [Authorize]
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelJob(int id, [FromBody] CancelJobRequest? request)
    {
        try
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);

            var job = await _context.Jobs.FindAsync(id);
            if (job == null)
                return NotFound("Job not found");

            if (job.UserId != userId)
                return Forbid("You can only cancel your own jobs");

            var nonCancellableStatuses = new[] { "Completed", "Cancelled", "In Progress" };
            if (nonCancellableStatuses.Contains(job.Status))
                return BadRequest($"Cannot cancel a job with status '{job.Status}'.");

            job.Status = "Cancelled";
            job.UpdatedAt = DateTime.UtcNow;
            _context.Jobs.Update(job);

            // Mark all pending bids as withdrawn
            var pendingBids = await _context.JobBids
                .Where(b => b.JobId == id && b.Status == "Pending")
                .ToListAsync();
            foreach (var bid in pendingBids)
                bid.Status = "Withdrawn";

            await _context.SaveChangesAsync();

            return Ok(new { message = "Job cancelled successfully", jobId = id, status = "Cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error cancelling job: {ex.Message}");
            return StatusCode(500, new { message = "Error cancelling job", error = ex.Message });
        }
    }

    // POST: api/jobs/{jobId}/bids/{bidId}/withdraw
    [Authorize]
    [HttpPost("{jobId}/bids/{bidId}/withdraw")]
    public async Task<IActionResult> WithdrawBid(int jobId, int bidId)
    {
        try
        {
            var proIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(proIdStr, out int proId);

            var bid = await _context.JobBids.FindAsync(bidId);
            if (bid == null)
                return NotFound("Bid not found");

            if (bid.JobId != jobId)
                return BadRequest("Bid does not belong to this job");

            if (bid.ProId != proId)
                return Forbid("You can only withdraw your own bids");

            if (bid.Status != "Pending")
                return BadRequest($"Cannot withdraw a bid with status '{bid.Status}'. Only pending bids can be withdrawn.");

            bid.Status = "Withdrawn";
            _context.JobBids.Update(bid);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Bid withdrawn successfully", bidId = bid.Id, status = bid.Status });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error withdrawing bid: {ex.Message}");
            return StatusCode(500, new { message = "Error withdrawing bid", error = ex.Message });
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

    // PUT: api/jobs/{jobId}/phases - Update all phases for a job
    [Authorize]
    [HttpPut("{jobId}/phases")]
    public async Task<ActionResult> UpdateJobPhases(int jobId, [FromBody] UpdateJobPhasesRequest request)
    {
        try
        {
            var job = await _context.Jobs.FindAsync(jobId);
            if (job == null)
            {
                return NotFound(new { message = "Job not found" });
            }

            // Verify user has permission to update this job
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = int.Parse(userIdClaim ?? "0");

            if (job.UserId != userId && job.AssignedProId != userId)
            {
                return Forbid();
            }

            // Update the job phases as JSON
            if (request.JobPhases != null)
            {
                job.JobPhases = System.Text.Json.JsonSerializer.Serialize(request.JobPhases);
            }

            job.UpdatedAt = DateTime.UtcNow;
            _context.Jobs.Update(job);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Updated phases for job {jobId}");
            return Ok(new { message = "Phases updated successfully", job });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating job phases: {ex.Message}");
            return StatusCode(500, new { message = "Error updating phases", error = ex.Message });
        }
    }

    // POST: api/jobs/{jobId}/phases/{phaseId}/toggle - Toggle phase completion
    [Authorize]
    [HttpPost("{jobId}/phases/{phaseId}/toggle")]
    public async Task<ActionResult> TogglePhaseCompletion(int jobId, string phaseId)
    {
        try
        {
            var job = await _context.Jobs.FindAsync(jobId);
            if (job == null)
            {
                return NotFound(new { message = "Job not found" });
            }

            // Verify user has permission to update this job
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userId = int.Parse(userIdClaim ?? "0");

            if (job.UserId != userId && job.AssignedProId != userId)
            {
                return Forbid();
            }

            // Parse phases from JSON
            if (string.IsNullOrEmpty(job.JobPhases))
            {
                return BadRequest(new { message = "No phases found for this job" });
            }

            var phases = System.Text.Json.JsonSerializer.Deserialize<List<JobPhaseDto>>(job.JobPhases);
            if (phases == null)
            {
                return BadRequest(new { message = "Invalid phases data" });
            }

            var phase = phases.FirstOrDefault(p => p.Id == phaseId);
            if (phase == null)
            {
                return NotFound(new { message = "Phase not found" });
            }

            // Toggle completion
            phase.IsCompleted = !phase.IsCompleted;
            phase.CompletedAt = phase.IsCompleted ? DateTime.UtcNow.ToString("O") : null;

            // Update the job
            job.JobPhases = System.Text.Json.JsonSerializer.Serialize(phases);
            job.UpdatedAt = DateTime.UtcNow;
            _context.Jobs.Update(job);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Toggled phase {phaseId} for job {jobId}");
            return Ok(new { message = "Phase toggled successfully", phase });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error toggling phase: {ex.Message}");
            return StatusCode(500, new { message = "Error toggling phase", error = ex.Message });
        }
    }
}


public class SubmitCompletionRequest
{
    public string? CompletionNotes { get; set; }
}

public class UpdateJobPhasesRequest
{
    public List<JobPhaseDto>? JobPhases { get; set; }
}

public class JobPhaseDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public string? CompletedAt { get; set; }
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
