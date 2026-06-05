using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;
using ServiceProviderAPI.Services.Abstractions;

namespace ServiceProviderAPI.Services;

public interface IPayoutService
{
    /// <summary>
    /// Creates a Payout record for a completed job payment and immediately attempts
    /// to initiate the Razorpay payout if the pro has bank details configured.
    /// If bank details are missing the payout stays Pending.
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
        // Guard: don't double-create
        var existing = await _context.Payouts
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
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

        if (!HasBankDetails(pro))
        {
            _logger.LogInformation("Pro:{ProId} has no bank details — payout {PayoutId} stays Pending", proId, payout.Id);
            await _notificationService.NotifyAsync(
                pro.Email,
                pro.PhoneNumber,
                "Action required: set up bank details to receive your payout",
                $"Hi {pro.ProName}, your payment of ₹{amount:F2} for a completed job is ready. " +
                "Please log in and add your bank account or UPI details in your profile to receive your payout.");
            return payout;
        }

        await SendToRazorpayAsync(payout, pro);
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

        if (payout.Pro == null || !HasBankDetails(payout.Pro))
        {
            _logger.LogWarning("Payout:{PayoutId} — pro has no bank details", payoutId);
            return false;
        }

        return await SendToRazorpayAsync(payout, payout.Pro);
    }

    private async Task<bool> SendToRazorpayAsync(Payout payout, Pro pro)
    {
        // Ensure Razorpay contact exists
        if (string.IsNullOrEmpty(pro.RazorpayContactId))
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

            pro.RazorpayContactId = contactId;
            await _context.SaveChangesAsync();
        }

        // Ensure fund account exists
        if (string.IsNullOrEmpty(pro.RazorpayFundAccountId))
        {
            var (accountType, holderName, accountNumber, ifsc, vpa) = GetBankParams(pro);
            var fundAccountId = await _paymentProvider.CreateFundAccountAsync(
                pro.RazorpayContactId!, accountType, holderName, accountNumber, ifsc, vpa);

            if (fundAccountId == null)
            {
                _logger.LogError("Failed to create Razorpay fund account for Pro:{ProId}", pro.Id);
                payout.Status = "Failed";
                payout.FailureReason = "Could not register bank/UPI with Razorpay";
                payout.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return false;
            }

            pro.RazorpayFundAccountId = fundAccountId;
            await _context.SaveChangesAsync();
        }

        var mode = pro.PayoutMethod == "UPI" ? "UPI" : "IMPS";
        var razorpayPayoutId = await _paymentProvider.InitiatePayoutAsync(
            pro.RazorpayFundAccountId!,
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
                pro.Email,
                pro.PhoneNumber,
                $"Payout failed — Job #{payout.JobId}",
                $"Hi {pro.ProName}, your payout of ₹{payout.Amount:F2} could not be processed. " +
                "Please verify your bank details in your profile or contact support.");
            return false;
        }

        payout.Status = "Processing";
        payout.Mode = mode;
        payout.RazorpayPayoutId = razorpayPayoutId;
        payout.RazorpayFundAccountId = pro.RazorpayFundAccountId;
        payout.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Payout:{PayoutId} initiated — RazorpayId:{RazorpayPayoutId}", payout.Id, razorpayPayoutId);

        await _notificationService.NotifyAsync(
            pro.Email,
            pro.PhoneNumber,
            $"Payout initiated — ₹{payout.Amount:F2} on its way",
            $"Hi {pro.ProName}, your payout of ₹{payout.Amount:F2} for job #{payout.JobId} " +
            $"has been initiated via {mode}. Funds typically arrive within 1–2 business days.");
        return true;
    }

    private static bool HasBankDetails(Pro pro) =>
        pro.PayoutMethod == "UPI"
            ? !string.IsNullOrWhiteSpace(pro.UpiVpa)
            : !string.IsNullOrWhiteSpace(pro.BankAccountNumber) && !string.IsNullOrWhiteSpace(pro.BankIfsc);

    private static (string accountType, string holderName, string? accountNumber, string? ifsc, string? vpa)
        GetBankParams(Pro pro)
    {
        if (pro.PayoutMethod == "UPI")
            return ("vpa", pro.ProName, null, null, pro.UpiVpa);

        return ("bank_account",
            pro.BankAccountHolderName ?? pro.ProName,
            pro.BankAccountNumber,
            pro.BankIfsc,
            null);
    }
}
