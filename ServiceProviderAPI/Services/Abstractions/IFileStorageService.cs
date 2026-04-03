namespace ServiceProviderAPI.Services.Abstractions;

/// <summary>
/// Abstraction for file storage (Azure Blob, AWS S3, local disk, etc.)
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Upload a file to storage
    /// </summary>
    /// <param name="fileName">Name of file (unique identifier will be added)</param>
    /// <param name="fileStream">File stream to upload</param>
    /// <param name="contentType">MIME type (e.g., "image/jpeg")</param>
    /// <returns>File ID for later retrieval, or null if failed</returns>
    Task<string?> UploadFileAsync(string fileName, Stream fileStream, string contentType);

    /// <summary>
    /// Download/retrieve a file
    /// </summary>
    /// <param name="fileId">File ID returned from upload</param>
    /// <returns>Stream containing file data, or null if not found</returns>
    Task<Stream?> DownloadFileAsync(string fileId);

    /// <summary>
    /// Get public URL for a file (for display/preview)
    /// </summary>
    /// <param name="fileId">File ID</param>
    /// <returns>Public URL, or null if file private or not found</returns>
    Task<string?> GetFileUrlAsync(string fileId);

    /// <summary>
    /// Delete a file
    /// </summary>
    /// <param name="fileId">File ID</param>
    /// <returns>True if deleted, false if not found or error</returns>
    Task<bool> DeleteFileAsync(string fileId);

    /// <summary>
    /// Storage provider name for logging
    /// </summary>
    string StorageName { get; }
}
