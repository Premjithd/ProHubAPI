using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class ServiceArea
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Country { get; set; } = string.Empty;

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(100)]
    public string? District { get; set; }

    [StringLength(20)]
    public string? PinCode { get; set; }

    public bool IsActive { get; set; } = true;

    // True when this entry was auto-created during pro registration
    public bool IsAutoEnrolled { get; set; } = false;

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
