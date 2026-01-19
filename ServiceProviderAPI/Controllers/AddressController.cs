using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace ServiceProviderAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AddressController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AddressController> _logger;

        public AddressController(HttpClient httpClient, ILogger<AddressController> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
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
                if (string.IsNullOrWhiteSpace(query) || query.Length < 6)
                {
                    return BadRequest(new { message = "Query must be at least 6 characters long" });
                }

                // Prevent abuse - query length limit
                if (query.Length > 255)
                {
                    return BadRequest(new { message = "Query too long" });
                }

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
                
                _logger.LogInformation($"Address search completed for query: {query}");

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
                _logger.LogError($"Unexpected error in address search: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
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

                // Build Nominatim API URL for details
                var url = $"https://nominatim.openstreetmap.org/search?" +
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
