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
    public string? Location { get; set; }

    [Required]
    [StringLength(50)]
    public string? Budget { get; set; }  // e.g., "under-100", "100-250", etc.

    [Required]
    [StringLength(50)]
    public string? Timeline { get; set; }  // e.g., "asap", "1-week", "1-month", "flexible"

    [StringLength(500)]
    public string? Attachments { get; set; }  // JSON array of file URLs

    [StringLength(20)]
    public string? Status { get; set; } = "Open";  // "Open", "In Progress", "Completed", "Cancelled"

    public bool IsBid { get; set; } = false;  // True if job has received at least one bid

    public int? AssignedProId { get; set; }  // ID of the Pro assigned to this job

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
