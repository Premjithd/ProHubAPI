using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.DTOs;
using ServiceProviderAPI.Models;
using System.Security.Claims;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ReviewsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // POST /api/reviews/jobs/{jobId} — authenticated user submits a review
    [HttpPost("jobs/{jobId}")]
    [Authorize]
    public async Task<ActionResult<ReviewDto>> SubmitReview(int jobId, [FromBody] CreateReviewRequest request)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(callerIdStr, out var callerId))
            return Unauthorized();

        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null)
            return NotFound(new { message = "Job not found" });

        if (job.Status != "Completed")
            return BadRequest(new { message = "Reviews can only be submitted for completed jobs" });

        if (job.UserId != callerId)
            return Forbid();

        if (job.AssignedProId == null)
            return BadRequest(new { message = "No professional assigned to this job" });

        var alreadyReviewed = await _context.Reviews.AnyAsync(r => r.JobId == jobId);
        if (alreadyReviewed)
            return Conflict(new { message = "A review has already been submitted for this job" });

        var review = new Review
        {
            JobId = jobId,
            ReviewerId = callerId,
            ProId = job.AssignedProId.Value,
            Rating = request.Rating,
            Comment = request.Comment,
            CreatedAt = DateTime.UtcNow
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var reviewer = await _context.Users.FindAsync(callerId);

        return CreatedAtAction(nameof(GetJobReview), new { jobId }, new ReviewDto
        {
            Id = review.Id,
            JobId = review.JobId,
            JobTitle = job.Title,
            ReviewerId = review.ReviewerId,
            ReviewerName = reviewer != null ? $"{reviewer.FirstName} {reviewer.LastName}" : "User",
            ProId = review.ProId,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt
        });
    }

    // GET /api/reviews/jobs/{jobId} — check if a review exists for this job (authenticated)
    [HttpGet("jobs/{jobId}")]
    [Authorize]
    public async Task<ActionResult<ReviewDto>> GetJobReview(int jobId)
    {
        var review = await _context.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Job)
            .FirstOrDefaultAsync(r => r.JobId == jobId);

        if (review == null)
            return NotFound();

        return Ok(new ReviewDto
        {
            Id = review.Id,
            JobId = review.JobId,
            JobTitle = review.Job?.Title,
            ReviewerId = review.ReviewerId,
            ReviewerName = review.Reviewer != null
                ? $"{review.Reviewer.FirstName} {review.Reviewer.LastName}"
                : "User",
            ProId = review.ProId,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt
        });
    }

    // GET /api/reviews/pros/{proId} — public list of reviews for a pro
    [HttpGet("pros/{proId}")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> GetProReviews(int proId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = _context.Reviews
            .Where(r => r.ProId == proId)
            .Include(r => r.Reviewer)
            .Include(r => r.Job);

        var total = await query.CountAsync();

        var reviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                JobId = r.JobId,
                JobTitle = r.Job != null ? r.Job.Title : null,
                ReviewerId = r.ReviewerId,
                ReviewerName = r.Reviewer != null ? $"{r.Reviewer.FirstName} {r.Reviewer.LastName}" : "User",
                ProId = r.ProId,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return Ok(new { reviews, total, page, pageSize });
    }

    // GET /api/reviews/pros/{proId}/summary — public rating summary for a pro
    [HttpGet("pros/{proId}/summary")]
    [AllowAnonymous]
    public async Task<ActionResult<ProRatingSummary>> GetProRatingSummary(int proId)
    {
        var reviews = await _context.Reviews
            .Where(r => r.ProId == proId)
            .Select(r => r.Rating)
            .ToListAsync();

        var breakdown = new int[5];
        foreach (var r in reviews)
            if (r >= 1 && r <= 5) breakdown[r - 1]++;

        return Ok(new ProRatingSummary
        {
            ProId = proId,
            AverageRating = reviews.Count > 0 ? Math.Round(reviews.Average(), 1) : 0,
            TotalReviews = reviews.Count,
            RatingBreakdown = breakdown
        });
    }

    // GET /api/reviews/stats — platform-wide stats (public, for About page)
    [HttpGet("stats")]
    [AllowAnonymous]
    public async Task<ActionResult<PlatformRatingStats>> GetPlatformStats()
    {
        var ratings = await _context.Reviews.Select(r => r.Rating).ToListAsync();

        return Ok(new PlatformRatingStats
        {
            AverageRating = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : 0,
            TotalReviews = ratings.Count
        });
    }

    // ── User reviews (pro reviews user after completed job) ───────────────────

    // POST /api/reviews/jobs/{jobId}/user — assigned pro submits a review for the customer
    [HttpPost("jobs/{jobId}/user")]
    [Authorize]
    public async Task<ActionResult<UserReviewDto>> SubmitUserReview(int jobId, [FromBody] CreateReviewRequest request)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(callerIdStr, out var callerId))
            return Unauthorized();

        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null)
            return NotFound(new { message = "Job not found" });

        if (job.Status != "Completed")
            return BadRequest(new { message = "Reviews can only be submitted for completed jobs" });

        if (job.AssignedProId != callerId)
            return Forbid();

        var alreadyReviewed = await _context.UserReviews.AnyAsync(r => r.JobId == jobId);
        if (alreadyReviewed)
            return Conflict(new { message = "A review has already been submitted for this job" });

        var review = new UserReview
        {
            JobId = jobId,
            ReviewerId = callerId,
            UserId = job.UserId,
            Rating = request.Rating,
            Comment = request.Comment,
            CreatedAt = DateTime.UtcNow
        };

        _context.UserReviews.Add(review);
        await _context.SaveChangesAsync();

        var reviewer = await _context.Pros.FindAsync(callerId);

        return CreatedAtAction(nameof(GetJobUserReview), new { jobId }, new UserReviewDto
        {
            Id = review.Id,
            JobId = review.JobId,
            JobTitle = job.Title,
            ReviewerId = review.ReviewerId,
            ReviewerName = reviewer?.ProName ?? "Professional",
            UserId = review.UserId,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt
        });
    }

    // GET /api/reviews/jobs/{jobId}/user — fetch the user review for a specific job
    [HttpGet("jobs/{jobId}/user")]
    [Authorize]
    public async Task<ActionResult<UserReviewDto>> GetJobUserReview(int jobId)
    {
        var review = await _context.UserReviews
            .Include(r => r.Reviewer)
            .Include(r => r.Job)
            .FirstOrDefaultAsync(r => r.JobId == jobId);

        if (review == null)
            return NotFound();

        return Ok(new UserReviewDto
        {
            Id = review.Id,
            JobId = review.JobId,
            JobTitle = review.Job?.Title,
            ReviewerId = review.ReviewerId,
            ReviewerName = review.Reviewer?.ProName ?? "Professional",
            UserId = review.UserId,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt
        });
    }

    // GET /api/reviews/users/{userId} — paginated reviews received by a user
    [HttpGet("users/{userId}")]
    [Authorize]
    public async Task<ActionResult<object>> GetUserReviews(int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = _context.UserReviews
            .Where(r => r.UserId == userId)
            .Include(r => r.Reviewer)
            .Include(r => r.Job);

        var total = await query.CountAsync();

        var reviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new UserReviewDto
            {
                Id = r.Id,
                JobId = r.JobId,
                JobTitle = r.Job != null ? r.Job.Title : null,
                ReviewerId = r.ReviewerId,
                ReviewerName = r.Reviewer != null ? r.Reviewer.ProName : "Professional",
                UserId = r.UserId,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return Ok(new { reviews, total, page, pageSize });
    }

    // GET /api/reviews/users/{userId}/summary — rating summary for a user
    [HttpGet("users/{userId}/summary")]
    [Authorize]
    public async Task<ActionResult<UserRatingSummary>> GetUserRatingSummary(int userId)
    {
        var ratings = await _context.UserReviews
            .Where(r => r.UserId == userId)
            .Select(r => r.Rating)
            .ToListAsync();

        var breakdown = new int[5];
        foreach (var r in ratings)
            if (r >= 1 && r <= 5) breakdown[r - 1]++;

        return Ok(new UserRatingSummary
        {
            UserId = userId,
            AverageRating = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : 0,
            TotalReviews = ratings.Count,
            RatingBreakdown = breakdown
        });
    }
}
