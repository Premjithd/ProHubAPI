using System.Net;
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
    /// Directly invite a single pro — whom the consumer specifically requested — to bid on a job.
    /// </summary>
    Task<bool> NotifyProDirectJobRequestAsync(Job job, Pro pro, User consumer, string categoryName, string jobLink);

    /// <summary>
    /// Notify pro that consumer has confirmed payment and waiting for pro confirmation
    /// </summary>
    Task<bool> NotifyProPaymentReceivedAsync(Job job, JobBid bid, Pro pro, decimal amount);

    /// <summary>
    /// Notify the consumer that the assigned Pro has requested a payment for their job.
    /// </summary>
    Task<bool> NotifyUserPaymentRequestedAsync(Job job, User consumer, string requestType, decimal requestedAmount);

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
            yProHub Team
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
            var serviceArea = $"{job.ServiceAddress?.City}, {job.ServiceAddress?.State}";
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
            yProHub Team
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

    public async Task<bool> NotifyProDirectJobRequestAsync(Job job, Pro pro, User consumer, string categoryName, string jobLink)
    {
        try
        {
            if (string.IsNullOrEmpty(pro.Email) && string.IsNullOrEmpty(pro.PhoneNumber))
            {
                _logger.LogWarning($"Requested pro {pro.Id} has no email or phone to notify");
                return false;
            }

            var consumerName = $"{consumer.FirstName} {consumer.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(consumerName)) consumerName = "A yProHub customer";
            var subject = $"{consumerName} requested you for \"{job.Title}\" on yProHub";
            var location = string.Join(", ",
                new[] { job.ServiceAddress?.City, job.ServiceAddress?.State }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
            var budget = job.EstimatedBudget.HasValue ? $"₹{job.EstimatedBudget:N0}" : (job.Budget ?? "Not specified");

            var tasks = new List<Task<bool>>();

            // Branded HTML email — SmtpEmailService wraps this inner content in the shared
            // shell (logo + footer), so we only author the body here (styled CTA button like
            // the admin-invite template).
            if (!string.IsNullOrEmpty(pro.Email) && _emailChannel != null)
            {
                var html = $@"
                    <h1 style=""margin:0 0 12px 0; font-size:20px; color:#1a1a1a;"">You've been personally requested</h1>
                    <p style=""margin:0 0 16px 0;"">Hi {WebUtility.HtmlEncode(pro.ProName)},</p>
                    <p style=""margin:0 0 16px 0;""><strong>{WebUtility.HtmlEncode(consumerName)}</strong> selected you on yProHub to carry out the job below and is inviting you to place a bid.</p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f9fafb; border:1px solid #e5e7eb; border-radius:8px; margin:0 0 20px 0;"">
                      <tr><td style=""padding:16px 18px; font-size:14px; color:#374151; line-height:1.7;"">
                        <strong style=""font-size:15px; color:#111827;"">{WebUtility.HtmlEncode(job.Title)}</strong><br/>
                        <span style=""color:#6b7280;"">Category:</span> {WebUtility.HtmlEncode(categoryName)}<br/>
                        {(string.IsNullOrWhiteSpace(location) ? "" : $"<span style=\"color:#6b7280;\">Location:</span> {WebUtility.HtmlEncode(location)}<br/>")}
                        <span style=""color:#6b7280;"">Budget:</span> {WebUtility.HtmlEncode(budget)}<br/>
                        <span style=""color:#6b7280;"">Timeline:</span> {WebUtility.HtmlEncode(job.Timeline ?? "Flexible")}<br/>
                        {(string.IsNullOrWhiteSpace(job.Description) ? "" : $"<span style=\"color:#6b7280;\">Details:</span> {WebUtility.HtmlEncode(job.Description)}")}
                      </td></tr>
                    </table>
                    <div style=""text-align:center; margin:24px 0;"">
                        <a href=""{jobLink}"" style=""display:inline-block; background-color:#1d4ed8; color:#ffffff; text-decoration:none; padding:12px 28px; border-radius:8px; font-size:15px; font-weight:600;"">View job &amp; place your bid</a>
                    </div>
                    <p style=""margin:0 0 6px 0; font-size:13px; color:#6b7280;"">If the button doesn't work, copy and paste this URL into your browser:</p>
                    <p style=""margin:0 0 20px 0; font-size:12px; word-break:break-all;""><a href=""{jobLink}"" style=""color:#1d4ed8;"">{jobLink}</a></p>
                    <p style=""margin:0; font-size:12px; color:#6b7280;"">Other professionals may also bid on this job, so respond promptly to win the work.</p>
                ";
                tasks.Add(_emailChannel.SendHtmlEmailAsync(pro.Email, subject, html));
            }

            // Concise branded SMS
            if (!string.IsNullOrEmpty(pro.PhoneNumber) && _smsChannel != null)
            {
                var smsText = BuildSmsText(
                    $"{consumerName} requested you for \"{job.Title}\"",
                    $"Place your bid now: {jobLink}");
                tasks.Add(_smsChannel.SendAsync(pro.PhoneNumber, subject, smsText));
            }

            if (tasks.Count == 0)
            {
                _logger.LogWarning($"No notification channels available to send direct request to pro {pro.Id}");
                return false;
            }

            var results = await Task.WhenAll(tasks);
            return results.Any(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error sending direct job request to pro {pro.Id}: {ex.Message}");
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
            Service Location: {job.ServiceAddress?.City}, {job.ServiceAddress?.State}
            Contact: {job.ContactPersonName} - {job.ContactPersonPhone}

            Please confirm your acceptance of this job assignment to proceed.

            Best regards,
            yProHub Team
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
            yProHub Team
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

            Thank you for using yProHub!

            Best regards,
            yProHub Team
            ";

            return await NotifyAsync(consumer.Email, consumer.PhoneNumber, subject, message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error notifying consumer of completion: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> NotifyUserPaymentRequestedAsync(Job job, User consumer, string requestType, decimal requestedAmount)
    {
        try
        {
            var subject = "Payment Requested for Your Job";
            var amountLine = requestType == "None"
                ? "The professional has waived upfront payment — work can begin right away."
                : $"Requested Amount: ₹{requestedAmount:N2}";

            var message = $@"
            Hi {consumer.FirstName},

            The professional assigned to your job '{job.Title}' has raised a payment request.

            {amountLine}

            Please open the job to review and complete the payment.

            Best regards,
            yProHub Team
            ";

            return await NotifyAsync(consumer.Email, consumer.PhoneNumber, subject, message);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error notifying consumer of payment request: {ex.Message}");
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

        // Send SMS (shorter, brand-prefixed message)
        if (!string.IsNullOrEmpty(recipientPhone) && _smsChannel != null)
        {
            var smsMessage = BuildSmsText(subject, message);
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

    /// <summary>
    /// Builds a concise, brand-prefixed SMS from an email subject + body. Collapses the
    /// multi-line email copy into a single clean line capped to a sensible SMS length.
    /// </summary>
    private static string BuildSmsText(string subject, string message)
    {
        var cleanedBody = System.Text.RegularExpressions.Regex.Replace(message ?? "", @"\s+", " ").Trim();
        var text = string.IsNullOrEmpty(cleanedBody) ? subject : $"{subject}. {cleanedBody}";

        const int maxLen = 150;
        if (text.Length > maxLen)
            text = text.Substring(0, maxLen).TrimEnd() + "…";

        return $"yProHub: {text}";
    }
}
