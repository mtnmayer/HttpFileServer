using System.Collections.Concurrent;
using System.Text.Json;
using HttpFileServer.Common;
using HttpFileServer.Helpers;
using HttpFileServer.Model;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.VisualBasic;
using Constants = HttpFileServer.Common.Constants;

namespace HttpFileServer.Services
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly string _dataDir = Path.Combine(Directory.GetCurrentDirectory(), "blobs_data");
        private readonly string _metadataFile;
        private readonly ConcurrentDictionary<string, BlobMetadata> _blobIndex = new();
        private readonly SemaphoreSlim _metadataLock = new(1, 1);
        private readonly ILogger<BlobStorageService> _logger;

        private long _diskUsage = 0;
        private bool _isReady = false;

        public bool IsReady => _isReady;
        public long DiskUsage => _diskUsage;

        public BlobStorageService(ILogger<BlobStorageService> logger)
        {
            _logger = logger;
            _metadataFile = Path.Combine(_dataDir, "metadata.json");
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing blob storage service...");

            Directory.CreateDirectory(_dataDir);

            await LoadMetadataAsync();
            await CalculateDiskUsageAsync();
            await CleanupAsync();

            _isReady = true;
            _logger.LogInformation($"Blob storage initialized. Current disk usage: {_diskUsage} bytes");
        }

        private async Task LoadMetadataAsync()
        {
            try
            {
                if (File.Exists(_metadataFile))
                {
                    var json = await File.ReadAllTextAsync(_metadataFile);
                    var metadata = JsonSerializer.Deserialize<Dictionary<string, BlobMetadata>>(json);

                    if (metadata != null)
                    {
                        foreach (var kvp in metadata)
                        {
                            _blobIndex[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load metadata, starting fresh");
            }
        }

        private async Task SaveMetadataAsync()
        {
            await _metadataLock.WaitAsync();
            try
            {
                var metadata = _blobIndex.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_metadataFile, json);
            }
            finally
            {
                _metadataLock.Release();
            }
        }

        private async Task CalculateDiskUsageAsync()
        {
            long totalSize = 0;
            var keysToRemove = new List<string>();

            foreach (var kvp in _blobIndex)
            {
                try
                {
                    var fileInfo = new FileInfo(kvp.Value.Path);
                    if (fileInfo.Exists)
                    {
                        totalSize += fileInfo.Length;
                    }
                    else
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                catch
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _blobIndex.TryRemove(key, out _);
            }

            if (File.Exists(_metadataFile))
            {
                totalSize += new FileInfo(_metadataFile).Length;
            }

            _diskUsage = totalSize;
        }

        private async Task CleanupAsync()
        {
            var allFiles = GetAllBlobFiles();
            var indexedPaths = new HashSet<string>(_blobIndex.Values.Select(v => v.Path));

            foreach (var filePath in allFiles)
            {
                if (!indexedPaths.Contains(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        _logger.LogInformation($"Removed orphaned file: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to remove orphaned file: {filePath}");
                    }
                }
            }

            await SaveMetadataAsync();
        }

        private IEnumerable<string> GetAllBlobFiles()
        {
            if (!Directory.Exists(_dataDir))
                return Enumerable.Empty<string>();

            return Directory.GetFiles(_dataDir, "*.blob", SearchOption.AllDirectories);
        }

        public async Task<(bool Success, string? Error)> StoreBlobAsync(string id, Stream content, IHeaderDictionary headers)
        {
            if (!_isReady)
                return (false, "Server is not ready");

            var idError = ValidationHelper.ValidateId(id);
            if (idError != null)
                return (false, idError);

            if (!headers.ContainsKey("Content-Length"))
                return (false, "Content-Length header is required");

            if (!long.TryParse(headers["Content-Length"], out var contentLength))
                return (false, "Invalid Content-Length header");

            var (storedHeaders, headerError) = ValidationHelper.ValidateHeaders(headers);
            if (headerError != null)
                return (false, headerError);

            var headersSize = JsonSerializer.Serialize(storedHeaders).Length;
            var totalSize = contentLength + headersSize;

            if (totalSize > Constants.MAX_LENGTH)
                return (false, $"Total size ({totalSize}) exceeds maximum of {Constants.MAX_LENGTH}");

            var existingSize = _blobIndex.TryGetValue(id, out var existing) ? existing.Size : 0;
            var sizeChange = totalSize - existingSize;

            if (_diskUsage + sizeChange > Constants.MAX_DISK_QUOTA)
                return (false, "Insufficient storage space");

            var (directory, blobPath) = Helpers.PathHelper.GetBlobPath(_dataDir, id);

            try
            {
                Directory.CreateDirectory(directory);

                using var fileStream = new FileStream(blobPath, FileMode.Create, FileAccess.Write);
                await content.CopyToAsync(fileStream);

                var actualSize = fileStream.Length;
                if (actualSize != contentLength)
                {
                    File.Delete(blobPath);
                    return (false, $"Content size mismatch. Expected: {contentLength}, Actual: {actualSize}");
                }

                var metadata = new BlobMetadata
                {
                    Path = blobPath,
                    Size = totalSize,
                    Headers = storedHeaders
                };

                _blobIndex[id] = metadata;
                Interlocked.Add(ref _diskUsage, sizeChange);

                await SaveMetadataAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error storing blob {id}");

                try { File.Delete(blobPath); } catch { }

                return (false, "Internal server error");
            }
        }

        public Task<(bool Found, BlobMetadata? Metadata)> GetBlobMetadataAsync(string id)
        {
            if (!_isReady)
                return Task.FromResult((false, (BlobMetadata?)null));

            var found = _blobIndex.TryGetValue(id, out var metadata);
            return Task.FromResult((found, metadata));
        }

        public async Task<Stream?> GetBlobStreamAsync(string id)
        {
            if (!_isReady || !_blobIndex.TryGetValue(id, out var metadata))
                return null;

            try
            {
                if (!File.Exists(metadata.Path))
                {
                    _blobIndex.TryRemove(id, out _);
                    await SaveMetadataAsync();
                    return null;
                }

                return new FileStream(metadata.Path, FileMode.Open, FileAccess.Read);
            }
            catch
            {
                return null;
            }
        }

        public async Task DeleteBlobAsync(string id)
        {
            if (!_isReady)
                return;

            if (_blobIndex.TryRemove(id, out var metadata))
            {
                try
                {
                    if (File.Exists(metadata.Path))
                    {
                        File.Delete(metadata.Path);
                        Interlocked.Add(ref _diskUsage, -metadata.Size);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to delete blob file: {metadata.Path}");
                }

                await SaveMetadataAsync();
            }
        }
    }
}