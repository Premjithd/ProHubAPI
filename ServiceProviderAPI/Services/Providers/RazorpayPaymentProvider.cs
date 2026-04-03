using ServiceProviderAPI.Services.Abstractions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ServiceProviderAPI.Services.Providers;

/// <summary>
/// Payment provider implementation using Razorpay
/// Phase 1C implementation - default payment provider for India
/// Uses Razorpay REST API for order creation and payment verification
/// </summary>
public class RazorpayPaymentProvider : IPaymentProvider
{
    private readonly ILogger<RazorpayPaymentProvider> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _keyId;
    private readonly string _keySecret;
    private readonly string _apiBaseUrl = "https://api.razorpay.com/v1";

    public string ProviderName => "Razorpay";

    public RazorpayPaymentProvider(
        ILogger<RazorpayPaymentProvider> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;

        _keyId = configuration.GetSection("Payment:Razorpay:KeyId").Value ?? "";
        _keySecret = configuration.GetSection("Payment:Razorpay:KeySecret").Value ?? "";

        if (string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_keySecret))
        {
            _logger.LogWarning("Razorpay credentials not configured. Payment functionality disabled.");
        }
        else
        {
            _logger.LogInformation($"Razorpay configured with Key ID: {_keyId[..10]}...");
        }
    }

    public async Task<PaymentOrderResponse> CreateOrderAsync(
        int jobId,
        int bidId,
        decimal amount,
        string consumerName,
        string consumerEmail,
        string consumerPhone)
    {
        try
        {
            if (string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_keySecret))
            {
                _logger.LogError("Razorpay not configured");
                return new PaymentOrderResponse { Amount = amount };
            }

            _logger.LogInformation($"Creating Razorpay order for Job:{jobId}, Bid:{bidId}, Amount:₹{amount}");

            // Razorpay API expects amount in paisa (smallest unit)
            var amountInPaisa = (long)(amount * 100);

            // Prepare request body
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("amount", amountInPaisa.ToString()),
                new KeyValuePair<string, string>("currency", "INR"),
                new KeyValuePair<string, string>("receipt", $"job_{jobId}_bid_{bidId}"),
                new KeyValuePair<string, string>("notes[jobId]", jobId.ToString()),
                new KeyValuePair<string, string>("notes[bidId]", bidId.ToString()),
                new KeyValuePair<string, string>("notes[consumerName]", consumerName),
                new KeyValuePair<string, string>("notes[consumerEmail]", consumerEmail)
            });

            // Create authorization header
            var authString = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_keyId}:{_keySecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);

            // Call Razorpay API
            var response = await _httpClient.PostAsync($"{_apiBaseUrl}/orders", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Razorpay API error ({response.StatusCode}): {errorContent}");
                return new PaymentOrderResponse { Amount = amount };
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
            {
                var root = doc.RootElement;
                var orderId = root.GetProperty("id").GetString();
                var createdAt = root.GetProperty("created_at").GetInt64();

                _logger.LogInformation($"Order created successfully: {orderId}");

                var paymentResponse = new PaymentOrderResponse
                {
                    OrderId = orderId,
                    Amount = amount,
                    Currency = "INR",
                    Key = _keyId,
                    Metadata = new Dictionary<string, object>
                    {
                        { "jobId", jobId },
                        { "bidId", bidId },
                        { "consumerName", consumerName },
                        { "consumerEmail", consumerEmail },
                        { "createdAt", createdAt }
                    }
                };

                return paymentResponse;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating Razorpay order: {ex.Message}");
            return new PaymentOrderResponse { Amount = amount };
        }
    }

    public async Task<bool> VerifyPaymentAsync(string orderId, string paymentId, string signature)
    {
        try
        {
            if (string.IsNullOrEmpty(_keySecret))
            {
                _logger.LogError("Razorpay secret not configured");
                return false;
            }

            _logger.LogInformation($"Verifying Razorpay payment: Order:{orderId}, Payment:{paymentId}");

            // Verify signature: HMAC-SHA256(orderId|paymentId, keySecret) == signature
            var verifyInput = $"{orderId}|{paymentId}";
            var hash = ComputeHmacSha256(verifyInput, _keySecret);

            var isValid = hash.Equals(signature, StringComparison.OrdinalIgnoreCase);

            if (isValid)
            {
                _logger.LogInformation($"Payment verified successfully: {paymentId}");
            }
            else
            {
                _logger.LogWarning($"Payment verification failed: {paymentId}");
            }

            return await Task.FromResult(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error verifying payment: {ex.Message}");
            return false;
        }
    }

    public async Task<string?> ProcessRefundAsync(
        string orderId,
        string paymentId,
        decimal amount,
        string reason)
    {
        try
        {
            if (string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_keySecret))
            {
                _logger.LogError("Razorpay not configured");
                return null;
            }

            _logger.LogInformation($"Processing refund: Payment:{paymentId}, Amount:₹{amount}, Reason:{reason}");

            // TODO: Implement actual Razorpay refund API call
            // var client = new RazorpayClient(_keyId, _keySecret);
            // var refund = client.Payment.Refund(paymentId, new Dictionary<string, object> { ... });

            var refundId = $"refund_{Guid.NewGuid().ToString().Substring(0, 8)}";
            _logger.LogInformation($"Refund initiated: {refundId}");
            return await Task.FromResult(refundId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing refund: {ex.Message}");
            return null;
        }
    }

    public async Task<PaymentStatus> GetPaymentStatusAsync(string paymentId)
    {
        try
        {
            _logger.LogInformation($"Fetching payment status: {paymentId}");

            // TODO: Implement actual Razorpay API call to get payment details
            // For now, return Unknown
            return await Task.FromResult(PaymentStatus.Unknown);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching payment status: {ex.Message}");
            return PaymentStatus.Unknown;
        }
    }

    private string ComputeHmacSha256(string input, string key)
    {
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
        {
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}
