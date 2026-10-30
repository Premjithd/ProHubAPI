using ServiceProviderAPI.Services;

namespace ServiceProviderAPI.Tests;

/// <summary>
/// Unit tests for <see cref="GeocodingService.ReconcileCoordinates"/> — the pure decision
/// that keeps a saved address's coordinates consistent with its typed city/PIN.
/// </summary>
public class GeocodingReconcileTests
{
    // New Delhi (matches a "New Delhi / 110001" address) vs a point ~far away.
    private static readonly (double Lat, double Lon) NewDelhi = (28.6139, 77.2090);
    private static readonly (double Lat, double Lon) FarAway = (41.7669, 123.4155); // NE Asia — the Ashok bug

    [Fact]
    public void NoSuppliedCoordinates_UsesGeocoded()
    {
        var result = GeocodingService.ReconcileCoordinates(null, null, NewDelhi);
        Assert.Equal(NewDelhi, result);
    }

    [Fact]
    public void SuppliedCoordinates_ButGeocodeFailed_KeepsSupplied()
    {
        var result = GeocodingService.ReconcileCoordinates(FarAway.Lat, FarAway.Lon, geocoded: null);
        Assert.Equal(FarAway, result);
    }

    [Fact]
    public void SuppliedContradictsAddress_UsesAddressDerived()
    {
        // Supplied point is thousands of km from the geocoded address → override with geocoded.
        var result = GeocodingService.ReconcileCoordinates(FarAway.Lat, FarAway.Lon, NewDelhi);
        Assert.Equal(NewDelhi, result);
    }

    [Fact]
    public void SuppliedConsistentWithAddress_KeepsSupplied()
    {
        // A precise address point a few km from the city-centroid geocode is within tolerance → keep it.
        var precise = (Lat: 28.6500, Lon: 77.2300); // ~5 km from the NewDelhi centroid
        var result = GeocodingService.ReconcileCoordinates(precise.Lat, precise.Lon, NewDelhi);
        Assert.Equal(precise, result);
    }

    [Fact]
    public void NothingSuppliedAndNoGeocode_ReturnsNull()
    {
        var result = GeocodingService.ReconcileCoordinates(null, null, null);
        Assert.Null(result);
    }
}
