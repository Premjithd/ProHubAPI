using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServiceProviderAPI.Models;

public class MessageIndex
{
    public int Id { get; set; }

    [Required]
    public int UserId1 { get; set; }  // First user ID (can be User or Pro ID)

    [Required]
    [StringLength(20)]
    public string? UserType1 { get; set; }  // "User" or "Pro"

    [Required]
    public int UserId2 { get; set; }  // Second user ID (can be User or Pro ID)

    [Required]
    [StringLength(20)]
    public string? UserType2 { get; set; }  // "User" or "Pro"

    [Required]
    public DateTime InitiatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastMessageAt { get; set; }

    // Foreign key relationship to Messages table
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
