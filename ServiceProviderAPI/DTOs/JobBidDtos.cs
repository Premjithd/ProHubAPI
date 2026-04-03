using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ServiceProviderAPI.DTOs;

public class CreateJobBidRequest
{
    [JsonPropertyName("message")]
    [StringLength(1000)]
    public string? BidMessage { get; set; }

    [JsonPropertyName("quotedPrice")]
    [Required(ErrorMessage = "Bid amount is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Bid amount must be greater than 0")]
    public decimal BidAmount { get; set; }  // In Indian Rupees (₹)

    // Enhanced Quote Fields
    [JsonPropertyName("commenceDate")]
    public DateTime? CommenceDate { get; set; }

    [JsonPropertyName("expectedDurationDays")]
    public int? ExpectedDurationDays { get; set; }

    [JsonPropertyName("materialsDescription")]
    [StringLength(2000)]
    public string? MaterialsDescription { get; set; }

    // Quote expiry date (optional - server will set to 30 days from now if not provided)
    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }
}

public class UpdateJobBidRequest
{
    [StringLength(1000)]
    public string? BidMessage { get; set; }

    public decimal? BidAmount { get; set; }

    public DateTime? CommenceDate { get; set; }

    public int? ExpectedDurationDays { get; set; }

    [StringLength(2000)]
    public string? MaterialsDescription { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

public class JobBidDto
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public int ProId { get; set; }
    public string? ProName { get; set; }
    public string? BusinessName { get; set; }

    [Display(Name = "Bid Message")]
    public string? BidMessage { get; set; }

    [Display(Name = "Amount (₹)")]
    public decimal? BidAmount { get; set; }

    [Display(Name = "Commencement Date")]
    public DateTime? CommenceDate { get; set; }

    [Display(Name = "Expected Duration (days)")]
    public int? ExpectedDurationDays { get; set; }

    [Display(Name = "Materials")]
    public string? MaterialsDescription { get; set; }

    [Display(Name = "Quote Expires")]
    public DateTime? ExpiresAt { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt;

    public string? Status { get; set; }
    public bool IsMessageExchange { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class BidAcceptRequest
{
    [Required]
    public int JobId { get; set; }

    [Required]
    public int BidId { get; set; }
}

public class BidRejectRequest
{
    [Required]
    public int BidId { get; set; }

    [StringLength(500)]
    public string? RejectionReason { get; set; }
}
