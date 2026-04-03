using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServiceProviderAPI.Models;

public class Material
{
    public int Id { get; set; }

    [Required]
    public int ServiceCategoryId { get; set; }

    [Required]
    [StringLength(100)]
    public string? Name { get; set; }

    [StringLength(100)]
    public string? Brand { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }  // Price in Indian Rupees (₹)

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey("ServiceCategoryId")]
    public ServiceCategory? Category { get; set; }
}
