using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class Pro
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string ProName { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    public string? PasswordHash { get; set; }

    [Phone]
    public string PhoneNumber { get; set; }

    [Required]
    public string BusinessName { get; set; }

    // Service Radius in kilometers — default 25 km
    public int ServiceRadiusKm { get; set; } = 25;

    // Address (normalized to Addresses table)
    public int? AddressId { get; set; }
    public Address? Address { get; set; }

    public ICollection<Service>? Services { get; set; }
    public ICollection<ProUserRelationship>? ProUsers { get; set; }
    public ICollection<ProBusinessMembership>? BusinessMemberships { get; set; }
    public ICollection<PaymentMethod>? PaymentMethods { get; set; }
    // KYC Documents
    [StringLength(500)]
    public string? AadhaarDocumentPath { get; set; }

    [StringLength(500)]
    public string? PanDocumentPath { get; set; }

    [StringLength(20)]
    public string KycStatus { get; set; } = "None"; // None | Submitted | Approved | Rejected

    public DateTime? KycSubmittedAt { get; set; }

    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public bool IsProfileComplete { get; set; } = false;
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
