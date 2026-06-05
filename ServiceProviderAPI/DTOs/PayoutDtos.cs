using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.DTOs;

public class PayoutDto
{
    public int Id { get; set; }
    public int ProId { get; set; }
    public string? ProName { get; set; }
    public int PaymentId { get; set; }
    public int JobId { get; set; }
    public string? JobTitle { get; set; }
    public decimal Amount { get; set; }
    public string? Status { get; set; }
    public string? Mode { get; set; }
    public string? RazorpayPayoutId { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public class ProBankDetailsDto
{
    public string? PayoutMethod { get; set; }
    public string? BankAccountHolderName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankIfsc { get; set; }
    public string? UpiVpa { get; set; }
    public bool HasBankDetails { get; set; }
}

public class UpdateBankDetailsRequest
{
    [Required]
    public string PayoutMethod { get; set; } = string.Empty; // "Bank" or "UPI"

    public string? BankAccountHolderName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankIfsc { get; set; }
    public string? UpiVpa { get; set; }
}
