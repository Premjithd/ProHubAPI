using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServiceProviderAPI.Models;

public class Message
{
    public int Id { get; set; }

    [Required]
    public int JobId { get; set; }

    [Required]
    public int SenderId { get; set; }  // User ID or Pro ID

    [Required]
    public int RecipientId { get; set; }  // Recipient User ID or Pro ID

    [Required]
    [StringLength(20)]
    public string? SenderType { get; set; }  // "User" or "Pro"

    [Required]
    [StringLength(1000)]
    public string? Content { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;

    public DateTime? ReadAt { get; set; }

    public int? MessageIndexId { get; set; }

    // Navigation properties
    [ForeignKey("JobId")]
    public virtual Job? Job { get; set; }

    [ForeignKey("MessageIndexId")]
    public virtual MessageIndex? MessageIndex { get; set; }
}
