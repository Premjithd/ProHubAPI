namespace ServiceProviderAPI.DTOs;

public class ServiceBrowseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public int ProId { get; set; }
    public string? ProName { get; set; }
    public string? BusinessName { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public int? ServiceCategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryIcon { get; set; }
}
