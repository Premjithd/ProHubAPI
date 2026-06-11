using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class ProRegistrationStep1Request
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

    [StringLength(100)]
    public string? BusinessName { get; set; }
}

public class ProRegistrationStep2Request
{
    [Required]
    [StringLength(100)]
    public string HouseNameNumber { get; set; }

    [Required]
    [StringLength(255)]
    public string Street1 { get; set; }

    [StringLength(255)]
    public string? Street2 { get; set; }

    [Required]
    [StringLength(100)]
    public string City { get; set; }

    [StringLength(100)]
    public string? District { get; set; }

    [Required]
    [StringLength(100)]
    public string State { get; set; }

    [Required]
    [StringLength(100)]
    public string Country { get; set; }

    [Required]
    [StringLength(20)]
    public string ZipPostalCode { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Optional: link to a pre-registered Business
    public int? BusinessId { get; set; }
}

public class UserRegistrationStep1Request
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

public class UserRegistrationStep2Request
{
    [Required]
    [StringLength(100)]
    public string HouseNameNumber { get; set; }

    [Required]
    [StringLength(255)]
    public string Street1 { get; set; }

    [StringLength(255)]
    public string? Street2 { get; set; }

    [Required]
    [StringLength(100)]
    public string City { get; set; }

    [StringLength(100)]
    public string? District { get; set; }

    [Required]
    [StringLength(100)]
    public string State { get; set; }

    [Required]
    [StringLength(100)]
    public string Country { get; set; }

    [Required]
    [StringLength(20)]
    public string ZipPostalCode { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
