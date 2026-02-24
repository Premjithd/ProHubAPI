namespace ServiceProviderAPI.Services;

public interface IEmailService
{
    Task SendAdminInvitationAsync(string recipientEmail, string invitationLink);
    Task SendEmailAsync(string to, string subject, string body);
}

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAdminInvitationAsync(string recipientEmail, string invitationLink)
    {
        var subject = "ProHub Admin Invitation";
        var body = $@"
            <h2>You're Invited to Join ProHub Admin Team</h2>
            <p>You have been invited to become a platform administrator for ProHub.</p>
            <p>
                <a href='{invitationLink}' style='display: inline-block; padding: 10px 20px; background-color: #667eea; color: white; text-decoration: none; border-radius: 5px;'>
                    Accept Invitation
                </a>
            </p>
            <p>This invitation will expire in 7 days.</p>
            <p>If you did not expect this invitation, please ignore this email.</p>
        ";

        await SendEmailAsync(recipientEmail, subject, body);
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var smtpServer = _configuration["Email:SmtpServer"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var senderEmail = _configuration["Email:SenderEmail"];
            var senderPassword = _configuration["Email:SenderPassword"];

            using (var client = new System.Net.Mail.SmtpClient(smtpServer, smtpPort))
            {
                client.EnableSsl = true;
                client.Credentials = new System.Net.NetworkCredential(senderEmail, senderPassword);

                var mailMessage = new System.Net.Mail.MailMessage
                {
                    From = new System.Net.Mail.MailAddress(senderEmail, "ProHub"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(to);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation($"Email sent successfully to {to}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error sending email to {to}: {ex.Message}");
            throw;
        }
    }
}
