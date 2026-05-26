using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public string Password { get; set; }
}

public class LoginResponse
{
    public string Token { get; set; }
    public string? RefreshToken { get; set; }
    public string Role { get; set; }
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequest
{
    public string? RefreshToken { get; set; }
}

public class AcceptAdminInvitationRequest
{
    [Required]
    public string Token { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FirstName { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string LastName { get; set; }

    [Required]
    [StringLength(255, MinimumLength = 6)]
    public string Password { get; set; }
}

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string UserType { get; set; } = null!; // "User" or "Pro"
}

public class ResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = null!;

    [Required]
    [StringLength(255, MinimumLength = 8)]
    public string NewPassword { get; set; } = null!;
}
