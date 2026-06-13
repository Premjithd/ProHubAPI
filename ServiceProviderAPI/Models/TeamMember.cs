using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

// A member shown in the "Our Team" section of the public /about page.
// Managed by admins; the section's visibility is gated by the
// "show_our_team" AppSetting.
public class TeamMember
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = null!;

    [StringLength(120)]
    public string? Role { get; set; }

    [StringLength(600)]
    public string? Bio { get; set; }

    // Short initials shown in the avatar circle (e.g. "JS").
    [StringLength(4)]
    public string? Initials { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
