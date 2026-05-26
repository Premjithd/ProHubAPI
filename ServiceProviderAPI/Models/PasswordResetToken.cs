using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class PasswordResetToken
{
    public int Id { get; set; }

    [Required]
    public string Token { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string UserType { get; set; } = null!; // "User" or "Pro"

    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; }
}
