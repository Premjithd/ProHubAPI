using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.DTOs;

public class PaymentDto
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public int? BidId { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public decimal PlatformFee { get; set; }
    public decimal ProPayout { get; set; }
    public string? Status { get; set; }
    public string? RazorpayOrderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public decimal? RefundAmount { get; set; }
    public string? RefundReason { get; set; }
}

public class CreatePaymentRequest
{
    [Required]
    public int JobId { get; set; }

    [Required]
    public int BidId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }  // Full agreed bid amount (context)

    // Principal portion the consumer is paying now. Defaults to the full Amount when omitted (legacy).
    [Range(0.0, double.MaxValue)]
    public decimal PrincipalAmount { get; set; }
}

// ── Pro-raised payment requests + per-job payment tracking ──────────────────

public class CreatePaymentRequestRequest
{
    [Required]
    public int JobId { get; set; }

    [Required]
    [StringLength(20)]
    public string RequestType { get; set; } = "Partial";  // "None" | "Partial" | "Full"

    [Range(0.0, double.MaxValue)]
    public decimal RequestedAmount { get; set; }  // principal; ignored for None/Full

    [Range(0, 100)]
    public decimal MinPercent { get; set; }  // floor % of requested amount (Partial only)

    [StringLength(500)]
    public string? Note { get; set; }
}

public class PaymentRequestDto
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public int? BidId { get; set; }
    public int ProId { get; set; }
    public string RequestType { get; set; } = "Partial";
    public decimal RequestedAmount { get; set; }
    public decimal MinPercent { get; set; }
    public decimal MinAmount { get; set; }  // computed floor in ₹ (RequestedAmount * MinPercent / 100, capped at remaining)
    public string Status { get; set; } = "Pending";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FulfilledAt { get; set; }
}

public class PaymentHistoryItemDto
{
    public int Id { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal Amount { get; set; }
    public decimal PlatformFee { get; set; }
    public decimal ProPayout { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class PaymentSummaryDto
{
    public int JobId { get; set; }
    public decimal BidAmount { get; set; }            // agreed total (accepted bid)
    public decimal TotalPaidPrincipal { get; set; }   // sum of completed payments' principal
    public decimal Remaining { get; set; }            // BidAmount - TotalPaidPrincipal
    public bool IsFullyPaid { get; set; }
    public List<PaymentHistoryItemDto> Payments { get; set; } = new();
    public PaymentRequestDto? ActiveRequest { get; set; }
}

public class VerifyPaymentRequest
{
    [Required]
    public string? RazorpayOrderId { get; set; }

    [Required]
    public string? RazorpayPaymentId { get; set; }

    [Required]
    public string? RazorpaySignature { get; set; }
}
