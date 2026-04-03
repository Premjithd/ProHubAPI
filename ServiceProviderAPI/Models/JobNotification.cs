using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServiceProviderAPI.Models;

public class JobNotification
{
    public int Id { get; set; }

    [Required]
    public int JobId { get; set; }

    [Required]
    public int ProId { get; set; }

    [StringLength(20)]
    public string? NotificationType { get; set; } = "JobPosted";  // "JobPosted", "JobUpdated", "Reminder"

    [StringLength(500)]
    public string? Message { get; set; }

    public bool IsRead { get; set; } = false;

    [StringLength(20)]
    public string? DeliveryStatus { get; set; } = "Pending";  // "Pending", "Sent", "Failed", "Bounced"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    // Navigation properties
    [ForeignKey("JobId")]
    public Job? Job { get; set; }

    [ForeignKey("ProId")]
    public Pro? Pro { get; set; }
}
