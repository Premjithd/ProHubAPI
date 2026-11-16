namespace ServiceProviderAPI.Configuration;

/// <summary>
/// Configurable knobs for KYC document upload/serving. Bound from the "Kyc" section of
/// configuration; every property defaults to the previous hard-coded value, so an absent
/// section leaves behaviour unchanged.
/// </summary>
public class KycOptions
{
    public const string SectionName = "Kyc";

    /// <summary>Maximum accepted upload size in bytes (default 5 MB).</summary>
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Accepted file extensions (lower-case, leading dot). Matched case-insensitively.</summary>
    public string[] AllowedExtensions { get; set; } = [".pdf", ".jpg", ".jpeg", ".png"];

    /// <summary>How long a signed document URL stays valid, in minutes (default 60).</summary>
    public int SignedUrlTtlMinutes { get; set; } = 60;

    /// <summary>Folder (relative to the content root, outside wwwroot) where docs are stored.</summary>
    public string StorageFolder { get; set; } = "private-uploads";

    /// <summary>Dedicated HMAC key for signing document URLs. Falls back to Jwt:Key when unset.</summary>
    public string? SigningKey { get; set; }
}
