using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServiceProviderAPI.Models;

public class JobInsurance
{
    public int Id { get; set; }

    [Required]
    public int JobId { get; set; }

    [StringLength(100)]
    public string? ProviderId { get; set; }  // e.g., "pending", "policybazaar", "redcrescent", etc.

    [StringLength(100)]
    public string? CoverageType { get; set; }  // e.g., "basic", "premium", "comprehensive"

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }  // Coverage amount in Indian Rupees (₹)

    [StringLength(20)]
    public string? Status { get; set; } = "Pending";  // "Pending", "Active", "Claimed", "Completed", "Cancelled"

    [StringLength(200)]
    public string? PolicyNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }  // When coverage expires

    public DateTime? UpdatedAt { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    // Navigation properties
    [ForeignKey("JobId")]
    public Job? Job { get; set; }
}
