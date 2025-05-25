using HttpFileServer.Model;

namespace HttpFileServer.Services
{
    public interface IBlobStorageService
    {
        Task InitializeAsync();
        Task<(bool Success, string? Error)> StoreBlobAsync(string id, Stream content, IHeaderDictionary headers);
        Task<(bool Found, BlobMetadata? Metadata)> GetBlobMetadataAsync(string id);
        Task<Stream?> GetBlobStreamAsync(string id);
        Task DeleteBlobAsync(string id);
        bool IsReady { get; }
        long DiskUsage { get; }
    }
}
