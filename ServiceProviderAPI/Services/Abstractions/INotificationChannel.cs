namespace ServiceProviderAPI.Services.Abstractions;

/// <summary>
/// Abstraction for sending notifications through different channels (email, SMS, push, etc.)
/// </summary>
public interface INotificationChannel
{
    /// <summary>
    /// Send a notification to a recipient
    /// </summary>
    /// <param name="recipient">Email address, phone number, or user ID depending on channel</param>
    /// <param name="subject">Subject line (for email) or message title</param>
    /// <param name="body">Message body/content</param>
    /// <returns>True if sent successfully, false otherwise</returns>
    Task<bool> SendAsync(string recipient, string subject, string body);

    /// <summary>
    /// Friendly name of the channel for logging
    /// </summary>
    string ChannelName { get; }
}

/// <summary>
/// Email channel implementation (SMTP, SendGrid, Azure Mail, etc.)
/// </summary>
public interface IEmailChannel : INotificationChannel
{
    /// <summary>
    /// Send HTML email with optional attachments
    /// </summary>
    Task<bool> SendHtmlEmailAsync(string toEmail, string subject, string htmlBody);
}

/// <summary>
/// SMS channel implementation (MSG91, Twilio, etc.)
/// </summary>
public interface ISmsChannel : INotificationChannel
{
    /// <summary>
    /// Send SMS to a phone number
    /// </summary>
    Task<bool> SendSmsAsync(string phoneNumber, string message);
}
