using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Services;

public interface IVerificationService
{
    Task<string> GenerateAndSendEmailVerificationCode(string email, string userType);
    Task<string> GenerateAndSendPhoneVerificationCode(string phoneNumber, string userType);
    Task<bool> VerifyEmailCode(string email, string code, string userType);
    Task<bool> VerifyPhoneCode(string phoneNumber, string code, string userType);
}

public class VerificationService : IVerificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly Random _random;

    public VerificationService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
        _random = new Random();
    }

    public async Task<string> GenerateAndSendEmailVerificationCode(string email, string userType)
    {
        var code = GenerateRandomCode();
        var verificationCode = new VerificationCode
        {
            Code = code,
            Email = email,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false,
            Type = "Email",
            UserType = userType
        };

        _context.VerificationCodes.Add(verificationCode);
        await _context.SaveChangesAsync();

        await SendVerificationEmail(email, code);
        return code;
    }

    public async Task<string> GenerateAndSendPhoneVerificationCode(string phoneNumber, string userType)
    {
        var code = GenerateRandomCode();
        var verificationCode = new VerificationCode
        {
            Code = code,
            PhoneNumber = phoneNumber,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false,
            Type = "Phone",
            UserType = userType
        };

        _context.VerificationCodes.Add(verificationCode);
        await _context.SaveChangesAsync();

        await SendVerificationSms(phoneNumber, code);
        return code;
    }

    public async Task<bool> VerifyEmailCode(string email, string code, string userType)
    {
        var verificationCode = await _context.VerificationCodes
            .OrderByDescending(v => v.ExpiresAt)
            .FirstOrDefaultAsync(v => 
                v.Email == email && 
                v.Code == code && 
                v.Type == "Email" &&
                v.UserType == userType &&
                !v.IsUsed &&
                v.ExpiresAt > DateTime.UtcNow);

        if (verificationCode == null)
            return false;

        verificationCode.IsUsed = true;
        await _context.SaveChangesAsync();

        // Update user or pro email verification status
        if (userType == "User")
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null)
            {
                user.IsEmailVerified = true;
                await _context.SaveChangesAsync();
            }
        }
        else
        {
            var pro = await _context.Pros.FirstOrDefaultAsync(p => p.Email == email);
            if (pro != null)
            {
                pro.IsEmailVerified = true;
                await _context.SaveChangesAsync();
            }
        }

        return true;
    }

    public async Task<bool> VerifyPhoneCode(string phoneNumber, string code, string userType)
    {
        var verificationCode = await _context.VerificationCodes
            .OrderByDescending(v => v.ExpiresAt)
            .FirstOrDefaultAsync(v => 
                v.PhoneNumber == phoneNumber && 
                v.Code == code && 
                v.Type == "Phone" &&
                v.UserType == userType &&
                !v.IsUsed &&
                v.ExpiresAt > DateTime.UtcNow);

        if (verificationCode == null)
            return false;

        verificationCode.IsUsed = true;
        await _context.SaveChangesAsync();

        // Update user or pro phone verification status
        if (userType == "User")
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
            if (user != null)
            {
                user.IsPhoneVerified = true;
                await _context.SaveChangesAsync();
            }
        }
        else
        {
            var pro = await _context.Pros.FirstOrDefaultAsync(p => p.PhoneNumber == phoneNumber);
            if (pro != null)
            {
                pro.IsPhoneVerified = true;
                await _context.SaveChangesAsync();
            }
        }

        return true;
    }

    private string GenerateRandomCode()
    {
        return _random.Next(100000, 999999).ToString();
    }

    private async Task SendVerificationEmail(string email, string code)
    {
        // In a production environment, use a proper email service
        // This is just a placeholder implementation
        var smtpClient = new SmtpClient(_configuration["Email:SmtpServer"])
        {
            Port = int.Parse(_configuration["Email:Port"]),
            Credentials = new System.Net.NetworkCredential(_configuration["Email:Username"], _configuration["Email:Password"]),
            EnableSsl = true,
        };

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_configuration["Email:From"], "yProHub"),
            Subject = "Your yProHub verification code",
            IsBodyHtml = true
        };
        mailMessage.To.Add(email);

        // Wrap the code-specific content in the shared branded shell (logo embedded inline).
        mailMessage.AlternateViews.Add(EmailTemplates.BuildBrandedView(
            BuildVerificationEmailContent(code),
            "This email was sent by <strong>yProHub</strong> because you requested a verification code."));

        await smtpClient.SendMailAsync(mailMessage);
        Console.WriteLine($"Email verification code sent to {email}");
    }

    private static string BuildVerificationEmailContent(string code)
    {
        return $@"
          <div style=""text-align:center;"">
            <h1 style=""margin:0 0 8px 0; font-size:20px; color:#1a1a1a;"">Verify your email</h1>
            <p style=""margin:0 0 20px 0; font-size:14px; color:#4b5563;"">Use the code below to complete your verification.</p>
            <div style=""display:inline-block; background-color:#f0f4ff; border:1px solid #d6e0ff; border-radius:10px; padding:16px 28px; font-size:32px; font-weight:700; letter-spacing:8px; color:#1d4ed8;"">{code}</div>
            <p style=""margin:16px 0 0 0; font-size:13px; color:#6b7280;"">This code expires in <strong>15 minutes</strong>.</p>
          </div>
          <p style=""margin:20px 0 0 0; font-size:12px; color:#6b7280; line-height:1.5;"">If you did not request this code, you can safely ignore this email — your account remains secure. Never share this code with anyone; yProHub staff will never ask you for it.</p>";
    }

    private async Task SendVerificationSms(string phoneNumber, string code)
    {
        // In a production environment, use a proper SMS service
        // This is just a placeholder implementation
        var message = $"yProHub: {code} is your verification code. It expires in 15 minutes. Do not share this code with anyone.";

        // For development, just log the branded message
        Console.WriteLine($"SMS verification message for {phoneNumber}: {message}");
        await Task.CompletedTask;
    }
}
