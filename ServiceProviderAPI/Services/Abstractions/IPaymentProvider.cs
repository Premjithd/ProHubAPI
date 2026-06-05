using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Services.Abstractions;

/// <summary>
/// Abstraction for payment processing providers (Razorpay, CCAvenue, PayU, etc.)
/// Allows swapping providers without changing core business logic
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// Create a payment order
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="bidId">Job Bid ID</param>
    /// <param name="amount">Amount in Indian Rupees (₹)</param>
    /// <param name="consumerName">Consumer name for receipt</param>
    /// <param name="consumerEmail">Consumer email</param>
    /// <param name="consumerPhone">Consumer phone</param>
    /// <returns>Order object with OrderId and other details</returns>
    Task<PaymentOrderResponse> CreateOrderAsync(
        int jobId, 
        int bidId, 
        decimal amount, 
        string consumerName, 
        string consumerEmail, 
        string consumerPhone);

    /// <summary>
    /// Verify payment after successful transaction
    /// </summary>
    /// <param name="orderId">Original order ID returned by provider</param>
    /// <param name="paymentId">Payment ID from provider</param>
    /// <param name="signature">Signature for verification</param>
    /// <returns>True if payment is verified and valid</returns>
    Task<bool> VerifyPaymentAsync(string orderId, string paymentId, string signature);

    /// <summary>
    /// Process refund for a payment
    /// </summary>
    /// <param name="orderId">Original order ID</param>
    /// <param name="paymentId">Payment ID to refund</param>
    /// <param name="amount">Refund amount (partial refund if less than original)</param>
    /// <param name="reason">Reason for refund</param>
    /// <returns>Refund ID or null if failed</returns>
    Task<string?> ProcessRefundAsync(
        string orderId, 
        string paymentId, 
        decimal amount, 
        string reason);

    /// <summary>
    /// Get payment status
    /// </summary>
    /// <param name="paymentId">Payment ID from provider</param>
    /// <returns>Payment status (Pending, Completed, Failed, Refunded)</returns>
    Task<PaymentStatus> GetPaymentStatusAsync(string paymentId);

    /// <summary>
    /// Create a Razorpay contact for a pro (required before creating a fund account).
    /// Returns the contact ID or null on failure.
    /// </summary>
    Task<string?> CreateOrGetContactAsync(int proId, string name, string email, string phone);

    /// <summary>
    /// Register a bank account or UPI VPA as a fund account linked to a contact.
    /// accountType: "bank_account" or "vpa"
    /// Returns fund account ID or null on failure.
    /// </summary>
    Task<string?> CreateFundAccountAsync(
        string contactId,
        string accountType,
        string accountHolderName,
        string? accountNumber,
        string? ifsc,
        string? vpa);

    /// <summary>
    /// Initiate a payout to a registered fund account.
    /// mode: "NEFT", "IMPS", or "UPI"
    /// Returns payout ID or null on failure.
    /// </summary>
    Task<string?> InitiatePayoutAsync(
        string fundAccountId,
        decimal amount,
        string mode,
        string purpose,
        string referenceId);

    /// <summary>
    /// Get current status of a Razorpay payout.
    /// </summary>
    Task<RazorpayPayoutStatus> GetRazorpayPayoutStatusAsync(string payoutId);

    /// <summary>
    /// Provider name for logging/identification
    /// </summary>
    string ProviderName { get; }
}

/// <summary>
/// Response from CreateOrder
/// </summary>
public class PaymentOrderResponse
{
    public string? OrderId { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; } = "INR";
    public string? Key { get; set; }  // API key for client-side checkout
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Payment status enum
/// </summary>
public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Refunded,
    Unknown
}

/// <summary>
/// Razorpay payout status enum
/// </summary>
public enum RazorpayPayoutStatus
{
    Pending,
    Processing,
    Processed,
    Failed,
    Reversed,
    Unknown
}
