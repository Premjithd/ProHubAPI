using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;
using ServiceProviderAPI.Services.Abstractions;

namespace ServiceProviderAPI.Services;

public interface IPayoutService
{
    /// <summary>
    /// Creates a Payout record for a completed job payment and immediately attempts
    /// to initiate the Razorpay payout if the pro has a payment method configured.
    /// If no payment method exists the payout stays Pending.
    /// </summary>
    Task<Payout> CreateAndProcessPayoutAsync(int paymentId, int jobId, int proId, decimal amount);

    /// <summary>
    /// Attempts to send a Pending payout to Razorpay (called when pro sets up bank details
    /// or when an admin manually triggers a retry).
    /// </summary>
    Task<bool> ProcessPendingPayoutAsync(int payoutId);
}

public class PayoutService : IPayoutService
{
    private readonly ApplicationDbContext _context;
    private readonly IPaymentProvider _paymentProvider;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PayoutService> _logger;

    public PayoutService(
        ApplicationDbContext context,
        IPaymentProvider paymentProvider,
        INotificationService notificationService,
        ILogger<PayoutService> logger)
    {
        _context = context;
        _paymentProvider = paymentProvider;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Payout> CreateAndProcessPayoutAsync(int paymentId, int jobId, int proId, decimal amount)
    {
        var existing = await _context.Payouts.FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        if (existing != null)
        {
            _logger.LogWarning("Payout already exists for Payment:{PaymentId} — skipping", paymentId);
            return existing;
        }

        var payout = new Payout
        {
            ProId = proId,
            PaymentId = paymentId,
            JobId = jobId,
            Amount = amount,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Payouts.Add(payout);
        await _context.SaveChangesAsync();

        var pro = await _context.Pros.FindAsync(proId);
        if (pro == null)
        {
            _logger.LogError("Pro:{ProId} not found when creating payout", proId);
            return payout;
        }

        var paymentMethod = await GetDefaultPaymentMethodAsync(proId);
        if (paymentMethod == null || !HasPayoutDetails(paymentMethod))
        {
            _logger.LogInformation("Pro:{ProId} has no payment method — payout {PayoutId} stays Pending", proId, payout.Id);
            await _notificationService.NotifyAsync(
                pro.Email,
                pro.PhoneNumber,
                "Action required: set up bank details to receive your payout",
                $"Hi {pro.ProName}, your payment of ₹{amount:F2} for a completed job is ready. " +
                "Please log in and add your bank account or UPI details in your profile to receive your payout.");
            return payout;
        }

        await SendToRazorpayAsync(payout, pro, paymentMethod);
        return payout;
    }

    public async Task<bool> ProcessPendingPayoutAsync(int payoutId)
    {
        var payout = await _context.Payouts
            .Include(p => p.Pro)
            .FirstOrDefaultAsync(p => p.Id == payoutId);

        if (payout == null)
        {
            _logger.LogWarning("Payout:{PayoutId} not found", payoutId);
            return false;
        }

        if (payout.Status != "Pending" && payout.Status != "Failed")
        {
            _logger.LogWarning("Payout:{PayoutId} is {Status} — cannot process", payoutId, payout.Status);
            return false;
        }

        if (payout.Pro == null) return false;

        var paymentMethod = await GetDefaultPaymentMethodAsync(payout.ProId);
        if (paymentMethod == null || !HasPayoutDetails(paymentMethod))
        {
            _logger.LogWarning("Payout:{PayoutId} — pro has no payment method set up", payoutId);
            return false;
        }

        return await SendToRazorpayAsync(payout, payout.Pro, paymentMethod);
    }

    private async Task<PaymentMethod?> GetDefaultPaymentMethodAsync(int proId) =>
        await _context.PaymentMethods
            .Where(pm => pm.ProId == proId && pm.IsDefault)
            .FirstOrDefaultAsync()
        ?? await _context.PaymentMethods
            .Where(pm => pm.ProId == proId)
            .OrderBy(pm => pm.CreatedAt)
            .FirstOrDefaultAsync();

    private async Task<bool> SendToRazorpayAsync(Payout payout, Pro pro, PaymentMethod pm)
    {
        if (string.IsNullOrEmpty(pm.RazorpayContactId))
        {
            var contactId = await _paymentProvider.CreateOrGetContactAsync(
                pro.Id, pro.ProName, pro.Email, pro.PhoneNumber);

            if (contactId == null)
            {
                _logger.LogError("Failed to create Razorpay contact for Pro:{ProId}", pro.Id);
                payout.Status = "Failed";
                payout.FailureReason = "Could not create Razorpay contact";
                payout.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return false;
            }

            pm.RazorpayContactId = contactId;
            await _context.SaveChangesAsync();
        }

        if (string.IsNullOrEmpty(pm.RazorpayFundAccountId))
        {
            var (accountType, holderName, accountNumber, ifsc, vpa) = GetBankParams(pm, pro.ProName);
            var fundAccountId = await _paymentProvider.CreateFundAccountAsync(
                pm.RazorpayContactId!, accountType, holderName, accountNumber, ifsc, vpa);

            if (fundAccountId == null)
            {
                _logger.LogError("Failed to create Razorpay fund account for Pro:{ProId}", pro.Id);
                payout.Status = "Failed";
                payout.FailureReason = "Could not register bank/UPI with Razorpay";
                payout.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return false;
            }

            pm.RazorpayFundAccountId = fundAccountId;
            await _context.SaveChangesAsync();
        }

        var mode = pm.Type == "UPI" ? "UPI" : "IMPS";
        var razorpayPayoutId = await _paymentProvider.InitiatePayoutAsync(
            pm.RazorpayFundAccountId!,
            payout.Amount,
            mode,
            "payout",
            $"payout_{payout.Id}");

        if (razorpayPayoutId == null)
        {
            _logger.LogError("Razorpay payout initiation failed for Payout:{PayoutId}", payout.Id);
            payout.Status = "Failed";
            payout.FailureReason = "Razorpay payout initiation failed";
            payout.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _notificationService.NotifyAsync(
                pro.Email, pro.PhoneNumber,
                $"Payout failed — Job #{payout.JobId}",
                $"Hi {pro.ProName}, your payout of ₹{payout.Amount:F2} could not be processed. " +
                "Please verify your bank details in your profile or contact support.");
            return false;
        }

        payout.Status = "Processing";
        payout.Mode = mode;
        payout.RazorpayPayoutId = razorpayPayoutId;
        payout.RazorpayFundAccountId = pm.RazorpayFundAccountId;
        payout.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Payout:{PayoutId} initiated — RazorpayId:{RazorpayPayoutId}", payout.Id, razorpayPayoutId);

        await _notificationService.NotifyAsync(
            pro.Email, pro.PhoneNumber,
            $"Payout initiated — ₹{payout.Amount:F2} on its way",
            $"Hi {pro.ProName}, your payout of ₹{payout.Amount:F2} for job #{payout.JobId} " +
            $"has been initiated via {mode}. Funds typically arrive within 1–2 business days.");
        return true;
    }

    private static bool HasPayoutDetails(PaymentMethod pm) =>
        pm.Type == "UPI"
            ? !string.IsNullOrWhiteSpace(pm.UpiVpa)
            : !string.IsNullOrWhiteSpace(pm.BankAccountNumber) && !string.IsNullOrWhiteSpace(pm.BankIfsc);

    private static (string accountType, string holderName, string? accountNumber, string? ifsc, string? vpa)
        GetBankParams(PaymentMethod pm, string proName)
    {
        if (pm.Type == "UPI")
            return ("vpa", pm.BankAccountHolderName ?? proName, null, null, pm.UpiVpa);

        return ("bank_account",
            pm.BankAccountHolderName ?? proName,
            pm.BankAccountNumber,
            pm.BankIfsc,
            null);
    }
}
