using ServiceProviderAPI.Services.Abstractions;

namespace ServiceProviderAPI.Services.Providers;

/// <summary>
/// Development-only stand-in for a real payment gateway. Lets the full payment
/// flow (create order → pay → verify → payout) be exercised locally without
/// valid Razorpay credentials or network access.
///
/// Enabled via "Payment:UseMockProvider": true (set only in appsettings.Development.json).
/// The synthetic Key "rzp_test_mock" signals the Angular checkout to skip the
/// real Razorpay widget and complete the payment directly. NEVER enable in production.
/// </summary>
public class MockPaymentProvider : IPaymentProvider
{
    public const string MockKey = "rzp_test_mock";

    private readonly ILogger<MockPaymentProvider> _logger;

    public string ProviderName => "Mock";

    public MockPaymentProvider(ILogger<MockPaymentProvider> logger)
    {
        _logger = logger;
        _logger.LogWarning("⚠️  MockPaymentProvider is active — payments are simulated, no real money moves. Development only.");
    }

    public Task<PaymentOrderResponse> CreateOrderAsync(
        int jobId, int bidId, decimal amount, string consumerName, string consumerEmail, string consumerPhone)
    {
        var orderId = $"order_mock_{Guid.NewGuid():N}";
        _logger.LogInformation("Mock order created: {OrderId} for Job:{JobId}, Amount:₹{Amount}", orderId, jobId, amount);

        return Task.FromResult(new PaymentOrderResponse
        {
            OrderId = orderId,
            Amount = amount,
            Currency = "INR",
            Key = MockKey,
            Metadata = new Dictionary<string, object> { { "jobId", jobId }, { "bidId", bidId }, { "mock", true } }
        });
    }

    // Any non-empty payment/signature for a mock order is accepted.
    public Task<bool> VerifyPaymentAsync(string orderId, string paymentId, string signature)
    {
        var ok = !string.IsNullOrEmpty(orderId) && !string.IsNullOrEmpty(paymentId);
        _logger.LogInformation("Mock verify Order:{OrderId} Payment:{PaymentId} → {Ok}", orderId, paymentId, ok);
        return Task.FromResult(ok);
    }

    public Task<string?> ProcessRefundAsync(string orderId, string paymentId, decimal amount, string reason)
        => Task.FromResult<string?>($"rfnd_mock_{Guid.NewGuid():N}");

    public Task<PaymentStatus> GetPaymentStatusAsync(string paymentId)
        => Task.FromResult(PaymentStatus.Completed);

    public Task<string?> CreateOrGetContactAsync(int proId, string name, string email, string phone)
        => Task.FromResult<string?>($"cont_mock_{proId}");

    public Task<string?> CreateFundAccountAsync(
        string contactId, string accountType, string accountHolderName, string? accountNumber, string? ifsc, string? vpa)
        => Task.FromResult<string?>($"fa_mock_{Guid.NewGuid():N}");

    public Task<string?> InitiatePayoutAsync(
        string fundAccountId, decimal amount, string mode, string purpose, string referenceId)
        => Task.FromResult<string?>($"pout_mock_{Guid.NewGuid():N}");

    public Task<RazorpayPayoutStatus> GetRazorpayPayoutStatusAsync(string payoutId)
        => Task.FromResult(RazorpayPayoutStatus.Processed);
}
