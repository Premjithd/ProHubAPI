using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class ServiceArea
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string? Name { get; set; }

    [Required]
    [StringLength(20)]
    public string? Type { get; set; }  // "City", "State", "Country"

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = false;  // Default to inactive; admins activate by phase

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
