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
}

public class CreatePaymentRequest
{
    [Required]
    public int JobId { get; set; }

    [Required]
    public int BidId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
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
