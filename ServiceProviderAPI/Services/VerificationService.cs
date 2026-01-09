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

        var mailMessage = new MailMessage
        {
            From = new MailAddress(_configuration["Email:From"]),
            Subject = "Verify your email",
            Body = $"Your verification code is: {code}",
            IsBodyHtml = true
        };
        mailMessage.To.Add(email);

        // Comment out actual sending for development
        // await smtpClient.SendMailAsync(mailMessage);
        
        // For development, just log the code
        Console.WriteLine($"Email verification code for {email}: {code}");
    }

    private async Task SendVerificationSms(string phoneNumber, string code)
    {
        // In a production environment, use a proper SMS service
        // This is just a placeholder implementation
        
        // For development, just log the code
        Console.WriteLine($"SMS verification code for {phoneNumber}: {code}");
    }
}
