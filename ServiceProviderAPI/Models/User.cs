using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string? FirstName { get; set; }

    [Required]
    [StringLength(100)]
    public string? LastName { get; set; }

    [Required]
    [EmailAddress]
    public string? Email { get; set; }

    public string? PasswordHash { get; set; }

    [Phone]
    public string? PhoneNumber { get; set; }

    public string? UserType { get; set; } = "User";  // "User" or "Pro"

    // Saved payment preference for checkout pre-fill
    [StringLength(100)]
    public string? UpiVpa { get; set; }

    // Address (normalized to Addresses table)
    public int? AddressId { get; set; }
    public Address? Address { get; set; }

    public ICollection<ProUserRelationship>? ProRelationships { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
