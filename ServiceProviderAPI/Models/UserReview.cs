using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class UserReview
{
    public int Id { get; set; }

    [Required]
    public int JobId { get; set; }
    public Job? Job { get; set; }

    [Required]
    public int ReviewerId { get; set; }   // Pro who wrote the review
    public Pro? Reviewer { get; set; }

    [Required]
    public int UserId { get; set; }       // User being reviewed
    public User? User { get; set; }

    [Required, Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
