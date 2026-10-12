using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Services;

namespace ServiceProviderAPI.Tests;

/// <summary>
/// Unit tests for <see cref="RateSplitService"/>. The DB-backed path runs against an
/// EF Core in-memory database with an empty AppSettings table, exercising the default
/// commission configuration. The prorate path is pure and needs no database.
/// </summary>
public class RateSplitServiceTests
{
    private static RateSplitService NewService()
    {
        // Unique database name per service so tests are isolated.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ratesplit-{Guid.NewGuid()}")
            .Options;
        var context = new ApplicationDbContext(options);
        return new RateSplitService(context, NullLogger<RateSplitService>.Instance);
    }

    [Fact]
    public async Task CalculateSplit_WithDefaults_ComputesStandardSplit()
    {
        var svc = NewService();

        var split = await svc.CalculateSplitAsync(1000m);

        // Defaults: user 10%, pro 10%, GST 18%, min fee 10, max 20%.
        Assert.Equal(1000m, split.BidAmount);
        Assert.Equal(100m, split.UserCommission);          // 10% of 1000
        Assert.Equal(18m, split.GstOnUserCommission);      // 18% of 100
        Assert.Equal(1118m, split.TotalAmountUserPays);    // 1000 + 100 + 18
        Assert.Equal(100m, split.ProDeduction);            // 10% of 1000
        Assert.Equal(900m, split.ProPayout);               // 1000 - 100
        Assert.Equal(200m, split.TotalPlatformEarnings);   // 100 + 100
        Assert.Equal(11.8m, split.EffectiveUserChargePercent);
        Assert.Equal(90m, split.EffectiveProPayoutPercent);
    }

    [Fact]
    public async Task CalculateSplit_SmallBid_AppliesMinimumFee()
    {
        var svc = NewService();

        // Bid 50: raw user commission = 5, below the ₹10 min fee → bumped to 10
        // (still under the 20% cap of 10), so the min-fee branch is exercised.
        var split = await svc.CalculateSplitAsync(50m);

        Assert.Equal(10m, split.UserCommission);
        Assert.Equal(1.8m, split.GstOnUserCommission);     // 18% of 10
        Assert.Equal(61.8m, split.TotalAmountUserPays);    // 50 + 10 + 1.8
    }

    [Fact]
    public async Task CalculateSplit_TinyBid_CapsAtMaxPercent()
    {
        var svc = NewService();

        // Bid 20: raw = 2 → min-fee bumps to 10 → but 20% cap = 4, so Math.Min caps it at 4.
        // Exercises the max-commission branch.
        var split = await svc.CalculateSplitAsync(20m);

        Assert.Equal(4m, split.UserCommission);
    }

    [Fact]
    public void ProrateForPrincipal_HalfPrincipal_ScalesFeesByHalf()
    {
        var full = new RateSplit
        {
            BidAmount = 1000m,
            UserCommission = 100m,
            GstOnUserCommission = 18m,
            ProDeduction = 100m,
            UserCommissionPercent = 10m,
            ProCommissionPercent = 10m,
            GstPercent = 18m,
        };
        var svc = NewService();

        var half = svc.ProrateForPrincipal(full, 500m);

        Assert.Equal(500m, half.BidAmount);
        Assert.Equal(50m, half.UserCommission);
        Assert.Equal(9m, half.GstOnUserCommission);
        Assert.Equal(50m, half.ProDeduction);
        Assert.Equal(450m, half.ProPayout);                // 500 - 50
        Assert.Equal(559m, half.TotalAmountUserPays);      // 500 + 50 + 9
        Assert.Equal(100m, half.TotalPlatformEarnings);    // 50 + 50
        Assert.Equal(11.8m, half.EffectiveUserChargePercent);
        Assert.Equal(90m, half.EffectiveProPayoutPercent);
    }

    [Fact]
    public void ProrateForPrincipal_PartialsSumToFull()
    {
        var full = new RateSplit
        {
            BidAmount = 1000m,
            UserCommission = 100m,
            GstOnUserCommission = 18m,
            ProDeduction = 100m,
        };
        var svc = NewService();

        var a = svc.ProrateForPrincipal(full, 400m);
        var b = svc.ProrateForPrincipal(full, 600m);

        // The documented invariant: a series of partials sums exactly to the full split.
        Assert.Equal(full.UserCommission, a.UserCommission + b.UserCommission);
        Assert.Equal(full.GstOnUserCommission, a.GstOnUserCommission + b.GstOnUserCommission);
        Assert.Equal(full.ProDeduction, a.ProDeduction + b.ProDeduction);
        Assert.Equal(full.BidAmount, a.BidAmount + b.BidAmount);
    }

    [Fact]
    public void ProrateForPrincipal_ZeroBidAmount_Throws()
    {
        var full = new RateSplit { BidAmount = 0m };
        var svc = NewService();

        Assert.Throws<ArgumentException>(() => svc.ProrateForPrincipal(full, 100m));
    }
}
