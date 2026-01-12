using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class UserRegistrationRequest
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; }

    [Required]
    [StringLength(100)]
    public string LastName { get; set; }
    
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    
    [Required]
    [MinLength(6)]
    public string Password { get; set; }
    
    [Phone]
    public string PhoneNumber { get; set; }
    
    // Optional Address Fields
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
}

public class ProRegistrationRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; }
    
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    
    [Required]
    [MinLength(6)]
    public string Password { get; set; }
    
    [Phone]
    public string PhoneNumber { get; set; }
    
    [Required]
    [StringLength(100)]
    public string BusinessName { get; set; }
    
    // Optional Address Fields
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
}
