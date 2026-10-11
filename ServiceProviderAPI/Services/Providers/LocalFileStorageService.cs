using ServiceProviderAPI.Services.Abstractions;

namespace ServiceProviderAPI.Services.Providers;

/// <summary>
/// Stores uploaded files on the local disk under <c>wwwroot/uploads/files</c> and serves
/// them as static content. A <c>fileId</c> is the stored file name (guid + extension);
/// the public URL is <c>/uploads/files/{fileId}</c>. Swap for an Azure Blob / S3
/// implementation in production by providing another <see cref="IFileStorageService"/>.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalFileStorageService> _logger;

    private const string RelativeRoot = "uploads/files";

    public LocalFileStorageService(IWebHostEnvironment env, ILogger<LocalFileStorageService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public string StorageName => "LocalDisk";

    private string WebRoot => _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");

    public async Task<string?> UploadFileAsync(string fileName, Stream fileStream, string contentType)
    {
        try
        {
            var ext = Path.GetExtension(fileName);
            var fileId = $"{Guid.NewGuid():N}{ext}";
            var folder = Path.Combine(WebRoot, "uploads", "files");
            Directory.CreateDirectory(folder);

            var fullPath = Path.Combine(folder, fileId);
            await using var dest = new FileStream(fullPath, FileMode.Create);
            await fileStream.CopyToAsync(dest);

            _logger.LogInformation("Stored file {FileId} ({ContentType})", fileId, contentType);
            return fileId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store uploaded file {FileName}", fileName);
            return null;
        }
    }

    public Task<Stream?> DownloadFileAsync(string fileId)
    {
        var fullPath = ResolvePath(fileId);
        if (fullPath == null || !File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<string?> GetFileUrlAsync(string fileId)
    {
        if (string.IsNullOrWhiteSpace(fileId))
            return Task.FromResult<string?>(null);
        return Task.FromResult<string?>($"/{RelativeRoot}/{fileId}");
    }

    public Task<bool> DeleteFileAsync(string fileId)
    {
        try
        {
            var fullPath = ResolvePath(fileId);
            if (fullPath != null && File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to delete file {FileId}: {Error}", fileId, ex.Message);
        }
        return Task.FromResult(false);
    }

    // A fileId must be a bare file name — reject anything that could escape the uploads folder.
    private string? ResolvePath(string fileId)
    {
        if (string.IsNullOrWhiteSpace(fileId) || fileId.Contains("..") ||
            fileId.Contains('/') || fileId.Contains('\\'))
            return null;
        return Path.Combine(WebRoot, "uploads", "files", fileId);
    }
}
