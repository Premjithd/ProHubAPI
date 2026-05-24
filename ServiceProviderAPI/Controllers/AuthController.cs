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

    public AuthController(ApplicationDbContext context, IJwtService jwtService, ITokenBlacklistService blacklist)
    {
        _context = context;
        _jwtService = jwtService;
        _blacklist = blacklist;
    }

    [HttpPost("pro/login")]
    [EnableRateLimiting("auth-login")]
    public async Task<ActionResult<LoginResponse>> LoginPro(LoginRequest request)
    {
        var pro = await _context.Pros
            .FirstOrDefaultAsync(p => p.Email == request.Email);

        if (pro == null || !BC.Verify(request.Password, pro.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password" });

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

    [HttpPost("user/login")]
    [EnableRateLimiting("auth-login")]
    public async Task<ActionResult<LoginResponse>> LoginUser(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !BC.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password" });

        var role = user.UserType ?? "User";
        var (token, _) = _jwtService.GenerateToken(user, role);
        var refresh = await CreateRefreshTokenAsync(user.Id, role);
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
        // Check if email already exists
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            return BadRequest(new { message = "Email already registered" });
        }

        // Create new user
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
        // Check if email already exists
        if (await _context.Pros.AnyAsync(p => p.Email == request.Email))
        {
            return BadRequest(new { message = "Email already registered" });
        }

        // Create new pro
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
            State = request.State,
            Country = request.Country,
            ZipPostalCode = request.ZipPostalCode,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Pros.Add(pro);
        await _context.SaveChangesAsync();

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
            // Find the invitation
            var invitation = await _context.AdminInvitations
                .FirstOrDefaultAsync(ai => ai.Token == request.Token);

            if (invitation == null)
                return BadRequest(new { message = "Invalid invitation token" });

            if (invitation.IsUsed)
                return BadRequest(new { message = "This invitation has already been used" });

            if (invitation.ExpiresAt <= DateTime.UtcNow)
                return BadRequest(new { message = "This invitation has expired" });

            // Check if email already exists in Users table
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == invitation.Email);

            if (existingUser != null)
                return BadRequest(new { message = "An account already exists for this email" });

            // Create new user with Admin role
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

            // Mark invitation as used
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

        // Rotate: revoke old token
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
