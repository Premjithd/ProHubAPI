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
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<MessagesController> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    // GET: api/messages or api/messages?userId1=X&userType1=Type&userId2=Y&userType2=Type
    [HttpGet]
    public async Task<IActionResult> GetAllMessages(
        [FromQuery] int? userId1, 
        [FromQuery] string? userType1,
        [FromQuery] int? userId2,
        [FromQuery] string? userType2)
    {
        try
        {
            // If specific user pair parameters are provided, get messages between them
            if (userId1.HasValue && userId2.HasValue && !string.IsNullOrEmpty(userType1) && !string.IsNullOrEmpty(userType2))
            {
                // Get the MessageIndex for this conversation pair
                var messageIndex = await _context.MessageIndexes
                    .FirstOrDefaultAsync(mi => 
                        (mi.UserId1 == userId1 && mi.UserType1 == userType1 && 
                         mi.UserId2 == userId2 && mi.UserType2 == userType2) ||
                        (mi.UserId1 == userId2 && mi.UserType1 == userType2 && 
                         mi.UserId2 == userId1 && mi.UserType2 == userType1));

                if (messageIndex == null)
                {
                    // No messages found for this conversation pair
                    return Ok(new object[0]);
                }

                // Get all messages for this MessageIndex (exclude navigation properties to avoid circular references)
                var messages = await _context.Messages
                    .Where(m => m.MessageIndexId == messageIndex.Id)
                    .OrderBy(m => m.SentAt)
                    .Select(m => new
                    {
                        m.Id,
                        m.SenderId,
                        m.RecipientId,
                        m.SenderType,
                        m.Content,
                        m.SentAt,
                        m.IsRead,
                        m.ReadAt,
                        m.MessageIndexId
                    })
                    .ToListAsync();

                return Ok(messages);
            }

            // // Otherwise, get all messages for the current authenticated user from claims
            // var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // if (!int.TryParse(userIdClaim, out int currentUserId))
            //     return Unauthorized(new { message = "Unable to determine user" });

            // // Get all messages where current user is either sender or recipient
            // var allMessages = await _context.Messages
            //     .Where(m => m.SenderId == currentUserId || m.RecipientId == currentUserId)
            //     .OrderByDescending(m => m.SentAt)
            //     .ToListAsync();

            // return Ok(allMessages);
            return Ok(new object[0]);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET: api/messages/conversations?userId=1&userType=User|Pro
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversationPartners([FromQuery] int? userId, [FromQuery] string userType)
    {
        try
        {
            // If userId is not provided, get it from claims
            int actualUserId;
            if (userId.HasValue && userId.Value > 0)
            {
                actualUserId = userId.Value;
            }
            else
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out actualUserId))
                    return Unauthorized(new { message = "Unable to determine user" });
            }

            // Validate userType parameter
            if (string.IsNullOrEmpty(userType) || (userType != "User" && userType != "Pro"))
                return BadRequest(new { message = "UserType must be 'User' or 'Pro'" });

            // Get all MessageIndex entries where current user is involved
            var messageIndexes = await _context.MessageIndexes
                .Where(mi => (mi.UserId1 == actualUserId && mi.UserType1 == userType) || 
                             (mi.UserId2 == actualUserId && mi.UserType2 == userType))
                .OrderByDescending(mi => mi.LastMessageAt)
                .ToListAsync();

            // Apply distinct in memory to eliminate duplicate partner combinations
            messageIndexes = messageIndexes
                .DistinctBy(mi => new 
                { 
                    PartnerId = (mi.UserId1 == actualUserId && mi.UserType1 == userType) ? mi.UserId2 : mi.UserId1,
                    PartnerType = (mi.UserId1 == actualUserId && mi.UserType1 == userType) ? mi.UserType2 : mi.UserType1
                })
                .ToList();

            var conversationPartners = new List<object>();

            foreach (var messageIndex in messageIndexes)
            {
                // Determine the partner ID and type by checking which side the current user is on
                int partnerId;
                string partnerType;

                // Check if current user is on the first position
                if (messageIndex.UserId1 == actualUserId && messageIndex.UserType1 == userType)
                {
                    partnerId = messageIndex.UserId2;
                    partnerType = messageIndex.UserType2;
                }
                // Check if current user is on the second position
                else if (messageIndex.UserId2 == actualUserId && messageIndex.UserType2 == userType)
                {
                    partnerId = messageIndex.UserId1;
                    partnerType = messageIndex.UserType1;
                }
                else
                {
                    // This shouldn't happen if the WHERE clause is correct, skip this entry
                    continue;
                }

                // Get the latest message with this partner
                var latestMessage = await _context.Messages
                    .Where(m => m.MessageIndexId == messageIndex.Id)
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                // Get partner info based on type
                string partnerName;
                string partnerEmail;

                if (partnerType == "User")
                {
                    var partnerUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == partnerId);
                    if (partnerUser != null)
                    {
                        partnerName = partnerUser.FirstName + " " + partnerUser.LastName;
                        partnerEmail = partnerUser.Email;
                    }
                    else
                    {
                        partnerName = "User";
                        partnerEmail = "";
                    }
                }
                else
                {
                    var partnerPro = await _context.Pros.FirstOrDefaultAsync(p => p.Id == partnerId);
                    if (partnerPro != null)
                    {
                        partnerName = partnerPro.BusinessName ?? partnerPro.ProName ?? "Professional";
                        partnerEmail = partnerPro.Email;
                    }
                    else
                    {
                        partnerName = "Professional";
                        partnerEmail = "";
                    }
                }

                // Count unread messages from this partner
                var unreadCount = await _context.Messages
                    .Where(m => m.MessageIndexId == messageIndex.Id && 
                                m.SenderId == partnerId && 
                                m.RecipientId == actualUserId && 
                                !m.IsRead)
                    .CountAsync();

                conversationPartners.Add(new
                {
                    userId = partnerId,
                    userName = partnerName,
                    userEmail = partnerEmail,
                    userType = partnerType,
                    lastMessage = latestMessage?.Content ?? "",
                    lastMessageTime = messageIndex.LastMessageAt ?? messageIndex.InitiatedAt,
                    unreadCount = unreadCount
                });
            }

            return Ok(conversationPartners);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST: api/messages/send (Direct message between any users)
    [HttpPost("send")]
    public async Task<IActionResult> SendDirectMessage([FromBody] SendDirectMessageRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.Content))
                return BadRequest(new { message = "Message content is required" });

            if (request.RecipientId <= 0 || string.IsNullOrWhiteSpace(request.SenderType))
                return BadRequest(new { message = "Valid recipient ID and sender type are required" });

            // Get current user ID from claims
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out int senderId))
                return Unauthorized(new { message = "Unable to determine sender" });

            string senderType = request.SenderType;
            int recipientId = request.RecipientId;

            // Determine recipient type based on the recipient ID
            string recipientType = "User"; // Default to User, could be Pro
            // var proRecipient = await _context.Pros.FirstOrDefaultAsync(p => p.Id == recipientId);
            // if (proRecipient != null)
            //     recipientType = "Pro";
            if(senderType == "User")
                {recipientType = "Pro";}
            else
                {recipientType = "User";}

            // Get or create MessageIndex entry
            var messageIndex = await GetOrCreateMessageIndex(senderId, senderType, recipientId, recipientType);

            var message = new Message
            {
                SenderId = senderId,
                RecipientId = recipientId,
                SenderType = senderType,
                Content = request.Content,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                MessageIndexId = messageIndex.Id
            };

            _context.Messages.Add(message);
            
            // Update LastMessageAt in MessageIndex
            messageIndex.LastMessageAt = DateTime.UtcNow;
            _context.MessageIndexes.Update(messageIndex);
            
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAllMessages), message);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET: api/messages/user/{userId}
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetMessagesWithUser(int userId)
    {
        try
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out int currentUserId))
                return Unauthorized(new { message = "Unable to determine user" });

            // Get all messages between current user and specified user
            var messages = await _context.Messages
                .Where(m => (m.SenderId == currentUserId && m.RecipientId == userId) ||
                           (m.SenderId == userId && m.RecipientId == currentUserId))
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            return Ok(messages);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT: api/messages/{messageId}/read
    [HttpPut("{messageId}/read")]
    public async Task<IActionResult> MarkMessageAsRead(int messageId)
    {
        try
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message == null)
                return NotFound(new { message = "Message not found" });

            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
            _context.Messages.Update(message);
            await _context.SaveChangesAsync();

            return Ok(message);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST: api/messages/bid/{bidId}
    [HttpPost("bid/{bidId}")]
    public async Task<IActionResult> SendMessageToBid(int bidId, [FromBody] SendMessageRequest request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { message = "Request body is required" });

            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { message = "Message content is required" });

            var bid = await _context.JobBids.Include(b => b.Pro).FirstOrDefaultAsync(b => b.Id == bidId);
            if (bid == null)
                return NotFound(new { message = "Bid not found" });

            var job = await _context.Jobs.FindAsync(bid.JobId);
            if (job == null)
                return NotFound(new { message = "Job not found" });

            // Get current user ID from claims
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out int senderId))
                return Unauthorized(new { message = "Unable to determine sender" });

            // Verify that the sender is the job owner
            if (job.UserId != senderId)
                return StatusCode(403, new { message = "You are not authorized to send messages for this job" });

            // Get or create MessageIndex entry
            var messageIndex = await GetOrCreateMessageIndex(senderId, "User", bid.ProId, "Pro");

            var message = new Message
            {
                SenderId = senderId,
                RecipientId = bid.ProId,
                SenderType = "User",
                Content = request.Content,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                MessageIndexId = messageIndex.Id
            };

            _context.Messages.Add(message);
            
            // Update LastMessageAt in MessageIndex
            messageIndex.LastMessageAt = DateTime.UtcNow;
            _context.MessageIndexes.Update(messageIndex);

            // Mark IsMessageExchange as true on the bid
            bid.IsMessageExchange = true;
            _context.JobBids.Update(bid);
            
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAllMessages), message);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.InnerException?.Message });
        }
    }

    // GET: api/messages/job/{jobId}
    [HttpGet("job/{jobId}")]
    public async Task<IActionResult> GetMessagesByJob(int jobId)
    {
        try
        {
            _logger.LogInformation($"Getting messages for job {jobId}");

            // Verify job exists
            var job = await _context.Jobs.FindAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning($"Job {jobId} not found");
                return NotFound(new { message = "Job not found" });
            }

            // For now, return empty array - messages will be populated once JobId is properly tracked
            // This prevents 500 errors while the feature is being implemented
            var emptyMessages = new object[0];
            return Ok(emptyMessages);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting messages for job {jobId}: {ex.Message}\n{ex.StackTrace}");
            // Return empty array instead of error to prevent blocking the UI
            return Ok(new object[0]);
        }
    }

    // Helper method to get or create MessageIndex entry
    private async Task<MessageIndex> GetOrCreateMessageIndex(int userId1, string userType1, int userId2, string userType2)
    {
        // Normalize the user pair order (smaller ID first) to ensure uniqueness
        if (userId1 > userId2)
        {
            (userId1, userId2) = (userId2, userId1);
            (userType1, userType2) = (userType2, userType1);
        }

        // Look for existing MessageIndex
        var existingIndex = await _context.MessageIndexes
            .FirstOrDefaultAsync(mi => mi.UserId1 == userId1 && mi.UserType1 == userType1 && 
                                       mi.UserId2 == userId2 && mi.UserType2 == userType2);

        if (existingIndex != null)
            return existingIndex;

        // Create new MessageIndex if it doesn't exist
        var newIndex = new MessageIndex
        {
            UserId1 = userId1,
            UserType1 = userType1,
            UserId2 = userId2,
            UserType2 = userType2,
            InitiatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };

        _context.MessageIndexes.Add(newIndex);
        await _context.SaveChangesAsync();

        return newIndex;
    }
}

public class SendMessageRequest
{
    public string? Content { get; set; }
}

public class SendDirectMessageRequest
{
    public int RecipientId { get; set; }
    public string? SenderType { get; set; }
    public string? Content { get; set; }
}

