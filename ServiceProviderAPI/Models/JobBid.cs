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

    [StringLength(20)]
    public string? Status { get; set; } = "Pending";  // "Pending", "Accepted", "Rejected", "Withdrawn"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey("JobId")]
    public Job? Job { get; set; }

    [ForeignKey("ProId")]
    public Pro? Pro { get; set; }
}
