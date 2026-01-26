using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MessagesController(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    // GET: api/messages/job/{jobId}
    [HttpGet("job/{jobId}")]
    public async Task<IActionResult> GetJobMessages(int jobId)
    {
        try
        {
            var job = await _context.Jobs.FindAsync(jobId);
            if (job == null)
                return NotFound(new { message = "Job not found" });

            var messages = await _context.Messages
                .Where(m => m.JobId == jobId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            return Ok(messages);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST: api/messages/job/{jobId}
    [HttpPost("job/{jobId}")]
    public async Task<IActionResult> SendMessage(int jobId, [FromBody] SendMessageRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.Content))
                return BadRequest(new { message = "Message content is required" });

            var job = await _context.Jobs.FindAsync(jobId);
            if (job == null)
                return NotFound(new { message = "Job not found" });

            // Get current user ID from claims (using NameIdentifier from JWT token)
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out int senderId))
                return Unauthorized(new { message = "Unable to determine sender" });

            // Determine sender type and recipient
            string senderType;
            int recipientId;

            // Check if sender is the job owner (user)
            if (job.UserId == senderId)
            {
                senderType = "User";
                recipientId = job.AssignedProId ?? 0;
            }
            // Check if sender is the assigned pro
            else if (job.AssignedProId == senderId)
            {
                senderType = "Pro";
                recipientId = job.UserId;
            }
            else
            {
                return StatusCode(403, new { message = "You are not authorized to send messages for this job" });
            }

            if (recipientId == 0)
                return BadRequest(new { message = "No recipient found for this job" });

            var message = new Message
            {
                JobId = jobId,
                SenderId = senderId,
                RecipientId = recipientId,
                SenderType = senderType,
                Content = request.Content,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetJobMessages), new { jobId }, message);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class SendMessageRequest
{
    public string? Content { get; set; }
}
