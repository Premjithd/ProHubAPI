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

    /// <summary>
    /// Returns the coordinates to persist for a typed address, reconciling any client-supplied
    /// coordinates against the address text. If no coordinates are supplied, geocodes the address.
    /// If supplied coordinates are present but sit implausibly far from the geocoded address
    /// (i.e. they contradict the typed city/PIN — e.g. stale device GPS), the address-derived
    /// coordinates are used instead so location stays consistent with the postal fields.
    /// Falls back to the supplied coordinates when the address can't be geocoded to verify.
    /// </summary>
    Task<(double Lat, double Lon)?> ResolveCoordinatesAsync(
        double? suppliedLat, double? suppliedLon,
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

    // Supplied coordinates farther than this from the geocoded address are treated as
    // contradicting the typed city/PIN. Generous enough to allow a specific address vs a
    // city-centroid geocode, while still catching gross mismatches (wrong city/country).
    private const double MaxConsistentKm = 50d;

    public async Task<(double Lat, double Lon)?> ResolveCoordinatesAsync(
        double? suppliedLat, double? suppliedLon,
        string? houseNumber, string? street, string? city, string? state, string? country,
        CancellationToken ct = default)
    {
        var geocoded = await TryGeocodeAsync(houseNumber, street, city, state, country, ct);
        var chosen = ReconcileCoordinates(suppliedLat, suppliedLon, geocoded);

        // Log when supplied coordinates were overridden because they contradicted the address.
        if (suppliedLat.HasValue && chosen is not null &&
            (chosen.Value.Lat != suppliedLat.Value || chosen.Value.Lon != suppliedLon!.Value))
        {
            _logger.LogWarning(
                "Supplied coordinates ({Lat},{Lon}) contradict the typed address — using " +
                "address-derived coordinates ({GeoLat},{GeoLon}) to stay consistent with the postal fields.",
                suppliedLat, suppliedLon, chosen.Value.Lat, chosen.Value.Lon);
        }

        return chosen;
    }

    /// <summary>
    /// Pure reconciliation decision (no I/O). Returns the coordinates to keep given the
    /// client-supplied pair and the geocoded-from-address pair:
    /// no supplied → geocoded; no geocode to verify → supplied; supplied &gt; <see cref="MaxConsistentKm"/>
    /// from the address → geocoded (the supplied point contradicts the typed city/PIN); otherwise supplied.
    /// </summary>
    public static (double Lat, double Lon)? ReconcileCoordinates(
        double? suppliedLat, double? suppliedLon, (double Lat, double Lon)? geocoded)
    {
        if (!suppliedLat.HasValue || !suppliedLon.HasValue)
            return geocoded;
        if (geocoded is null)
            return (suppliedLat.Value, suppliedLon.Value);

        var distanceKm = HaversineKm(suppliedLat.Value, suppliedLon.Value, geocoded.Value.Lat, geocoded.Value.Lon);
        return distanceKm > MaxConsistentKm ? geocoded : (suppliedLat.Value, suppliedLon.Value);
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371d;
        double ToRad(double d) => d * Math.PI / 180d;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static string Parts(params string?[] parts) =>
        string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private record NominatimGeoResult(
        [property: JsonPropertyName("lat")] string Lat,
        [property: JsonPropertyName("lon")] string Lon);
}
