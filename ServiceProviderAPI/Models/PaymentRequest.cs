using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServiceProviderAPI.Models;

/// <summary>
/// A payment ask raised by the assigned Pro after a bid is accepted.
/// Only one Pending request may exist per job at a time. The consumer pays the agreed
/// bid amount down across one or more Payment rows; this entity nudges them for a specific amount.
/// </summary>
public class PaymentRequest
{
    public int Id { get; set; }

    [Required]
    public int JobId { get; set; }

    public int? BidId { get; set; }

    [Required]
    public int ProId { get; set; }  // Pro who raised the request

    [StringLength(20)]
    public string RequestType { get; set; } = "Partial";  // "None", "Partial", "Full"

    // Principal amount requested (portion of the agreed bid). 0 for "None"; = remaining for "Full".
    [Column(TypeName = "decimal(18,2)")]
    public decimal RequestedAmount { get; set; }

    // Minimum percentage (0-100) of the requested amount the consumer must pay. Floor of flexibility.
    [Column(TypeName = "decimal(5,2)")]
    public decimal MinPercent { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = "Pending";  // "Pending", "Fulfilled", "Cancelled"

    [StringLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? FulfilledAt { get; set; }

    // Navigation properties
    [ForeignKey("JobId")]
    public Job? Job { get; set; }

    [ForeignKey("BidId")]
    public JobBid? Bid { get; set; }

    [ForeignKey("ProId")]
    public Pro? Pro { get; set; }
}
