using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using ServiceProviderAPI.Services;

namespace ServiceProviderAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AddressController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AddressController> _logger;
        private readonly IMemoryCache _cache;
        private readonly NominatimThrottle _throttle;

        public AddressController(HttpClient httpClient, ILogger<AddressController> logger, IMemoryCache cache, NominatimThrottle throttle)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
            _throttle = throttle;
        }

        /// <summary>
        /// Get address predictions from Nominatim API via backend proxy
        /// This solves CORS issues with direct browser requests
        /// </summary>
        /// <param name="query">Address search query (minimum 3 characters)</param>
        /// <param name="countryCode">Country code filter (optional, default: us)</param>
        /// <returns>List of address predictions</returns>
        [HttpGet("search")]
        public async Task<IActionResult> SearchAddresses(
            [FromQuery] string query,
            [FromQuery] string countryCode = "us")
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
                {
                    return BadRequest(new { message = "Query must be at least 3 characters long" });
                }

                // Prevent abuse - query length limit
                if (query.Length > 255)
                {
                    return BadRequest(new { message = "Query too long" });
                }

                var cacheKey = $"addr_search:{countryCode}:{query.ToLowerInvariant().Trim()}";
                if (_cache.TryGetValue(cacheKey, out object? cached))
                {
                    return Ok(cached);
                }

                // Honour Nominatim's 1 req/sec policy before hitting the network
                await _throttle.WaitAsync(HttpContext.RequestAborted);

                // Build Nominatim API URL
                var url = $"https://nominatim.openstreetmap.org/search?" +
                    $"q={Uri.EscapeDataString(query)}" +
                    $"&format=json" +
                    $"&addressdetails=1" +
                    $"&countrycodes={Uri.EscapeDataString(countryCode ?? "us")}" +
                    $"&limit=10";

                // Set timeout - Nominatim can be slow, use 30 seconds
                _httpClient.Timeout = TimeSpan.FromSeconds(30);

                // Add User-Agent header (required by Nominatim)
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "ProHub-AddressService/1.0");

                // Make request to Nominatim
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<object>(content);

                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
                _logger.LogInformation("Address search completed for query: {Query}", query);

                // Return the raw JSON from Nominatim
                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"Error calling Nominatim API: {ex.Message}");
                return StatusCode(502, new { message = "Error calling address service", error = ex.Message });
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError($"Address service timeout: {ex.Message}");
                return StatusCode(504, new { message = "Address service timeout" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error in address search: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        /// <summary>
        /// Look up address fields from a postal/PIN code (worldwide).
        /// Results are cached for 24 h — postcode boundaries don't change.
        ///
        /// Strategy:
        ///   1. Nominatim fallback chain (full → no-spaces → prefix)
        ///   2. Zippopotam.us for Canadian FSA codes (letter-digit-letter) if Nominatim returns nothing
        /// </summary>
        [HttpGet("by-postcode")]
        public async Task<IActionResult> GetAddressByPostcode(
            [FromQuery] string code,
            [FromQuery] string? countryCode = null)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Trim().Length < 3)
                return BadRequest(new { message = "Postal code must be at least 3 characters" });

            if (code.Length > 20)
                return BadRequest(new { message = "Postal code too long" });

            var normalised = code.Trim().ToLowerInvariant();
            var cacheKey = $"addr_postcode:{countryCode?.ToLowerInvariant()}:{normalised}";
            if (_cache.TryGetValue(cacheKey, out object? cached))
                return Ok(cached);

            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            try
            {
                // ── 1. Nominatim fallback chain ──────────────────────────────────────
                var candidates = new List<string> { code.Trim() };

                var noSpaces = code.Replace(" ", "").Trim();
                if (noSpaces != code.Trim())
                    candidates.Add(noSpaces);

                // Prefix before first space: "V5X" from "V5X 3S5", "SW1A" from "SW1A 2AA"
                // When prefix is all letters (state label like "NSW"), try the suffix instead: "2000" from "NSW 2000"
                var spaceIdx = code.Trim().IndexOf(' ');
                if (spaceIdx > 2)
                {
                    var prefix = code.Trim()[..spaceIdx];
                    if (prefix.All(char.IsLetter))
                    {
                        // "NSW 2000" → try "2000"; "ACT 2600" → try "2600"
                        var suffix = code.Trim()[(spaceIdx + 1)..].Trim();
                        if (suffix.Length >= 3)
                            candidates.Add(suffix);
                    }
                    else
                    {
                        candidates.Add(prefix);
                    }
                }

                object? result = null;

                foreach (var candidate in candidates)
                {
                    await _throttle.WaitAsync(HttpContext.RequestAborted);

                    var url = $"https://nominatim.openstreetmap.org/search?" +
                        $"postalcode={Uri.EscapeDataString(candidate)}" +
                        $"&format=json&addressdetails=1&limit=1";

                    if (!string.IsNullOrWhiteSpace(countryCode))
                        url += $"&countrycodes={Uri.EscapeDataString(countryCode)}";

                    var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Add("User-Agent", "ProHub-AddressService/1.0");

                    var resp = await _httpClient.SendAsync(req, HttpContext.RequestAborted);
                    resp.EnsureSuccessStatusCode();

                    var content = await resp.Content.ReadAsStringAsync();
                    var hits = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement[]>(content);

                    if (hits != null && hits.Length > 0)
                    {
                        result = hits[0];
                        _logger.LogInformation("Postcode lookup: {Code} (Nominatim, candidate '{C}')", code, candidate);
                        break;
                    }
                }

                // ── 2. Zippopotam.us for Canadian postal codes ───────────────────────
                // Canadian FSAs are uniquely letter-digit-letter (e.g. V5X).
                // Nominatim has no Canadian postcode data; Zippopotam covers all FSAs.
                if (result == null)
                {
                    var fsa = (spaceIdx > 2 ? code.Trim()[..spaceIdx] : code.Trim()[..Math.Min(3, code.Trim().Length)]).ToUpperInvariant();
                    if (fsa.Length == 3 && char.IsLetter(fsa[0]) && char.IsDigit(fsa[1]) && char.IsLetter(fsa[2]))
                    {
                        result = await TryZippopotamUsAsync(fsa, code.Trim(), HttpContext.RequestAborted);
                        if (result != null)
                            _logger.LogInformation("Postcode lookup: {Code} (Zippopotam.us, FSA '{F}')", code, fsa);
                    }
                }

                if (result == null)
                    return Ok(null);

                _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError("Nominatim postcode error {Code}: {Msg}", code, ex.Message);
                return StatusCode(502, new { message = "Error calling address service" });
            }
            catch (TaskCanceledException)
            {
                return StatusCode(504, new { message = "Address service timeout" });
            }
            catch (Exception ex)
            {
                _logger.LogError("Postcode lookup failed {Code}: {Msg}", code, ex.Message);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Calls Zippopotam.us and returns a Nominatim-compatible object so the frontend
        /// address parser works unchanged.
        /// </summary>
        private async Task<object?> TryZippopotamUsAsync(string fsa, string originalCode, CancellationToken ct)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.zippopotam.us/CA/{fsa}");
                req.Headers.Add("User-Agent", "ProHub-AddressService/1.0");

                var resp = await _httpClient.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) return null;

                using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                var root = doc.RootElement;

                if (!root.TryGetProperty("places", out var places) || places.GetArrayLength() == 0)
                    return null;

                var place = places[0];
                var placeName  = place.TryGetProperty("place name",  out var pn) ? pn.GetString() ?? "" : "";
                var state      = place.TryGetProperty("state",        out var st) ? st.GetString() ?? "" : "";
                var lat        = place.TryGetProperty("latitude",     out var la) ? la.GetString() ?? "" : "";
                var lon        = place.TryGetProperty("longitude",    out var lo) ? lo.GetString() ?? "" : "";

                // Strip parenthetical neighbourhood suffix: "Vancouver (SE Oakridge)" → "Vancouver"
                var city = placeName.Contains('(') ? placeName[..placeName.IndexOf('(')].Trim() : placeName;

                // Return Nominatim-shaped object — frontend parseNominatimAddress handles it unchanged
                return new
                {
                    place_id = 0,
                    display_name = $"{city}, {state}, Canada",
                    address = new
                    {
                        city,
                        state,
                        country = "Canada",
                        country_code = "ca",
                        postcode = originalCode
                    },
                    lat,
                    lon
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Zippopotam.us failed for FSA {Fsa}: {Msg}", fsa, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Get detailed address information for a place
        /// </summary>
        /// <param name="placeId">Nominatim place ID</param>
        /// <returns>Detailed address information</returns>
        [HttpGet("details")]
        public async Task<IActionResult> GetAddressDetails([FromQuery] string placeId)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(placeId))
                {
                    return BadRequest(new { message = "Place ID is required" });
                }

                // Nominatim /search does not accept place_id — use the dedicated
                // details endpoint which looks up by place_id directly.
                var url = $"https://nominatim.openstreetmap.org/details?" +
                    $"place_id={Uri.EscapeDataString(placeId)}" +
                    $"&format=json" +
                    $"&addressdetails=1";

                // Set timeout - Nominatim can be slow, use 30 seconds
                _httpClient.Timeout = TimeSpan.FromSeconds(30);

                // Add User-Agent header
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "ProHub-AddressService/1.0");

                // Make request to Nominatim
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                
                _logger.LogInformation($"Address details retrieved for place ID: {placeId}");

                // Return the raw JSON from Nominatim
                return Ok(System.Text.Json.JsonSerializer.Deserialize<object>(content));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"Error calling Nominatim API: {ex.Message}");
                return StatusCode(502, new { message = "Error calling address service", error = ex.Message });
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError($"Address service timeout: {ex.Message}");
                return StatusCode(504, new { message = "Address service timeout" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error in address details: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }
    }
}
