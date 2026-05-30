using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Services;

public interface IServiceAreaService
{
    Task<bool> IsCountryAllowedAsync(string country);
    Task<bool> IsInServiceAreaAsync(string country, string? state, string? district, string? pinCode);
    Task AutoEnrollProLocationAsync(string country, string? state, string? district, string? pinCode);
    Task<List<ServiceArea>> GetAllAsync();
    Task<List<ServiceArea>> GetActiveAsync();
    Task<ServiceArea> AddAsync(ServiceArea area);
    Task<ServiceArea?> UpdateAsync(int id, ServiceArea area);
    Task<bool> DeleteAsync(int id);
    Task<bool> ToggleActiveAsync(int id);
}
