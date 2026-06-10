using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using System.Security.Claims;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Pro")]
public class KycController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<KycController> _logger;

    private static readonly string[] AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png"];
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public KycController(ApplicationDbContext context, IWebHostEnvironment env, ILogger<KycController> logger)
    {
        _context = context;
        _env = env;
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
            aadhaarUrl = pro.AadhaarDocumentPath != null ? $"{Request.Scheme}://{Request.Host}{pro.AadhaarDocumentPath}" : null,
            panUrl = pro.PanDocumentPath != null ? $"{Request.Scheme}://{Request.Host}{pro.PanDocumentPath}" : null
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

        return Ok(new
        {
            message = "Aadhaar document uploaded successfully.",
            url = $"{Request.Scheme}://{Request.Host}{result.RelativePath}"
        });
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

        return Ok(new
        {
            message = "PAN document uploaded successfully.",
            url = $"{Request.Scheme}://{Request.Host}{result.RelativePath}"
        });
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

    private async Task<(string? RelativePath, string? Error)> SaveDocument(IFormFile? file, int proId, string docType)
    {
        if (file == null || file.Length == 0)
            return (null, "No file provided.");

        if (file.Length > MaxFileSizeBytes)
            return (null, "File size must not exceed 5 MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return (null, "Only PDF, JPG, JPEG, and PNG files are allowed.");

        var folder = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "kyc", proId.ToString());
        Directory.CreateDirectory(folder);

        var fileName = $"{docType}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        var relativePath = $"/uploads/kyc/{proId}/{fileName}";
        _logger.LogInformation("KYC document saved: {Path}", relativePath);
        return (relativePath, null);
    }

    private void DeleteFile(string relativePath)
    {
        try
        {
            var fullPath = Path.Combine(_env.WebRootPath ?? "wwwroot", relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to delete old KYC file {Path}: {Error}", relativePath, ex.Message);
        }
    }
}
