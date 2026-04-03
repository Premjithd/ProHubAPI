using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.DTOs;

public class CreateJobRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200)]
    public string? Title { get; set; }

    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Description is required")]
    public string? Description { get; set; }

    // Service Address Fields (Structured)
    [StringLength(100)]
    public string? ServiceAddressHouse { get; set; }

    [StringLength(255)]
    public string? ServiceAddressStreet1 { get; set; }

    [StringLength(255)]
    public string? ServiceAddressStreet2 { get; set; }

    [Required(ErrorMessage = "City is required")]
    [StringLength(100)]
    public string? ServiceAddressCity { get; set; }

    [Required(ErrorMessage = "State is required")]
    [StringLength(100)]
    public string? ServiceAddressState { get; set; }

    [Required(ErrorMessage = "Country is required")]
    [StringLength(100)]
    public string? ServiceAddressCountry { get; set; }

    [StringLength(20)]
    public string? ServiceAddressPIN { get; set; }

    // Contact Person
    [Required(ErrorMessage = "Contact person name is required")]
    [StringLength(100)]
    public string? ContactPersonName { get; set; }

    [Required(ErrorMessage = "Contact person phone is required")]
    [Phone]
    public string? ContactPersonPhone { get; set; }

    // Budget (New: Decimal INR)
    [Required(ErrorMessage = "Budget is required")]
    public decimal EstimatedBudget { get; set; }  // In Indian Rupees (₹)

    [Required(ErrorMessage = "Timeline is required")]
    [StringLength(50)]
    public string? Timeline { get; set; }

    [StringLength(500)]
    public string? Attachments { get; set; }  // JSON array
}

public class UpdateJobRequest
{
    [StringLength(200)]
    public string? Title { get; set; }

    public int? CategoryId { get; set; }

    public string? Description { get; set; }

    // Service Address Fields
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

    // Contact Person
    [StringLength(100)]
    public string? ContactPersonName { get; set; }

    [Phone]
    public string? ContactPersonPhone { get; set; }

    // Budget
    public decimal? EstimatedBudget { get; set; }

    [StringLength(50)]
    public string? Timeline { get; set; }

    [StringLength(500)]
    public string? Attachments { get; set; }
}

public class JobDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? Title { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? Description { get; set; }

    // Backward compat: old location field
    public string? Location { get; set; }

    // Structured Service Address
    public string? ServiceAddressHouse { get; set; }
    public string? ServiceAddressStreet1 { get; set; }
    public string? ServiceAddressStreet2 { get; set; }
    public string? ServiceAddressCity { get; set; }
    public string? ServiceAddressState { get; set; }
    public string? ServiceAddressCountry { get; set; }
    public string? ServiceAddressPIN { get; set; }

    // Contact Person
    public string? ContactPersonName { get; set; }
    public string? ContactPersonPhone { get; set; }

    // Geolocation
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Budget (INR)
    public decimal? EstimatedBudget { get; set; }

    // Backward compat: old budget field
    public string? Budget { get; set; }

    public string? Timeline { get; set; }
    public string? Attachments { get; set; }
    public string? Status { get; set; }
    public bool IsBid { get; set; }
    public int? AssignedProId { get; set; }
    public string? JobPhases { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
