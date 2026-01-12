using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public AuthController(ApplicationDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    [HttpPost("pro/login")]
    public async Task<ActionResult<LoginResponse>> LoginPro(LoginRequest request)
    {
        var pro = await _context.Pros
            .FirstOrDefaultAsync(p => p.Email == request.Email);

        if (pro == null || !BC.Verify(request.Password, pro.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var token = _jwtService.GenerateToken(pro, "Pro");
        return new LoginResponse
        {
            Token = token,
            Role = "Pro",
            Id = pro.Id,
            FirstName = pro.ProName,
            Email = pro.Email
        };
    }

    [HttpPost("user/login")]
    public async Task<ActionResult<LoginResponse>> LoginUser(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !BC.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var token = _jwtService.GenerateToken(user, "User");
        return new LoginResponse
        {
            Token = token,
            Role = "User",
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email
        };
    }

    [HttpPost("user/register")]
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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Generate token and return response
        var token = _jwtService.GenerateToken(user, "User");
        return new LoginResponse
        {
            Token = token,
            Role = "User",
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email
        };
    }

    [HttpPost("pro/register")]
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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Pros.Add(pro);
        await _context.SaveChangesAsync();

        // Generate token and return response
        var token = _jwtService.GenerateToken(pro, "Pro");
        return new LoginResponse
        {
            Token = token,
            Role = "Pro",
            Id = pro.Id,
            FirstName = pro.ProName,
            Email = pro.Email
        };
    }
}
