using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;
using ServiceProviderAPI.Services;
using BC = BCrypt.Net.BCrypt;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ITokenBlacklistService _blacklist;
    private readonly INotificationService _notifications;
    private readonly IConfiguration _configuration;
    private readonly IServiceAreaService _serviceAreaService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ApplicationDbContext context,
        IJwtService jwtService,
        ITokenBlacklistService blacklist,
        INotificationService notifications,
        IConfiguration configuration,
        IServiceAreaService serviceAreaService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _blacklist = blacklist;
        _notifications = notifications;
        _configuration = configuration;
        _serviceAreaService = serviceAreaService;
        _logger = logger;
    }

    [HttpPost("pro/login")]
    [EnableRateLimiting("auth-login")]
    public async Task<ActionResult<LoginResponse>> LoginPro(LoginRequest request)
    {
        var pro = await _context.Pros
            .FirstOrDefaultAsync(p => p.Email == request.Email);

        if (pro == null)
            return Unauthorized(new { message = "Invalid email or password" });

        if (pro.LockoutUntil.HasValue && pro.LockoutUntil > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((pro.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes);
            return Unauthorized(new { message = $"Account locked. Try again in {remaining} minute(s)." });
        }

        if (!BC.Verify(request.Password, pro.PasswordHash))
        {
            pro.FailedLoginAttempts++;
            if (pro.FailedLoginAttempts >= 5)
            {
                pro.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
                pro.FailedLoginAttempts = 0;
            }
            await _context.SaveChangesAsync();
            return Unauthorized(new { message = "Invalid email or password" });
        }

        pro.FailedLoginAttempts = 0;
        pro.LockoutUntil = null;

        var (token, _) = _jwtService.GenerateToken(pro, "Pro");
        var refresh = await CreateRefreshTokenAsync(pro.Id, "Pro");
        await _context.SaveChangesAsync();

        return new LoginResponse
        {
            Token = token,
            RefreshToken = refresh.Token,
            Role = "Pro",
            Id = pro.Id,
            FirstName = pro.ProName,
            Email = pro.Email,
            IsProfileComplete = pro.IsProfileComplete
        };
    }

    [HttpPost("user/login")]
    [EnableRateLimiting("auth-login")]
    public async Task<ActionResult<LoginResponse>> LoginUser(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
            return Unauthorized(new { message = "Invalid email or password" });

        if (user.LockoutUntil.HasValue && user.LockoutUntil > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((user.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes);
            return Unauthorized(new { message = $"Account locked. Try again in {remaining} minute(s)." });
        }

        if (!BC.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
                user.FailedLoginAttempts = 0;
            }
            await _context.SaveChangesAsync();
            return Unauthorized(new { message = "Invalid email or password" });
        }

        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;

        var role = user.UserType ?? "User";
        var (token, _) = _jwtService.GenerateToken(user, role);
        var refresh = await CreateRefreshTokenAsync(user.Id, role);
        await _context.SaveChangesAsync();

        return new LoginResponse
        {
            Token = token,
            RefreshToken = refresh.Token,
            Role = role,
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email
        };
    }

    [HttpPost("user/register")]
    [EnableRateLimiting("auth-register")]
    public async Task<ActionResult<LoginResponse>> RegisterUser(UserRegistrationRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest(new { message = "Email already registered" });

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = BC.HashPassword(request.Password),
            PhoneNumber = request.PhoneNumber,
            HouseNameNumber = request.HouseNameNumber,
            Street1 = request.Street1,
            Street2 = request.Street2,
            City = request.City,
            State = request.State,
            Country = request.Country,
            ZipPostalCode = request.ZipPostalCode,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var (token, _) = _jwtService.GenerateToken(user, "User");
        var refresh = await CreateRefreshTokenAsync(user.Id, "User");
        return new LoginResponse
        {
            Token = token,
            RefreshToken = refresh.Token,
            Role = "User",
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email
        };
    }

    [HttpPost("pro/register")]
    [EnableRateLimiting("auth-register")]
    public async Task<ActionResult<LoginResponse>> RegisterPro(ProRegistrationRequest request)
    {
        if (await _context.Pros.AnyAsync(p => p.Email == request.Email))
            return BadRequest(new { message = "Email already registered" });

        // Check if any service areas are configured; if so, restrict to allowed countries
        var hasAnyAreas = await _context.ServiceAreas.AnyAsync();
        if (hasAnyAreas && !string.IsNullOrWhiteSpace(request.Country))
        {
            var countryAllowed = await _serviceAreaService.IsCountryAllowedAsync(request.Country);
            if (!countryAllowed)
                return BadRequest(new { message = $"We are not currently operating in {request.Country}. Please check back soon as we expand our service areas." });
        }

        var pro = new Pro
        {
            ProName = request.Name,
            Email = request.Email,
            PasswordHash = BC.HashPassword(request.Password),
            PhoneNumber = request.PhoneNumber,
            BusinessName = request.BusinessName,
            HouseNameNumber = request.HouseNameNumber,
            Street1 = request.Street1,
            Street2 = request.Street2,
            City = request.City,
            District = request.District,
            State = request.State,
            Country = request.Country,
            ZipPostalCode = request.ZipPostalCode,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            IsProfileComplete = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Pros.Add(pro);
        await _context.SaveChangesAsync();

        // Auto-enroll pro's location if country is allowed but specific area not yet listed
        if (!string.IsNullOrWhiteSpace(request.Country))
        {
            try
            {
                await _serviceAreaService.AutoEnrollProLocationAsync(
                    request.Country,
                    request.State,
                    request.District,
                    request.ZipPostalCode);
            }
            catch (Exception ex)
            {
                // Non-fatal: log and continue
                _logger.LogWarning(ex, "Failed to auto-enroll pro location for pro {ProId}", pro.Id);
            }
        }

        var (token, _) = _jwtService.GenerateToken(pro, "Pro");
        var refresh = await CreateRefreshTokenAsync(pro.Id, "Pro");
        return new LoginResponse
        {
            Token = token,
            RefreshToken = refresh.Token,
            Role = "Pro",
            Id = pro.Id,
            FirstName = pro.ProName,
            Email = pro.Email
        };
    }

    [HttpPost("pro/register/draft")]
    public async Task<ActionResult<object>> RegisterProStep1(ProRegistrationStep1Request request)
    {
        if (await _context.Pros.AnyAsync(p => p.Email == request.Email))
            return BadRequest(new { message = "This email is already registered. Please log in to complete or access your profile." });

        var pro = new Pro
        {
            ProName = request.Name,
            Email = request.Email,
            PasswordHash = BC.HashPassword(request.Password),
            PhoneNumber = request.PhoneNumber,
            BusinessName = request.BusinessName,
            IsProfileComplete = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Pros.Add(pro);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Pro draft created for {Email}, ProId={ProId}", request.Email, pro.Id);

        return Ok(new { proId = pro.Id });
    }

    [HttpPost("pro/register/complete/{proId:int}")]
    [EnableRateLimiting("auth-register")]
    public async Task<ActionResult<LoginResponse>> RegisterProStep2(int proId, ProRegistrationStep2Request request)
    {
        var pro = await _context.Pros.FindAsync(proId);
        if (pro == null || pro.IsProfileComplete)
            return NotFound(new { message = "Draft registration not found" });

        var hasAnyAreas = await _context.ServiceAreas.AnyAsync();
        if (hasAnyAreas && !string.IsNullOrWhiteSpace(request.Country))
        {
            var countryAllowed = await _serviceAreaService.IsCountryAllowedAsync(request.Country);
            if (!countryAllowed)
                return BadRequest(new { message = $"We are not currently operating in {request.Country}. Please check back soon as we expand our service areas." });
        }

        pro.HouseNameNumber = request.HouseNameNumber;
        pro.Street1 = request.Street1;
        pro.Street2 = request.Street2;
        pro.City = request.City;
        pro.District = request.District;
        pro.State = request.State;
        pro.Country = request.Country;
        pro.ZipPostalCode = request.ZipPostalCode;
        pro.Latitude = request.Latitude;
        pro.Longitude = request.Longitude;
        pro.IsProfileComplete = true;
        pro.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        try
        {
            if (!string.IsNullOrWhiteSpace(request.Country))
            {
                await _serviceAreaService.AutoEnrollProLocationAsync(
                    request.Country, request.State, request.District, request.ZipPostalCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-enroll pro location for pro {ProId}", pro.Id);
        }

        var (token, _) = _jwtService.GenerateToken(pro, "Pro");
        var refresh = await CreateRefreshTokenAsync(pro.Id, "Pro");
        return new LoginResponse
        {
            Token = token,
            RefreshToken = refresh.Token,
            Role = "Pro",
            Id = pro.Id,
            FirstName = pro.ProName,
            Email = pro.Email,
            IsProfileComplete = true
        };
    }

    [HttpPost("accept-admin-invite")]
    public async Task<ActionResult<LoginResponse>> AcceptAdminInvitation([FromBody] AcceptAdminInvitationRequest request)
    {
        if (string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.Password) ||
            string.IsNullOrEmpty(request.FirstName) || string.IsNullOrEmpty(request.LastName))
        {
            return BadRequest(new { message = "Token, password, first name, and last name are required" });
        }

        try
        {
            var invitation = await _context.AdminInvitations
                .FirstOrDefaultAsync(ai => ai.Token == request.Token);

            if (invitation == null)
                return BadRequest(new { message = "Invalid invitation token" });

            if (invitation.IsUsed)
                return BadRequest(new { message = "This invitation has already been used" });

            if (invitation.ExpiresAt <= DateTime.UtcNow)
                return BadRequest(new { message = "This invitation has expired" });

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == invitation.Email);

            if (existingUser != null)
                return BadRequest(new { message = "An account already exists for this email" });

            var user = new User
            {
                Email = invitation.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PasswordHash = BC.HashPassword(request.Password),
                UserType = "Admin",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            invitation.IsUsed = true;
            invitation.UsedAt = DateTime.UtcNow;
            _context.AdminInvitations.Update(invitation);
            await _context.SaveChangesAsync();

            var (token, _) = _jwtService.GenerateToken(user, "Admin");
            var refresh = await CreateRefreshTokenAsync(user.Id, "Admin");
            return new LoginResponse
            {
                Token = token,
                RefreshToken = refresh.Token,
                Role = "Admin",
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email
            };
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Error processing invitation", error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/auth/forgot-password — sends a password reset link if the account exists.
    /// Always returns 200 to prevent email enumeration.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth-forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        bool accountExists = request.UserType == "Pro"
            ? await _context.Pros.AnyAsync(p => p.Email == request.Email)
            : await _context.Users.AnyAsync(u => u.Email == request.Email);

        if (!accountExists)
            return Ok(new { message = "If an account with that email exists, you will receive a reset link." });

        // Invalidate any outstanding tokens for this email/type
        var existing = await _context.PasswordResetTokens
            .Where(t => t.Email == request.Email && t.UserType == request.UserType && !t.IsUsed)
            .ToListAsync();
        foreach (var t in existing)
            t.IsUsed = true;

        var resetToken = new PasswordResetToken
        {
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower(),
            Email = request.Email,
            UserType = request.UserType,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.PasswordResetTokens.Add(resetToken);
        await _context.SaveChangesAsync();

        var appUrl = _configuration["AppUrl"] ?? "http://localhost:4200";
        var resetLink = $"{appUrl}/auth/reset-password?token={resetToken.Token}&userType={request.UserType}";

        await _notifications.NotifyAsync(
            request.Email,
            null,
            "Reset your yProHub password",
            $@"Hi,

You requested a password reset for your yProHub account.

Click the link below to set a new password (expires in 1 hour):

{resetLink}

If you did not request this, you can safely ignore this email.

— yProHub Team");

        return Ok(new { message = "If an account with that email exists, you will receive a reset link." });
    }

    /// <summary>
    /// POST /api/auth/reset-password — validates the token and sets the new password.
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var tokenEntry = await _context.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.Token == request.Token && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow);

        if (tokenEntry == null)
            return BadRequest(new { message = "Invalid or expired reset token." });

        if (tokenEntry.UserType == "Pro")
        {
            var pro = await _context.Pros.FirstOrDefaultAsync(p => p.Email == tokenEntry.Email);
            if (pro == null) return BadRequest(new { message = "Account not found." });
            pro.PasswordHash = BC.HashPassword(request.NewPassword);
            pro.FailedLoginAttempts = 0;
            pro.LockoutUntil = null;
        }
        else
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == tokenEntry.Email);
            if (user == null) return BadRequest(new { message = "Account not found." });
            user.PasswordHash = BC.HashPassword(request.NewPassword);
            user.FailedLoginAttempts = 0;
            user.LockoutUntil = null;
        }

        tokenEntry.IsUsed = true;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Password reset successfully. You may now log in." });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? body = null)
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (!string.IsNullOrEmpty(jti))
        {
            var expClaim = User.FindFirstValue(JwtRegisteredClaimNames.Exp);
            DateTime expiresAt = DateTime.UtcNow.AddMinutes(15);
            if (long.TryParse(expClaim, out var expSeconds))
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
            await _blacklist.RevokeAsync(jti, expiresAt);
        }

        if (!string.IsNullOrEmpty(body?.RefreshToken))
        {
            var stored = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == body.RefreshToken);
            if (stored != null && !stored.IsRevoked)
            {
                stored.IsRevoked = true;
                stored.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        return Ok(new { message = "Logged out successfully." });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshRequest request)
    {
        var stored = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (stored == null || stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow)
            return Unauthorized(new { message = "Invalid or expired refresh token" });

        stored.IsRevoked = true;
        stored.RevokedAt = DateTime.UtcNow;

        string accessToken;
        RefreshToken newRefresh;

        if (stored.SubjectType == "Pro")
        {
            var pro = await _context.Pros.FindAsync(stored.SubjectId);
            if (pro == null) return Unauthorized(new { message = "Account not found" });
            (accessToken, _) = _jwtService.GenerateToken(pro, "Pro");
            newRefresh = BuildRefreshToken(pro.Id, "Pro");
        }
        else
        {
            var user = await _context.Users.FindAsync(stored.SubjectId);
            if (user == null) return Unauthorized(new { message = "Account not found" });
            var role = user.UserType ?? "User";
            (accessToken, _) = _jwtService.GenerateToken(user, role);
            newRefresh = BuildRefreshToken(user.Id, role);
        }

        _context.RefreshTokens.Add(newRefresh);
        await _context.SaveChangesAsync();

        return Ok(new { accessToken, refreshToken = newRefresh.Token });
    }

    private static RefreshToken BuildRefreshToken(int subjectId, string subjectType) => new()
    {
        Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
        SubjectId = subjectId,
        SubjectType = subjectType,
        ExpiresAt = DateTime.UtcNow.AddDays(30),
        IsRevoked = false,
        CreatedAt = DateTime.UtcNow
    };

    private async Task<RefreshToken> CreateRefreshTokenAsync(int subjectId, string subjectType)
    {
        var token = BuildRefreshToken(subjectId, subjectType);
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();
        return token;
    }
}
