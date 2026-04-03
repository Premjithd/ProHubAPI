using ServiceProviderAPI.Services.Abstractions;
using System.Net.Http;
using System.Web;

namespace ServiceProviderAPI.Services.Channels;

/// <summary>
/// SMS channel implementation using MSG91 API
/// Phase 1B implementation - can be swapped with Twilio later
/// </summary>
public class Msg91SmsChannel : ISmsChannel
{
    private readonly ILogger<Msg91SmsChannel> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _authKey;
    private readonly string _senderId;

    public string ChannelName => "MSG91 SMS";

    private const string Msg91BaseUrl = "https://api.msg91.com/app/sms/send/";

    public Msg91SmsChannel(
        ILogger<Msg91SmsChannel> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;

        _authKey = configuration.GetSection("SMS:MSG91:AuthKey").Value ?? "";
        _senderId = configuration.GetSection("SMS:MSG91:SenderId").Value ?? "ProHub";

        if (string.IsNullOrEmpty(_authKey))
        {
            _logger.LogWarning("MSG91 AuthKey not configured. SMS will not be sent.");
        }
    }

    public async Task<bool> SendAsync(string recipient, string subject, string body)
    {
        // For SMS, subject is ignored; body contains the message
        return await SendSmsAsync(recipient, body);
    }

    public async Task<bool> SendSmsAsync(string phoneNumber, string message)
    {
        try
        {
            if (string.IsNullOrEmpty(_authKey))
            {
                _logger.LogWarning($"SMS not configured. Would send to {phoneNumber}: {message}");
                return false;  // Fail gracefully if not configured
            }

            // Normalize phone number (ensure it starts with country code)
            if (!phoneNumber.StartsWith("+"))
            {
                if (!phoneNumber.StartsWith("91"))
                {
                    phoneNumber = "91" + phoneNumber.TrimStart('0');
                }
                phoneNumber = "+" + phoneNumber;
            }

            // Build MSG91 API request
            var parameters = new Dictionary<string, string>
            {
                { "authkey", _authKey },
                { "mobiles", phoneNumber.Replace("+", "") },  // MSG91 expects: 919876543210
                { "message", message },
                { "sender", _senderId },
                { "route", "2" },  // Route 2 for transactional SMS
                { "country", "91" }  // India
            };

            // Build query string
            var queryString = string.Join("&", parameters.Select(kvp => 
                $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}"));

            var url = Msg91BaseUrl + "?" + queryString;

            _logger.LogInformation($"Sending SMS via MSG91 to {phoneNumber}");

            using var response = await _httpClient.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && responseContent.Contains("success"))
            {
                _logger.LogInformation($"SMS sent successfully to {phoneNumber}");
                return true;
            }
            else
            {
                _logger.LogError($"MSG91 API error: {responseContent}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error sending SMS via MSG91: {ex.Message}");
            return false;
        }
    }
}
