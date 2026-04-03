using ServiceProviderAPI.Models;
using ServiceProviderAPI.Data;

namespace ServiceProviderAPI.Services;

/// <summary>
/// Centralized notification service that dispatches to multiple channels (email, SMS, push)
/// Phase 1B core service
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Notify consumer when a new quote is received for their job
    /// </summary>
    Task<bool> NotifyConsumerNewQuoteAsync(Job job, JobBid bid, Pro pro, User consumer);

    /// <summary>
    /// Notify pros about a new job in their service area
    /// </summary>
    Task<bool> NotifyProsNewJobAsync(Job job, List<Pro> pros);

    /// <summary>
    /// Notify pro that consumer has confirmed payment and waiting for pro confirmation
    /// </summary>
    Task<bool> NotifyProPaymentReceivedAsync(Job job, JobBid bid, Pro pro, decimal amount);

    /// <summary>
    /// Notify consumer when pro accepts the job
    /// </summary>
    Task<bool> NotifyConsumerProAcceptedAsync(Job job, Pro pro, User consumer);

    /// <summary>
    /// Notify consumer when job is completed and awaiting verification
    /// </summary>
    Task<bool> NotifyConsumerCompletionSubmittedAsync(Job job, Pro pro, User consumer);

    /// <summary>
    /// Generic notification dispatch
    /// </summary>
    Task<bool> NotifyAsync(string recipientEmail, string? recipientPhone, string subject, string message);
}

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly Abstractions.IEmailChannel? _emailChannel;
    private readonly Abstractions.ISmsChannel? _smsChannel;
    private readonly ApplicationDbContext _context;

    public NotificationService(
        ILogger<NotificationService> logger,
        ApplicationDbContext context,
        Abstractions.IEmailChannel? emailChannel = null,
        Abstractions.ISmsChannel? smsChannel = null)
    {
        _logger = logger;
        _context = context;
        _emailChannel = emailChannel;
        _smsChannel = smsChannel;
    }

    public async Task<bool> NotifyConsumerNewQuoteAsync(Job job, JobBid bid, Pro pro, User consumer)
    {
        try
        {
            if (string.IsNullOrEmpty(consumer.Email))
            {
                _logger.LogWarning($"Consumer {consumer.Id} has no email address");
                return false;
            }

            var subject = $"New Quote for {job.Title} - ₹{bid.BidAmount}";
            var message = $@"
            Hi {consumer.FirstName},

            Good news! You've received a new quote for your job '{job.Title}'.

            Pro Name: {pro.ProName}
            Business: {pro.BusinessName}
            Bid Amount: ₹{bid.BidAmount:N2}
            Commencement Date: {bid.CommenceDate?.ToString("dd-MMM-yyyy") ?? "Not specified"}
            Expected Duration: {bid.ExpectedDurationDays} days

            {(string.IsNullOrEmpty(bid.BidMessage) ? "" : $"Message: {bid.BidMessage}")}

            Please log in to review and accept this quote.
            Quote expires on: {bid.ExpiresAt?.ToString("dd-MMM-yyyy HH:mm") ?? "Not specified"}

            Best regards,
            ProHub Team
            ";

            return await NotifyAsync(consumer.Email, consumer.PhoneNumber, subject, message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error notifying consumer of new quote: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> NotifyProsNewJobAsync(Job job, List<Pro> pros)
    {
        try
        {
            var subject = $"New Job Opportunity - {job.Title}";
            var serviceArea = $"{job.ServiceAddressCity}, {job.ServiceAddressState}";
            var message = $@"
            Hi there,

            A new job matching your services has been posted in your area!

            Job Title: {job.Title}
            Service Category: {job.Category?.Name ?? "General"}
            Location: {serviceArea}
            Budget: ₹{job.EstimatedBudget:N2}
            Description: {job.Description}
            Contact Person: {job.ContactPersonName} - {job.ContactPersonPhone}

            Log in to submit your quote now. You have a limited time to respond!

            Best regards,
            ProHub Team
            ";

            var tasks = pros.Select(pro => NotifyAsync(pro.Email, pro.PhoneNumber, subject, message));
            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error notifying pros of new job: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> NotifyProPaymentReceivedAsync(Job job, JobBid bid, Pro pro, decimal amount)
    {
        try
        {
            var subject = "Payment Received - Confirm Job Acceptance";
            var message = $@"
            Hi {pro.ProName},

            Payment has been received from the consumer for the job '{job.Title}'.

            Amount Received: ₹{amount:N2}
            Expected Payout: ₹{(amount * 0.9m):N2} (after 10% platform fee)
            Job Start Date: {bid.CommenceDate?.ToString("dd-MMM-yyyy") ?? "To be confirmed"}
            Expected Duration: {bid.ExpectedDurationDays} days
            Service Location: {job.ServiceAddressCity}, {job.ServiceAddressState}
            Contact: {job.ContactPersonName} - {job.ContactPersonPhone}

            Please confirm your acceptance of this job assignment to proceed.

            Best regards,
            ProHub Team
            ";

            return await NotifyAsync(pro.Email, pro.PhoneNumber, subject, message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error notifying pro of payment: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> NotifyConsumerProAcceptedAsync(Job job, Pro pro, User consumer)
    {
        try
        {
            if (string.IsNullOrEmpty(consumer.Email))
            {
                _logger.LogWarning($"Consumer {consumer.Id} has no email address");
                return false;
            }

            var subject = $"Pro Confirmed - {pro.ProName} for {job.Title}";
            var message = $@"
            Hi {consumer.FirstName},

            Great news! {pro.ProName} from {pro.BusinessName} has confirmed acceptance of your job.

            Pro Details:
            Name: {pro.ProName}
            Phone: {pro.PhoneNumber}
            Services: {job.Category?.Name ?? "General services"}

            Work will commence as per the agreed date. The pro will contact you shortly.

            Best regards,
            ProHub Team
            ";

            return await NotifyAsync(consumer.Email, consumer.PhoneNumber, subject, message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error notifying consumer of pro acceptance: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> NotifyConsumerCompletionSubmittedAsync(Job job, Pro pro, User consumer)
    {
        try
        {
            if (string.IsNullOrEmpty(consumer.Email))
            {
                _logger.LogWarning($"Consumer {consumer.Id} has no email address");
                return false;
            }

            var subject = $"Work Completed - {job.Title} - Verification Needed";
            var message = $@"
            Hi {consumer.FirstName},

            {pro.ProName} has submitted the completion details for your job '{job.Title}'.

            Please review the submitted photos, receipts, and notes to verify that the work is completed as per requirements.

            Log in to your account to:
            - View completion photos and receipts
            - Review pro's completion notes
            - Verify completion OR raise a dispute

            Thank you for using ProHub!

            Best regards,
            ProHub Team
            ";

            return await NotifyAsync(consumer.Email, consumer.PhoneNumber, subject, message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error notifying consumer of completion: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> NotifyAsync(string recipientEmail, string? recipientPhone, string subject, string message)
    {
        var tasks = new List<Task<bool>>();

        // Send email
        if (!string.IsNullOrEmpty(recipientEmail) && _emailChannel != null)
        {
            tasks.Add(_emailChannel.SendAsync(recipientEmail, subject, message));
        }

        // Send SMS (shorter message)
        if (!string.IsNullOrEmpty(recipientPhone) && _smsChannel != null)
        {
            var smsMessage = $"{subject}: {message.Substring(0, Math.Min(message.Length, 150))}...";
            tasks.Add(_smsChannel.SendAsync(recipientPhone, subject, smsMessage));
        }

        if (tasks.Count == 0)
        {
            _logger.LogWarning($"No notification channels available to send: {subject}");
            return false;
        }

        var results = await Task.WhenAll(tasks);
        return results.Any(r => r);  // At least one channel worked
    }
}
