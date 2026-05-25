namespace ServiceProviderAPI.Models;

public class AdminUser
{
    public int Id { get; set; }

    public string Email { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string PasswordHash { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsEmailVerified { get; set; } = false;
    public bool IsPhoneVerified { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int? ProId { get; set; }
    public virtual Pro? Pro { get; set; }
    
    public int? UserId { get; set; }
    public virtual User? User { get; set; }
}
