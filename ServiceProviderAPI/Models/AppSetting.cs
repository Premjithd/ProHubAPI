using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class AppSetting
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public required string Key { get; set; }

    [Required]
    public required string Value { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
