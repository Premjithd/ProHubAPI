namespace ServiceProviderAPI.Models;

public class ProUser
{
    public int ProId { get; set; }
    public Pro Pro { get; set; }
    
    public int UserId { get; set; }
    public User User { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string Status { get; set; } = "Active";
}
