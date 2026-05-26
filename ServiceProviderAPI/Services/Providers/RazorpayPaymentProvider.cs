using ServiceProviderAPI.Services.Abstractions;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ServiceProviderAPI.Services.Providers;

/// <summary>
/// Razorpay implementation of IPaymentProvider.
/// All methods use per-request Authorization headers (thread-safe).
/// Razorpay REST API docs: https://razorpay.com/docs/api/
/// </summary>
public class RazorpayPaymentProvider : IPaymentProvider
{
    private readonly ILogger<RazorpayPaymentProvider> _logger;
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
        _httpClient = httpClient;

        _keyId = configuration["Payment:Razorpay:KeyId"] ?? "";
        _keySecret = configuration["Payment:Razorpay:KeySecret"] ?? "";

        if (string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_keySecret))
            _logger.LogWarning("Razorpay credentials not configured. Payment functionality disabled.");
        else
            _logger.LogInformation("Razorpay configured with Key ID: {KeyPrefix}...", _keyId[..Math.Min(10, _keyId.Length)]);
    }

    public async Task<PaymentOrderResponse> CreateOrderAsync(
        int jobId,
        int bidId,
        decimal amount,
        string consumerName,
        string consumerEmail,
        string consumerPhone)
    {
        if (string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_keySecret))
        {
            _logger.LogError("Razorpay not configured — cannot create order");
            return new PaymentOrderResponse { Amount = amount };
        }

        try
        {
            _logger.LogInformation("Creating Razorpay order for Job:{JobId}, Bid:{BidId}, Amount:₹{Amount}", jobId, bidId, amount);

            var amountInPaisa = (long)(amount * 100);
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

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiBaseUrl}/orders")
            {
                Headers = { Authorization = BuildBasicAuth() },
                Content = content
            };

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogError("Razorpay order creation failed ({StatusCode}): {Body}", response.StatusCode, err);
                return new PaymentOrderResponse { Amount = amount };
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var orderId = root.GetProperty("id").GetString();
            _logger.LogInformation("Razorpay order created: {OrderId}", orderId);

            return new PaymentOrderResponse
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
                    { "createdAt", root.GetProperty("created_at").GetInt64() }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Razorpay order for Job:{JobId}", jobId);
            return new PaymentOrderResponse { Amount = amount };
        }
    }

    public async Task<bool> VerifyPaymentAsync(string orderId, string paymentId, string signature)
    {
        if (string.IsNullOrEmpty(_keySecret))
        {
            _logger.LogError("Razorpay secret not configured — cannot verify payment");
            return false;
        }

        try
        {
            // Razorpay signature: HMAC-SHA256(orderId + "|" + paymentId, keySecret)
            var computed = ComputeHmacSha256($"{orderId}|{paymentId}", _keySecret);
            var isValid = computed.Equals(signature, StringComparison.OrdinalIgnoreCase);

            if (isValid)
                _logger.LogInformation("Payment verified: {PaymentId}", paymentId);
            else
                _logger.LogWarning("Payment verification failed: {PaymentId}", paymentId);

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment {PaymentId}", paymentId);
            return false;
        }
    }

    /// <summary>
    /// Calls POST /v1/payments/{paymentId}/refund.
    /// Returns the Razorpay refund ID (e.g. "rfnd_FP8QHiV938haTz") or null on failure.
    /// </summary>
    public async Task<string?> ProcessRefundAsync(
        string orderId,
        string paymentId,
        decimal amount,
        string reason)
    {
        if (string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_keySecret))
        {
            _logger.LogError("Razorpay not configured — cannot process refund");
            return null;
        }

        try
        {
            _logger.LogInformation("Processing refund: Payment:{PaymentId}, Amount:₹{Amount}", paymentId, amount);

            var amountInPaisa = (long)(amount * 100);
            var payload = JsonSerializer.Serialize(new
            {
                amount = amountInPaisa,
                notes = new { reason }
            });

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_apiBaseUrl}/payments/{paymentId}/refund")
            {
                Headers = { Authorization = BuildBasicAuth() },
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Razorpay refund failed ({StatusCode}): {Body}", response.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var refundId = doc.RootElement.GetProperty("id").GetString();

            _logger.LogInformation("Refund created: {RefundId} for Payment:{PaymentId}", refundId, paymentId);
            return refundId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund for Payment:{PaymentId}", paymentId);
            return null;
        }
    }

    /// <summary>
    /// Calls GET /v1/payments/{paymentId} to retrieve live payment status.
    /// </summary>
    public async Task<PaymentStatus> GetPaymentStatusAsync(string paymentId)
    {
        if (string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_keySecret))
        {
            _logger.LogWarning("Razorpay not configured — cannot fetch payment status");
            return PaymentStatus.Unknown;
        }

        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_apiBaseUrl}/payments/{paymentId}")
            {
                Headers = { Authorization = BuildBasicAuth() }
            };

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Razorpay payment status error ({StatusCode}): {Body}", response.StatusCode, body);
                return PaymentStatus.Unknown;
            }

            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.GetProperty("status").GetString();

            return status switch
            {
                "captured"   => PaymentStatus.Completed,
                "authorized" => PaymentStatus.Pending,
                "created"    => PaymentStatus.Pending,
                "failed"     => PaymentStatus.Failed,
                "refunded"   => PaymentStatus.Refunded,
                _            => PaymentStatus.Unknown
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching payment status for {PaymentId}", paymentId);
            return PaymentStatus.Unknown;
        }
    }

    private AuthenticationHeaderValue BuildBasicAuth() =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_keyId}:{_keySecret}")));

    private static string ComputeHmacSha256(string input, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(input)))
            .Replace("-", "").ToLower();
    }
}
