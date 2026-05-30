using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Services;

public class ServiceAreaService : IServiceAreaService
{
    private readonly ApplicationDbContext _context;

    public ServiceAreaService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsCountryAllowedAsync(string country)
    {
        if (string.IsNullOrWhiteSpace(country)) return false;
        return await _context.ServiceAreas.AnyAsync(sa =>
            sa.IsActive &&
            sa.Country.ToLower() == country.ToLower().Trim());
    }

    // Strip trailing admin-level suffixes that geocoders append (e.g. "Thiruvananthapuram District").
    private static string NormalizeAdmin(string value) =>
        Regex.Replace(value.ToLower().Trim(), @"\s+(district|taluk|division|tehsil|block)$", "").Trim();

    public async Task<bool> IsInServiceAreaAsync(string country, string? state, string? district, string? pinCode)
    {
        if (string.IsNullOrWhiteSpace(country)) return false;

        var countryLower = country.ToLower().Trim();
        var areas = await _context.ServiceAreas
            .Where(sa => sa.IsActive && sa.Country.ToLower() == countryLower)
            .ToListAsync();

        if (!areas.Any()) return false;

        // No active areas defines → no restriction (open)
        // Check most-specific rule first, fall through to broader ones

        // PIN-level match (most specific)
        if (!string.IsNullOrWhiteSpace(pinCode))
        {
            var pin = pinCode.Trim();
            if (areas.Any(sa => !string.IsNullOrWhiteSpace(sa.PinCode) && sa.PinCode == pin))
                return true;
        }

        // District-level match
        if (!string.IsNullOrWhiteSpace(district))
        {
            var dist = NormalizeAdmin(district);
            if (areas.Any(sa =>
                    string.IsNullOrWhiteSpace(sa.PinCode) &&
                    !string.IsNullOrWhiteSpace(sa.District) &&
                    NormalizeAdmin(sa.District) == dist))
                return true;
        }

        // State-level match
        if (!string.IsNullOrWhiteSpace(state))
        {
            var st = NormalizeAdmin(state);
            if (areas.Any(sa =>
                    string.IsNullOrWhiteSpace(sa.PinCode) &&
                    string.IsNullOrWhiteSpace(sa.District) &&
                    !string.IsNullOrWhiteSpace(sa.State) &&
                    NormalizeAdmin(sa.State) == st))
                return true;
        }

        // Country-only match (broadest — covers the entire country)
        if (areas.Any(sa =>
                string.IsNullOrWhiteSpace(sa.State) &&
                string.IsNullOrWhiteSpace(sa.District) &&
                string.IsNullOrWhiteSpace(sa.PinCode)))
            return true;

        return false;
    }

    public async Task AutoEnrollProLocationAsync(string country, string? state, string? district, string? pinCode)
    {
        if (string.IsNullOrWhiteSpace(country)) return;
        if (!await IsCountryAllowedAsync(country)) return;

        // Check if this exact combination already exists (active or inactive)
        var pinNorm = string.IsNullOrWhiteSpace(pinCode) ? null : pinCode.Trim();
        var distNorm = string.IsNullOrWhiteSpace(district) ? null : district.Trim();
        var stateNorm = string.IsNullOrWhiteSpace(state) ? null : state.Trim();

        var exists = await _context.ServiceAreas.AnyAsync(sa =>
            sa.Country.ToLower() == country.ToLower().Trim() &&
            (pinNorm == null ? string.IsNullOrWhiteSpace(sa.PinCode) : sa.PinCode == pinNorm) &&
            (distNorm == null ? string.IsNullOrWhiteSpace(sa.District) : sa.District!.ToLower() == distNorm.ToLower()) &&
            (stateNorm == null ? string.IsNullOrWhiteSpace(sa.State) : sa.State!.ToLower() == stateNorm.ToLower()));

        if (exists) return;

        // Only auto-enroll if there's something more specific than just the country
        if (pinNorm == null && distNorm == null && stateNorm == null) return;

        _context.ServiceAreas.Add(new ServiceArea
        {
            Country = country.Trim(),
            State = stateNorm,
            District = distNorm,
            PinCode = pinNorm,
            IsActive = true,
            IsAutoEnrolled = true,
            Notes = "Auto-enrolled during pro registration",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    public async Task<List<ServiceArea>> GetAllAsync()
    {
        return await _context.ServiceAreas
            .OrderBy(sa => sa.Country)
            .ThenBy(sa => sa.State)
            .ThenBy(sa => sa.District)
            .ThenBy(sa => sa.PinCode)
            .ToListAsync();
    }

    public async Task<List<ServiceArea>> GetActiveAsync()
    {
        return await _context.ServiceAreas
            .Where(sa => sa.IsActive)
            .OrderBy(sa => sa.Country)
            .ThenBy(sa => sa.State)
            .ThenBy(sa => sa.District)
            .ThenBy(sa => sa.PinCode)
            .ToListAsync();
    }

    public async Task<ServiceArea> AddAsync(ServiceArea area)
    {
        area.CreatedAt = DateTime.UtcNow;
        area.UpdatedAt = null;
        _context.ServiceAreas.Add(area);
        await _context.SaveChangesAsync();
        return area;
    }

    public async Task<ServiceArea?> UpdateAsync(int id, ServiceArea area)
    {
        var existing = await _context.ServiceAreas.FindAsync(id);
        if (existing == null) return null;

        existing.Country = area.Country;
        existing.State = string.IsNullOrWhiteSpace(area.State) ? null : area.State.Trim();
        existing.District = string.IsNullOrWhiteSpace(area.District) ? null : area.District.Trim();
        existing.PinCode = string.IsNullOrWhiteSpace(area.PinCode) ? null : area.PinCode.Trim();
        existing.IsActive = area.IsActive;
        existing.Notes = area.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var area = await _context.ServiceAreas.FindAsync(id);
        if (area == null) return false;
        _context.ServiceAreas.Remove(area);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleActiveAsync(int id)
    {
        var area = await _context.ServiceAreas.FindAsync(id);
        if (area == null) return false;
        area.IsActive = !area.IsActive;
        area.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return area.IsActive;
    }
}
