using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class Pro
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string ProName { get; set; }
    
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    
    public string? PasswordHash { get; set; }
    
    [Phone]
    public string PhoneNumber { get; set; }
    
    [Required]
    public string BusinessName { get; set; }
    
    // Address Fields
    [StringLength(100)]
    public string? HouseNameNumber { get; set; }
    
    [StringLength(255)]
    public string? Street1 { get; set; }
    
    [StringLength(255)]
    public string? Street2 { get; set; }
    
    [StringLength(100)]
    public string? City { get; set; }
    
    [StringLength(100)]
    public string? State { get; set; }
    
    [StringLength(100)]
    public string? Country { get; set; }
    
    [StringLength(20)]
    public string? ZipPostalCode { get; set; }
    
    public ICollection<Service>? Services { get; set; }
    public ICollection<ProUser>? ProUsers { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
