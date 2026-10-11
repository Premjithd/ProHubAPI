using ServiceProviderAPI.Services.Abstractions;

namespace ServiceProviderAPI.Services.Providers;

/// <summary>
/// Self-contained insurance provider that issues yProHub-backed coverage without an
/// external underwriter. Premiums are a flat percentage of the work amount and coverage
/// scales with the selected tier. Replace with a real third-party integration
/// (PolicyBazaar etc.) by providing another <see cref="IInsuranceProvider"/>.
/// </summary>
public class PlaceholderInsuranceProvider : IInsuranceProvider
{
    private readonly ILogger<PlaceholderInsuranceProvider> _logger;

    public PlaceholderInsuranceProvider(ILogger<PlaceholderInsuranceProvider> logger)
    {
        _logger = logger;
    }

    public string ProviderName => "yProHub Protect";

    public Task<List<InsuranceCoverageOption>> GetCoverageOptionsAsync(string serviceCategory, decimal amount)
    {
        var baseAmount = amount <= 0 ? 1000m : amount;
        var options = new List<InsuranceCoverageOption>
        {
            new()
            {
                CoverageType = "basic",
                Description = "Covers workmanship defects for 30 days after completion.",
                PremiumAmount = Math.Round(baseAmount * 0.02m, 2),
                CoverageAmount = baseAmount,
                CoverageDaysAfterCompletion = 30
            },
            new()
            {
                CoverageType = "premium",
                Description = "Covers workmanship defects and accidental damage for 90 days.",
                PremiumAmount = Math.Round(baseAmount * 0.04m, 2),
                CoverageAmount = baseAmount * 2,
                CoverageDaysAfterCompletion = 90
            },
            new()
            {
                CoverageType = "comprehensive",
                Description = "Full workmanship, damage, and liability cover for 180 days.",
                PremiumAmount = Math.Round(baseAmount * 0.06m, 2),
                CoverageAmount = baseAmount * 3,
                CoverageDaysAfterCompletion = 180
            }
        };
        return Task.FromResult(options);
    }

    public Task<InsurancePolicyResponse?> CreatePolicyAsync(
        int jobId, string coverageType, decimal amount, string consumerName, string consumerEmail)
    {
        var days = coverageType?.ToLowerInvariant() switch
        {
            "comprehensive" => 180,
            "premium" => 90,
            _ => 30
        };
        var policy = new InsurancePolicyResponse
        {
            PolicyNumber = $"YPH-{jobId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Status = "Active",
            CoverageType = coverageType,
            CoverageAmount = amount,
            ExpiresAt = DateTime.UtcNow.AddDays(days),
            ProviderReference = ProviderName
        };
        _logger.LogInformation("Issued {Coverage} policy {Policy} for Job:{JobId}", coverageType, policy.PolicyNumber, jobId);
        return Task.FromResult<InsurancePolicyResponse?>(policy);
    }

    public Task<InsurancePolicyDetails?> GetPolicyCoverageAsync(string policyNumber)
    {
        return Task.FromResult<InsurancePolicyDetails?>(new InsurancePolicyDetails
        {
            PolicyNumber = policyNumber,
            Status = "Active"
        });
    }

    public Task<string?> ClaimInsuranceAsync(
        string policyNumber, int jobId, string claimReason, List<string>? evidence = null)
    {
        var claimId = $"CLM-{jobId}-{Guid.NewGuid():N}";
        _logger.LogInformation("Recorded insurance claim {ClaimId} on policy {Policy}", claimId, policyNumber);
        return Task.FromResult<string?>(claimId);
    }
}
