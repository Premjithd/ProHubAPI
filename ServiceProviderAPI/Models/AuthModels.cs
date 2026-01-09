using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public string Password { get; set; }
}

public class LoginResponse
{
    public string Token { get; set; }
    public string Role { get; set; }
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}
