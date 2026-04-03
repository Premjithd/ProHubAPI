using ServiceProviderAPI.Services.Abstractions;

namespace ServiceProviderAPI.Services.Channels;

/// <summary>
/// Email channel implementation using SMTP (via existing IEmailService)
/// Phase 1B implementation
/// </summary>
public class SmtpEmailChannel : IEmailChannel
{
    private readonly ILogger<SmtpEmailChannel> _logger;
    private readonly IEmailService _emailService;

    public string ChannelName => "SMTP Email";

    public SmtpEmailChannel(
        ILogger<SmtpEmailChannel> logger,
        IEmailService emailService)
    {
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<bool> SendAsync(string recipient, string subject, string body)
    {
        try
        {
            _logger.LogInformation($"Sending email to {recipient}: {subject}");
            
            // Convert plain text to HTML
            var htmlBody = $"<pre>{body}</pre>";
            return await SendHtmlEmailAsync(recipient, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to send email: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SendHtmlEmailAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            await _emailService.SendEmailAsync(toEmail, subject, htmlBody);
            _logger.LogInformation($"Email sent successfully to {toEmail}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error sending HTML email to {toEmail}: {ex.Message}");
            return false;
        }
    }
}
