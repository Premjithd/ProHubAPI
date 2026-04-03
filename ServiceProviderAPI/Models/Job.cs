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
    public string? Location { get; set; }  // Kept for backward compatibility

    // Structured Service Address (NEW)
    [StringLength(100)]
    public string? ServiceAddressHouse { get; set; }

    [StringLength(255)]
    public string? ServiceAddressStreet1 { get; set; }

    [StringLength(255)]
    public string? ServiceAddressStreet2 { get; set; }

    [StringLength(100)]
    public string? ServiceAddressCity { get; set; }

    [StringLength(100)]
    public string? ServiceAddressState { get; set; }

    [StringLength(100)]
    public string? ServiceAddressCountry { get; set; }

    [StringLength(20)]
    public string? ServiceAddressPIN { get; set; }

    // Contact Person for Service Request (NEW)
    [StringLength(100)]
    public string? ContactPersonName { get; set; }

    [Phone]
    public string? ContactPersonPhone { get; set; }

    // Geolocation (NEW)
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    // Budget (UPDATED to decimal INR)
    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedBudget { get; set; }  // In Indian Rupees (₹)

    [Required]
    [StringLength(50)]
    public string? Budget { get; set; }  // Kept for backward compatibility, will be deprecated

    [Required]
    [StringLength(50)]
    public string? Timeline { get; set; }  // e.g., "asap", "1-week", "1-month", "flexible"

    [StringLength(500)]
    public string? Attachments { get; set; }  // JSON array of file URLs

    [StringLength(20)]
    public string? Status { get; set; } = "Open";  // "Open", "Bid Accepted", "Payment Made", "Pro Confirmed", "In Progress", "Completion Submitted", "Completed", "Cancelled"

    public bool IsBid { get; set; } = false;  // True if job has received at least one bid

    public int? AssignedProId { get; set; }  // ID of the Pro assigned to this job

    [StringLength(5000)]
    public string? JobPhases { get; set; }  // JSON array of phases with completion status

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey("UserId")]
    public User? User { get; set; }

    [ForeignKey("AssignedProId")]
    public Pro? AssignedPro { get; set; }

    [ForeignKey("CategoryId")]
    public ServiceCategory? Category { get; set; }
}
