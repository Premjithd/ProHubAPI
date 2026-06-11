namespace ServiceProviderAPI.Models;

public class ProBusinessMembership
{
    public int Id { get; set; }

    public int ProId { get; set; }
    public Pro Pro { get; set; } = null!;

    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    // 'Owner' | 'Member'
    public string Role { get; set; } = "Member";

    // 'Active' | 'Pending' | 'Revoked'
    public string Status { get; set; } = "Active";

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
