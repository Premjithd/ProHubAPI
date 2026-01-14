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
    
    [Required]
    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }
    
    public int ProId { get; set; }
    public Pro? Pro { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
