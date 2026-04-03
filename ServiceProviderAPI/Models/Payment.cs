using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServiceProviderAPI.Models;

public class Payment
{
    public int Id { get; set; }

    [Required]
    public int JobId { get; set; }

    public int? BidId { get; set; }

    [Required]
    public int UserId { get; set; }  // Consumer who made the payment

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }  // Total amount paid

    [Column(TypeName = "decimal(18,2)")]
    public decimal PlatformFee { get; set; }  // Platform's commission

    [Column(TypeName = "decimal(18,2)")]
    public decimal ProPayout { get; set; }  // Amount pro receives

    [StringLength(100)]
    public string? ProviderId { get; set; }  // e.g., "razorpay_order_xyz", for payment provider reference

    [StringLength(100)]
    public string? RazorpayOrderId { get; set; }  // Razorpay order ID

    [StringLength(100)]
    public string? RazorpayPaymentId { get; set; }  // Razorpay payment ID

    [StringLength(20)]
    public string? Status { get; set; } = "Pending";  // "Pending", "Completed", "Failed", "Refunded"

    [StringLength(500)]
    public string? FailureReason { get; set; }  // Reason for payment failure/refund

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    // Navigation properties
    [ForeignKey("JobId")]
    public Job? Job { get; set; }

    [ForeignKey("BidId")]
    public JobBid? Bid { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }
}
