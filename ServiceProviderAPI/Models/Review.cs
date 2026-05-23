using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServiceProviderAPI.Models;

public class Review
{
    public int Id { get; set; }

    [Required]
    public int JobId { get; set; }
    public Job? Job { get; set; }

    [Required]
    public int ReviewerId { get; set; }
    public User? Reviewer { get; set; }

    [Required]
    public int ProId { get; set; }
    public Pro? Pro { get; set; }

    [Required, Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
