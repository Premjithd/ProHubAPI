namespace ServiceProviderAPI.Models;

public class ProUserRelationship
{
    public int Id { get; set; }

    public int ProId { get; set; }
    public virtual Pro Pro { get; set; } = null!;

    public int? UserId { get; set; }
    public virtual User? User { get; set; }

    public string InviteEmail { get; set; } = string.Empty;
    public string? InviteToken { get; set; }
    public DateTime? InviteExpiresAt { get; set; }

    // Pending = invite sent, Active = user registered, Revoked = removed
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
