using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.DTOs;

public class CreateMaterialRequest
{
    [Required]
    public int ServiceCategoryId { get; set; }

    [Required]
    [StringLength(100)]
    public string? Name { get; set; }

    [StringLength(100)]
    public string? Brand { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}

public class UpdateMaterialRequest
{
    [StringLength(100)]
    public string? Name { get; set; }

    [StringLength(100)]
    public string? Brand { get; set; }

    public decimal? UnitPrice { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public bool? IsActive { get; set; }
}

public class MaterialDto
{
    public int Id { get; set; }
    public int ServiceCategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? Name { get; set; }
    public string? Brand { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
