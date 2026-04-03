using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServiceProviderAPI.Models;

public class JobBid
{
    public int Id { get; set; }

    [Required]
    public int JobId { get; set; }

    [Required]
    public int ProId { get; set; }

    [StringLength(1000)]
    public string? BidMessage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? BidAmount { get; set; }

    // Enhanced Quote Fields (NEW)
    public DateTime? CommenceDate { get; set; }  // Expected date work will start

    public int? ExpectedDurationDays { get; set; }  // Expected number of days to complete

    [StringLength(2000)]
    public string? MaterialsDescription { get; set; }  // Materials/brand selection details

    public DateTime? ExpiresAt { get; set; }  // Quote expiry deadline

    [StringLength(20)]
    public string? Status { get; set; } = "Pending";  // "Pending", "Accepted", "Rejected", "Withdrawn", "Expired"

    public bool IsMessageExchange { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey("JobId")]
    public Job? Job { get; set; }

    [ForeignKey("ProId")]
    public Pro? Pro { get; set; }
}
