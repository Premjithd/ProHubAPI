using System.ComponentModel.DataAnnotations;

namespace ServiceProviderAPI.Models;

public class Service
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; }
    
    [Required]
    public string Description { get; set; }
    
    public decimal Price { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public int ProId { get; set; }
    public Pro? Pro { get; set; }
    
    public int? ServiceCategoryId { get; set; }
    public ServiceCategory? ServiceCategory { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
