using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServiceProviderAPI.Models;

public class Job
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [StringLength(200)]
    public string? Title { get; set; }

    public int? CategoryId { get; set; }  // Foreign key to ServiceCategory

    [Required]
    public string? Description { get; set; }

    [Required]
    [StringLength(150)]
    public string? Location { get; set; }  // Kept for backward compatibility (populated from City)

    // Contact Person for Service Request
    [StringLength(100)]
    public string? ContactPersonName { get; set; }

    [Phone]
    public string? ContactPersonPhone { get; set; }

    // Budget (UPDATED to decimal INR)
    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedBudget { get; set; }  // In Indian Rupees (₹)

    [Required]
    [StringLength(50)]
    public string? Budget { get; set; }  // Kept for backward compatibility

    [Required]
    [StringLength(50)]
    public string? Timeline { get; set; }  // e.g., "asap", "1-week", "1-month", "flexible"

    [StringLength(500)]
    public string? Attachments { get; set; }  // JSON array of file URLs

    [StringLength(20)]
    public string? Status { get; set; } = "Open";

    public bool IsBid { get; set; } = false;

    public int? AssignedProId { get; set; }

    [StringLength(5000)]
    public string? JobPhases { get; set; }  // JSON array of phases with completion status

    // Service address (normalized to Addresses table)
    public int? ServiceAddressId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey("UserId")]
    public User? User { get; set; }

    [ForeignKey("AssignedProId")]
    public Pro? AssignedPro { get; set; }

    [ForeignKey("CategoryId")]
    public ServiceCategory? Category { get; set; }

    [ForeignKey("ServiceAddressId")]
    public Address? ServiceAddress { get; set; }
}
