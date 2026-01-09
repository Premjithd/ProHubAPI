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
}
