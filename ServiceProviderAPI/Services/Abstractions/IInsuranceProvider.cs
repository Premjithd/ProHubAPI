namespace ServiceProviderAPI.Services.Abstractions;

/// <summary>
/// Abstraction for insurance provider integration (PolicyBazaar, RedCrescent, etc.)
/// </summary>
public interface IInsuranceProvider
{
    /// <summary>
    /// Get available coverage options
    /// </summary>
    /// <param name="serviceCategory">Service category for coverage</param>
    /// <param name="amount">Estimated work amount</param>
    /// <returns>List of available coverage options</returns>
    Task<List<InsuranceCoverageOption>> GetCoverageOptionsAsync(
        string serviceCategory, 
        decimal amount);

    /// <summary>
    /// Create/activate insurance policy
    /// </summary>
    /// <param name="jobId">Job ID for insurance</param>
    /// <param name="coverageType">Type of coverage selected</param>
    /// <param name="amount">Coverage amount</param>
    /// <param name="consumerName">Consumer name</param>
    /// <param name="consumerEmail">Consumer email</param>
    /// <returns>Policy details with policy number</returns>
    Task<InsurancePolicyResponse?> CreatePolicyAsync(
        int jobId, 
        string coverageType, 
        decimal amount, 
        string consumerName, 
        string consumerEmail);

    /// <summary>
    /// Get policy coverage details
    /// </summary>
    /// <param name="policyNumber">Policy number</param>
    /// <returns>Coverage details, or null if not found</returns>
    Task<InsurancePolicyDetails?> GetPolicyCoverageAsync(string policyNumber);

    /// <summary>
    /// Claim insurance for a completed job
    /// </summary>
    /// <param name="policyNumber">Policy number</param>
    /// <param name="jobId">Job ID</param>
    /// <param name="claimReason">Reason for claim</param>
    /// <param name="evidence">Evidence/documentation URLs</param>
    /// <returns>Claim ID if successful</returns>
    Task<string?> ClaimInsuranceAsync(
        string policyNumber, 
        int jobId, 
        string claimReason, 
        List<string>? evidence = null);

    /// <summary>
    /// Provider name for logging
    /// </summary>
    string ProviderName { get; }
}

/// <summary>
/// Available coverage option
/// </summary>
public class InsuranceCoverageOption
{
    public string? CoverageType { get; set; }  // "basic", "premium", "comprehensive"
    public string? Description { get; set; }
    public decimal PremiumAmount { get; set; }  // Cost of coverage
    public decimal CoverageAmount { get; set; }  // Coverage limit
    public int CoverageDaysAfterCompletion { get; set; }  // How many days after completion it covers
}

/// <summary>
/// Response from policy creation
/// </summary>
public class InsurancePolicyResponse
{
    public string? PolicyNumber { get; set; }
    public string? Status { get; set; }  // "Active", "Pending", "Failed"
    public string? CoverageType { get; set; }
    public decimal CoverageAmount { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? ProviderReference { get; set; }
}

/// <summary>
/// Policy coverage details
/// </summary>
public class InsurancePolicyDetails
{
    public string? PolicyNumber { get; set; }
    public string? Status { get; set; }
    public string? CoverageType { get; set; }
    public decimal CoverageAmount { get; set; }
    public decimal PremiumPaid { get; set; }
    public DateTime? ActiveFrom { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public List<string>? CoveredItems { get; set; }
    public string? TermsAndConditions { get; set; }
}
