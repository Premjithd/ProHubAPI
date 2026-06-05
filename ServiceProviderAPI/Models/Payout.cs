using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServiceProviderAPI.Models;

public class Payout
{
    public int Id { get; set; }

    [Required]
    public int ProId { get; set; }

    [Required]
    public int PaymentId { get; set; }

    [Required]
    public int JobId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    // Pending → Processing → Processed | Failed | Reversed
    [StringLength(20)]
    public string Status { get; set; } = "Pending";

    // NEFT, IMPS, UPI
    [StringLength(20)]
    public string? Mode { get; set; }

    [StringLength(100)]
    public string? RazorpayPayoutId { get; set; }

    [StringLength(100)]
    public string? RazorpayFundAccountId { get; set; }

    [StringLength(500)]
    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("ProId")]
    public Pro? Pro { get; set; }

    [ForeignKey("PaymentId")]
    public Payment? Payment { get; set; }

    [ForeignKey("JobId")]
    public Job? Job { get; set; }
}
