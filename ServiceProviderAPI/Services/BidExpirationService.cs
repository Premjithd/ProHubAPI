using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;

namespace ServiceProviderAPI.Services;

/// <summary>
/// Periodic background sweep that marks overdue "Pending" bids as "Expired".
/// Accept-time checks in <c>JobsController.AcceptBid</c> already reject an individual
/// expired bid, but this keeps the stored <c>JobBid.Status</c> column consistent so
/// pros and admins see accurate state without an accept attempt.
/// </summary>
public class BidExpirationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BidExpirationService> _logger;
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(15);

    public BidExpirationService(IServiceScopeFactory scopeFactory, ILogger<BidExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run an initial sweep right away, then repeat on a fixed interval until shutdown.
        using var timer = new PeriodicTimer(SweepInterval);
        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bid expiration sweep failed; will retry next interval");
            }
        }
        while (await WaitForNextTickSafeAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickSafeAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;
        var expired = await context.JobBids
            .Where(b => b.Status == "Pending" && b.ExpiresAt != null && b.ExpiresAt < now)
            .ToListAsync(ct);

        if (expired.Count == 0)
            return;

        foreach (var bid in expired)
        {
            bid.Status = "Expired";
            bid.UpdatedAt = now;
        }

        await context.SaveChangesAsync(ct);
        _logger.LogInformation("Bid expiration sweep marked {Count} overdue bid(s) as Expired", expired.Count);
    }
}
