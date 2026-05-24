namespace ServiceProviderAPI.Models;

public class RefreshToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string SubjectType { get; set; } = string.Empty; // "User", "Pro", "Admin"
    public bool IsRevoked { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}
