using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;

namespace ServiceProviderAPI.Services;

public interface IRateSplitService
{
    Task<RateSplit> CalculateSplitAsync(decimal bidAmount);
    Task<RateSplitConfig> GetConfigAsync();
}

public class RateSplitService : IRateSplitService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RateSplitService> _logger;

    private static readonly Dictionary<string, decimal> Defaults = new()
    {
        ["commission.user_percent"] = 10,
        ["commission.pro_percent"]  = 10,
        ["commission.gst_percent"]  = 18,
        ["commission.min_fee"]      = 10,
        ["commission.max_percent"]  = 20,
    };

    public RateSplitService(ApplicationDbContext context, ILogger<RateSplitService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    private async Task<RateSplitConfig> LoadConfigAsync()
    {
        var keys = Defaults.Keys.ToList();
        var rows = await _context.AppSettings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        decimal Get(string key) =>
            rows.TryGetValue(key, out var v) && decimal.TryParse(v, out var n) ? n : Defaults[key];

        return new RateSplitConfig
        {
            UserCommissionPercent = Get("commission.user_percent"),
            ProCommissionPercent  = Get("commission.pro_percent"),
            GstPercent            = Get("commission.gst_percent"),
            MinPlatformFee        = Get("commission.min_fee"),
            MaxCommissionPercent  = Get("commission.max_percent"),
        };
    }

    public async Task<RateSplitConfig> GetConfigAsync() => await LoadConfigAsync();

    public async Task<RateSplit> CalculateSplitAsync(decimal bidAmount)
    {
        var cfg = await LoadConfigAsync();

        // User-side: platform commission added on top of the bid amount
        var userCommission = (bidAmount * cfg.UserCommissionPercent) / 100;
        userCommission = Math.Max(userCommission, cfg.MinPlatformFee);
        userCommission = Math.Min(userCommission, (bidAmount * cfg.MaxCommissionPercent) / 100);

        var gstOnUserCommission = (userCommission * cfg.GstPercent) / 100;
        var totalAmountUserPays = bidAmount + userCommission + gstOnUserCommission;

        // Pro-side: commission deducted from what the pro receives
        var proDeduction = (bidAmount * cfg.ProCommissionPercent) / 100;
        var proPayout    = bidAmount - proDeduction;

        var split = new RateSplit
        {
            BidAmount              = bidAmount,
            UserCommission         = userCommission,
            GstOnUserCommission    = gstOnUserCommission,
            TotalAmountUserPays    = totalAmountUserPays,
            ProDeduction           = proDeduction,
            ProPayout              = proPayout,
            TotalPlatformEarnings  = userCommission + proDeduction,
            UserCommissionPercent  = cfg.UserCommissionPercent,
            ProCommissionPercent   = cfg.ProCommissionPercent,
            GstPercent             = cfg.GstPercent,
            EffectiveUserChargePercent = decimal.Round((totalAmountUserPays / bidAmount - 1) * 100, 2),
            EffectiveProPayoutPercent  = decimal.Round((proPayout / bidAmount) * 100, 2),
        };

        _logger.LogInformation(
            "Rate split: Bid=₹{Bid:F2}, UserPays=₹{UserPays:F2}, ProPayout=₹{ProPayout:F2}, PlatformEarns=₹{Platform:F2}",
            bidAmount, totalAmountUserPays, proPayout, split.TotalPlatformEarnings);

        return split;
    }
}

public class RateSplitConfig
{
    public decimal UserCommissionPercent { get; set; } = 10;
    public decimal ProCommissionPercent  { get; set; } = 10;
    public decimal GstPercent            { get; set; } = 18;
    public decimal MinPlatformFee        { get; set; } = 10;
    public decimal MaxCommissionPercent  { get; set; } = 20;
}

public class RateSplit
{
    public decimal BidAmount             { get; set; }
    public decimal UserCommission        { get; set; }
    public decimal GstOnUserCommission   { get; set; }
    public decimal TotalAmountUserPays   { get; set; }
    public decimal ProDeduction          { get; set; }
    public decimal ProPayout             { get; set; }
    public decimal TotalPlatformEarnings { get; set; }
    public decimal UserCommissionPercent { get; set; }
    public decimal ProCommissionPercent  { get; set; }
    public decimal GstPercent            { get; set; }
    public decimal EffectiveUserChargePercent { get; set; }
    public decimal EffectiveProPayoutPercent  { get; set; }
}
