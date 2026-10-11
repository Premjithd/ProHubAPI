using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceProviderAPI.Services.Abstractions;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _storage;

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".pdf"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public FilesController(IFileStorageService storage)
    {
        _storage = storage;
    }

    // POST: api/files/upload — stores a single file, returns its id + public URL
    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<ActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { message = "File size must not exceed 10 MB." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { message = "Only JPG, PNG, WEBP, and PDF files are allowed." });

        await using var stream = file.OpenReadStream();
        var fileId = await _storage.UploadFileAsync(
            file.FileName, stream, file.ContentType ?? "application/octet-stream");
        if (fileId == null)
            return StatusCode(500, new { message = "Failed to store the uploaded file." });

        var relativeUrl = await _storage.GetFileUrlAsync(fileId);
        var url = relativeUrl == null ? null : $"{Request.Scheme}://{Request.Host}{relativeUrl}";

        return Ok(new { fileId, url });
    }

    // GET: api/files/{fileId} — streams a stored file back (public so <img>/<a> tags can load it)
    [HttpGet("{fileId}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(string fileId)
    {
        var stream = await _storage.DownloadFileAsync(fileId);
        if (stream == null) return NotFound();

        return File(stream, GetContentType(fileId));
    }

    private static string GetContentType(string fileId) =>
        Path.GetExtension(fileId).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
}
