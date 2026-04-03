using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServiceProviderAPI.Models;

public class JobCompletion
{
    public int Id { get; set; }

    [Required]
    public int JobId { get; set; }

    [StringLength(2000)]
    public string? CompletionNotes { get; set; }

    // JSON array of file IDs for completion photos
    [StringLength(2000)]
    public string? CompletionPhotoIds { get; set; }

    // JSON array of file IDs for receipt photos
    [StringLength(2000)]
    public string? ReceiptPhotoIds { get; set; }

    public bool VerifiedByConsumer { get; set; } = false;

    [StringLength(20)]
    public string? Status { get; set; } = "Submitted";  // "Submitted", "Verified", "Disputed", "Refunded"

    [StringLength(500)]
    public string? DisputeReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime? DisputedAt { get; set; }

    // Navigation properties
    [ForeignKey("JobId")]
    public Job? Job { get; set; }
}
