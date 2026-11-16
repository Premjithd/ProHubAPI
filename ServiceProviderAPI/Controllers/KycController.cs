using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Pro")]
public class KycController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KycController> _logger;

    private static readonly string[] AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png"];
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    // KYC docs are stored OUTSIDE wwwroot so they are never served as anonymous static files.
    private const string PrivateRoot = "private-uploads";
    private static readonly TimeSpan SignedUrlTtl = TimeSpan.FromHours(1);

    public KycController(ApplicationDbContext context, IWebHostEnvironment env, IConfiguration configuration, ILogger<KycController> logger)
    {
        _context = context;
        _env = env;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<ActionResult> GetKycStatus()
    {
        var proId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var pro = await _context.Pros.FindAsync(proId);
        if (pro == null) return NotFound();

        return Ok(new
        {
            kycStatus = pro.KycStatus,
            kycSubmittedAt = pro.KycSubmittedAt,
            aadhaarUploaded = pro.AadhaarDocumentPath != null,
            panUploaded = pro.PanDocumentPath != null,
            aadhaarUrl = pro.AadhaarDocumentPath != null ? BuildSignedUrl(proId, "aadhaar") : null,
            panUrl = pro.PanDocumentPath != null ? BuildSignedUrl(proId, "pan") : null
        });
    }

    [HttpPost("upload/aadhaar")]
    public async Task<ActionResult> UploadAadhaar(IFormFile file)
    {
        var proId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var result = await SaveDocument(file, proId, "aadhaar");
        if (result.Error != null) return BadRequest(new { message = result.Error });

        var pro = await _context.Pros.FindAsync(proId);
        if (pro == null) return NotFound();

        if (pro.AadhaarDocumentPath != null)
            DeleteFile(pro.AadhaarDocumentPath);

        pro.AadhaarDocumentPath = result.RelativePath;
        pro.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Aadhaar document uploaded successfully.", url = BuildSignedUrl(proId, "aadhaar") });
    }

    [HttpPost("upload/pan")]
    public async Task<ActionResult> UploadPan(IFormFile file)
    {
        var proId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var result = await SaveDocument(file, proId, "pan");
        if (result.Error != null) return BadRequest(new { message = result.Error });

        var pro = await _context.Pros.FindAsync(proId);
        if (pro == null) return NotFound();

        if (pro.PanDocumentPath != null)
            DeleteFile(pro.PanDocumentPath);

        pro.PanDocumentPath = result.RelativePath;
        pro.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "PAN document uploaded successfully.", url = BuildSignedUrl(proId, "pan") });
    }

    [HttpPost("submit")]
    public async Task<ActionResult> SubmitKyc()
    {
        var proId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var pro = await _context.Pros.FindAsync(proId);
        if (pro == null) return NotFound();

        if (pro.AadhaarDocumentPath == null || pro.PanDocumentPath == null)
            return BadRequest(new { message = "Please upload both Aadhaar and PAN documents before submitting." });

        if (pro.KycStatus == "Approved")
            return BadRequest(new { message = "KYC is already approved." });

        pro.KycStatus = "Submitted";
        pro.KycSubmittedAt = DateTime.UtcNow;
        pro.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "KYC submitted successfully. You will be notified once verified." });
    }

    // GET /api/kyc/doc?pid=&type=&exp=&sig= — streams a KYC document.
    // Anonymous (so <img>/<a> can load it without an auth header) but gated by a short-lived
    // HMAC signature that is only ever minted for the owning pro (or an admin, future review flow).
    [HttpGet("doc")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDocument(
        [FromQuery] int pid, [FromQuery] string type, [FromQuery] long exp, [FromQuery] string sig)
    {
        if (type != "aadhaar" && type != "pan")
            return BadRequest();

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
            return StatusCode(StatusCodes.Status410Gone, new { message = "Link expired. Reload the page to get a fresh link." });

        if (!FixedTimeEquals(sig, ComputeSignature(pid, type, exp)))
            return Forbid();

        var pro = await _context.Pros.FindAsync(pid);
        var storedPath = type == "aadhaar" ? pro?.AadhaarDocumentPath : pro?.PanDocumentPath;
        if (string.IsNullOrEmpty(storedPath)) return NotFound();

        var fullPath = ResolveStoredPath(storedPath);
        if (fullPath == null || !System.IO.File.Exists(fullPath)) return NotFound();

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        return File(stream, GetContentType(fullPath));
    }

    private async Task<(string? RelativePath, string? Error)> SaveDocument(IFormFile? file, int proId, string docType)
    {
        if (file == null || file.Length == 0)
            return (null, "No file provided.");

        if (file.Length > MaxFileSizeBytes)
            return (null, "File size must not exceed 5 MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return (null, "Only PDF, JPG, JPEG, and PNG files are allowed.");

        var folder = Path.Combine(_env.ContentRootPath, PrivateRoot, "kyc", proId.ToString());
        Directory.CreateDirectory(folder);

        var fileName = $"{docType}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        // Stored relative to ContentRoot (forward slashes) — NOT a web-servable path.
        var relativePath = $"{PrivateRoot}/kyc/{proId}/{fileName}";
        _logger.LogInformation("KYC document saved: {Path}", relativePath);
        return (relativePath, null);
    }

    private void DeleteFile(string relativePath)
    {
        try
        {
            var fullPath = ResolveStoredPath(relativePath);
            if (fullPath != null && System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to delete old KYC file {Path}: {Error}", relativePath, ex.Message);
        }
    }

    // Resolves a stored relative path under ContentRoot, rejecting anything that escapes the private root.
    private string? ResolveStoredPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains(".."))
            return null;
        var normalized = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, normalized));
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, PrivateRoot));
        return fullPath.StartsWith(root, StringComparison.Ordinal) ? fullPath : null;
    }

    private string BuildSignedUrl(int proId, string docType)
    {
        var exp = DateTimeOffset.UtcNow.Add(SignedUrlTtl).ToUnixTimeSeconds();
        var sig = ComputeSignature(proId, docType, exp);
        return $"{Request.Scheme}://{Request.Host}/api/kyc/doc?pid={proId}&type={docType}&exp={exp}&sig={sig}";
    }

    private string ComputeSignature(int proId, string docType, long exp)
    {
        var key = _configuration["Jwt:Key"] ?? "kyc-signing-fallback";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{proId}:{docType}:{exp}"));
        return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a ?? ""), Encoding.UTF8.GetBytes(b));

    private static string GetContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
}
