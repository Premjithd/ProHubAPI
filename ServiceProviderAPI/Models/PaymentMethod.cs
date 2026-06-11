using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class PaymentMethod
{
    public int Id { get; set; }

    // Owner — exactly one of these is set
    public int? UserId { get; set; }
    public int? ProId { get; set; }
    public int? BusinessId { get; set; }

    // "UPI" | "Bank"
    [Required, StringLength(10)]
    public string Type { get; set; } = null!;

    // Friendly name the owner gives this method
    [StringLength(100)]
    public string? Label { get; set; }

    public bool IsDefault { get; set; } = false;

    // UPI
    [StringLength(100)]
    public string? UpiVpa { get; set; }

    // Bank
    [StringLength(100)]
    public string? BankAccountHolderName { get; set; }

    [StringLength(50)]
    public string? BankAccountNumber { get; set; }

    [StringLength(20)]
    public string? BankIfsc { get; set; }

    // Razorpay payout infra — stored here after first successful payout setup
    // Only relevant for Pro / Business payout methods
    [StringLength(100)]
    public string? RazorpayContactId { get; set; }

    [StringLength(100)]
    public string? RazorpayFundAccountId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
    public Pro? Pro { get; set; }
    public Business? Business { get; set; }
}
