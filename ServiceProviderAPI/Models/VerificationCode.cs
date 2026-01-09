namespace ServiceProviderAPI.Models;

public class VerificationCode
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public string Type { get; set; } // "Email" or "Phone"
    public string UserType { get; set; } // "User" or "Pro"
}
