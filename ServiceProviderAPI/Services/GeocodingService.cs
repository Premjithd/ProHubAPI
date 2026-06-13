using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServiceProviderAPI.Services;

/// <summary>
/// Resolves a postal address to coordinates via Nominatim, for use on the
/// write path (e.g. when a user/pro saves a manually-typed address). Best-effort:
/// returns null on any failure so callers can fall back gracefully. Shares
/// <see cref="NominatimThrottle"/> to honour Nominatim's 1 req/sec policy.
/// This is independent of the admin geocode-backfill, which is left unchanged.
/// </summary>
public interface IGeocodingService
{
    Task<(double Lat, double Lon)?> TryGeocodeAsync(
        string? houseNumber, string? street, string? city, string? state, string? country,
        CancellationToken ct = default);
}

public class GeocodingService : IGeocodingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NominatimThrottle _throttle;
    private readonly ILogger<GeocodingService> _logger;

    public GeocodingService(IHttpClientFactory httpClientFactory, NominatimThrottle throttle, ILogger<GeocodingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _throttle = throttle;
        _logger = logger;
    }

    public async Task<(double Lat, double Lon)?> TryGeocodeAsync(
        string? houseNumber, string? street, string? city, string? state, string? country,
        CancellationToken ct = default)
    {
        // Progressively simpler queries so a fake/generic street number doesn't block city-level resolution.
        var candidates = new[]
        {
            Parts(houseNumber, street, city, state, country),
            Parts(street, city, state, country),
            Parts(city, state, country),
        }.Where(q => !string.IsNullOrWhiteSpace(q)).Distinct();

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ProHub-AddressService/1.0");

        foreach (var query in candidates)
        {
            try
            {
                // Respect the global 1 req/sec policy before hitting the network.
                await _throttle.WaitAsync(ct);

                var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query!)}&format=json&limit=1";
                var json = await httpClient.GetStringAsync(url, ct);
                var results = JsonSerializer.Deserialize<NominatimGeoResult[]>(json);

                if (results is { Length: > 0 } &&
                    double.TryParse(results[0].Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
                    double.TryParse(results[0].Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                {
                    return (lat, lon);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Inline geocoding query '{Query}' failed: {Error}", query, ex.Message);
            }
        }

        return null;
    }

    private static string Parts(params string?[] parts) =>
        string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private record NominatimGeoResult(
        [property: JsonPropertyName("lat")] string Lat,
        [property: JsonPropertyName("lon")] string Lon);
}
