using Microsoft.Extensions.Configuration;

namespace ServiceProviderAPI.Services;

/// <summary>
/// Service for calculating platform commission, GST, and pro payout
/// Phase 1C core business logic
/// </summary>
public interface IRateSplitService
{
    /// <summary>
    /// Calculate rate split from the bid amount
    /// </summary>
    /// <param name="bidAmount">Total bid amount in ₹</param>
    /// <returns>Rate split breakdown</returns>
    RateSplit CalculateSplit(decimal bidAmount);

    /// <summary>
    /// Get current configuration
    /// </summary>
    RateSplitConfig GetConfig();
}

public class RateSplitService : IRateSplitService
{
    private readonly RateSplitConfig _config;
    private readonly ILogger<RateSplitService> _logger;

    public RateSplitService(IConfiguration configuration, ILogger<RateSplitService> logger)
    {
        _logger = logger;
        
        // Load from config or use defaults
        _config = new RateSplitConfig
        {
            PlatformFeePercent = decimal.Parse(
                configuration.GetSection("RateSplit:PlatformFeePercent").Value ?? "10"),
            
            GstPercent = decimal.Parse(
                configuration.GetSection("RateSplit:GSTPercent").Value ?? "18"),
            
            MinPlatformFee = decimal.Parse(
                configuration.GetSection("RateSplit:MinPlatformFee").Value ?? "10"),
            
            MaxPlatformFeePercent = decimal.Parse(
                configuration.GetSection("RateSplit:MaxPlatformFeePercent").Value ?? "20")
        };

        _logger.LogInformation($"RateSplitService initialized: PlatformFee={_config.PlatformFeePercent}%, GST={_config.GstPercent}%");
    }

    public RateSplit CalculateSplit(decimal bidAmount)
    {
        try
        {
            // Calculate platform fee (with min/max bounds)
            var platformFee = (bidAmount * _config.PlatformFeePercent) / 100;
            platformFee = Math.Max(platformFee, _config.MinPlatformFee);  // Apply minimum
            platformFee = Math.Min(platformFee, (bidAmount * _config.MaxPlatformFeePercent) / 100);  // Apply maximum

            // Pro receives before GST
            var proPayoutBeforeGst = bidAmount - platformFee;

            // GST applied to platform fee only (common practice)
            var gstOnPlatformFee = (platformFee * _config.GstPercent) / 100;

            // Total platform cost (fee + GST)
            var totalPlatformCost = platformFee + gstOnPlatformFee;

            // Pro payout remains the same (they pay GST separately if registered)
            var proPayoutAfterGst = proPayoutBeforeGst;

            // Calculate effective rates
            var effectivePlatformFeePercent = (totalPlatformCost / bidAmount) * 100;
            var effectiveProPayoutPercent = (proPayoutAfterGst / bidAmount) * 100;

            var split = new RateSplit
            {
                BidAmount = bidAmount,
                PlatformFee = platformFee,
                GstOnPlatformFee = gstOnPlatformFee,
                TotalPlatformCost = totalPlatformCost,
                ProPayout = proPayoutAfterGst,
                PlatformFeePercent = _config.PlatformFeePercent,
                GstPercent = _config.GstPercent,
                EffectivePlatformFeePercent = decimal.Round(effectivePlatformFeePercent, 2),
                EffectiveProPayoutPercent = decimal.Round(effectiveProPayoutPercent, 2)
            };

            _logger.LogInformation($"Rate split calculated: BidAmount=₹{bidAmount:F2}, ProPayout=₹{split.ProPayout:F2}, PlatformCost=₹{split.TotalPlatformCost:F2}");
            return split;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error calculating rate split: {ex.Message}");
            throw;
        }
    }

    public RateSplitConfig GetConfig()
    {
        return _config;
    }
}

/// <summary>
/// Configuration for rate calculations
/// </summary>
public class RateSplitConfig
{
    public decimal PlatformFeePercent { get; set; } = 10;  // Default 10%
    public decimal GstPercent { get; set; } = 18;  // Indian GST: 18%
    public decimal MinPlatformFee { get; set; } = 10;  // Minimum ₹10
    public decimal MaxPlatformFeePercent { get; set; } = 20;  // Don't charge more than 20% even if calculation shows more
}

/// <summary>
/// Result of rate split calculation
/// </summary>
public class RateSplit
{
    /// <summary>Original bid amount</summary>
    public decimal BidAmount { get; set; }

    /// <summary>Platform commission before GST</summary>
    public decimal PlatformFee { get; set; }

    /// <summary>GST amount on platform fee</summary>
    public decimal GstOnPlatformFee { get; set; }

    /// <summary>Total platform cost to consumer (fee + GST)</summary>
    public decimal TotalPlatformCost { get; set; }

    /// <summary>Amount pro receives</summary>
    public decimal ProPayout { get; set; }

    /// <summary>Configured platform fee percentage</summary>
    public decimal PlatformFeePercent { get; set; }

    /// <summary>Configured GST percentage</summary>
    public decimal GstPercent { get; set; }

    /// <summary>Effective platform fee % after all calculations</summary>
    public decimal EffectivePlatformFeePercent { get; set; }

    /// <summary>Effective pro payout % of bid</summary>
    public decimal EffectiveProPayoutPercent { get; set; }
}
