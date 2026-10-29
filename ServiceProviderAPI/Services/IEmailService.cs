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
        var subject = "yProHub Admin Invitation";
        // Branded HTML (SendEmailAsync wraps this in the shared shell with the logo
        // embedded inline). Rendered as a styled button, not a raw link.
        var body = $@"
            <h1 style=""margin:0 0 12px 0; font-size:20px; color:#1a1a1a;"">You're invited to join the yProHub Admin Team</h1>
            <p style=""margin:0 0 16px 0;"">You have been invited to become a platform administrator for yProHub. Click the button below to accept and set up your account.</p>
            <div style=""text-align:center; margin:24px 0;"">
                <a href=""{invitationLink}"" style=""display:inline-block; background-color:#1d4ed8; color:#ffffff; text-decoration:none; padding:12px 28px; border-radius:8px; font-size:15px; font-weight:600;"">Accept invitation</a>
            </div>
            <p style=""margin:0 0 6px 0; font-size:13px; color:#6b7280;"">This invitation expires in <strong>7 days</strong>. If the button doesn't work, copy and paste this URL into your browser:</p>
            <p style=""margin:0 0 20px 0; font-size:12px; word-break:break-all;""><a href=""{invitationLink}"" style=""color:#1d4ed8;"">{invitationLink}</a></p>
            <p style=""margin:0; font-size:12px; color:#6b7280;"">If you did not expect this invitation, you can safely ignore this email.</p>
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

                using var mailMessage = new System.Net.Mail.MailMessage
                {
                    From = new System.Net.Mail.MailAddress(senderEmail, "yProHub"),
                    Subject = subject,
                    IsBodyHtml = true
                };

                // Wrap the body in the shared branded shell with the logo embedded inline.
                mailMessage.AlternateViews.Add(EmailTemplates.BuildBrandedView(body));

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
