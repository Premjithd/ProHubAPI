using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class Business
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string BusinessName { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [Required]
    public int AddressId { get; set; }
    public Address Address { get; set; } = null!;

    // 'Active' | 'Suspended'
    [StringLength(20)]
    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProBusinessMembership>? Members { get; set; }
    public ICollection<Service>? Services { get; set; }
    public ICollection<PaymentMethod>? PaymentMethods { get; set; }
}
