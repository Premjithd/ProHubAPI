using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.DTOs;

public class UpdateUserRequest
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    // If provided, will be treated as plaintext and hashed
    public string? PasswordHash { get; set; }
    public string? PhoneNumber { get; set; }
    // Address fields
    public string? HouseNameNumber { get; set; }
    public string? Street1 { get; set; }
    public string? Street2 { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? ZipPostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? UpiVpa { get; set; }
}

public class UpdateProRequest
{
    public int Id { get; set; }
    public string? ProName { get; set; }
    public string? Email { get; set; }
    // If provided, will be treated as plaintext and hashed
    public string? PasswordHash { get; set; }
    public string? PhoneNumber { get; set; }
    public string? BusinessName { get; set; }
    // Address fields
    public string? HouseNameNumber { get; set; }
    public string? Street1 { get; set; }
    public string? Street2 { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? ZipPostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int ServiceRadiusKm { get; set; } = 25;
}
