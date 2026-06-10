using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class Address
{
    public int Id { get; set; }

    [Required]
    [StringLength(10)]
    public string AddressType { get; set; } = string.Empty; // "User", "Pro", "Job"

    [StringLength(100)]
    public string? HouseNameNumber { get; set; }

    [StringLength(255)]
    public string? Street1 { get; set; }

    [StringLength(255)]
    public string? Street2 { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? District { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    [StringLength(20)]
    public string? ZipPostalCode { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
