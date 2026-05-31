using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.DTOs;

public class CreateReviewRequest
{
    [Required, Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }
}

public class ReviewDto
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public string? JobTitle { get; set; }
    public int ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public int ProId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProRatingSummary
{
    public int ProId { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int[] RatingBreakdown { get; set; } = new int[5]; // index 0 = 1-star count … index 4 = 5-star count
}

public class PlatformRatingStats
{
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
}

public class UserReviewDto
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public string? JobTitle { get; set; }
    public int ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public int UserId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserRatingSummary
{
    public int UserId { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int[] RatingBreakdown { get; set; } = new int[5]; // index 0 = 1-star … index 4 = 5-star
}
