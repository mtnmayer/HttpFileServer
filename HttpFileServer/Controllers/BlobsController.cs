using Microsoft.AspNetCore.Mvc;
using MimeTypes;
using HttpFileServer.Helpers;
using HttpFileServer.Services;

namespace HttpFileServer.Controllers
{
    [ApiController]
    [Route("blobs")]
    public class BlobsController : ControllerBase
    {
        private readonly IBlobStorageService _blobService;
        private readonly ILogger<BlobsController> _logger;

        public BlobsController(IBlobStorageService blobService, ILogger<BlobsController> logger)
        {
            _blobService = blobService;
            _logger = logger;
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> StoreBlob(string id)
        {
            if (!_blobService.IsReady)
                return StatusCode(503, new { error = "Server is not ready" });

            var (success, error) = await _blobService.StoreBlobAsync(id, Request.Body, Request.Headers);

            if (!success)
            {
                return error switch
                {
                    var e when e.Contains("ID") => BadRequest(new { error = e }),
                    var e when e.Contains("Content-Length") => BadRequest(new { error = e }),
                    var e when e.Contains("exceeds maximum") => BadRequest(new { error = e }),
                    var e when e.Contains("Total size") => BadRequest(new { error = e }),
                    var e when e.Contains("storage space") => BadRequest(new { error = e }),
                    _ => StatusCode(500, new { error = error })
                };
            }

            return Ok(new { message = "Blob stored successfully" });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBlob(string id)
        {
            if (!_blobService.IsReady)
                return StatusCode(503, new { error = "Server is not ready" });

            var idError = ValidationHelper.ValidateId(id);
            if (idError != null)
                return BadRequest(new { error = idError });

            var (found, metadata) = await _blobService.GetBlobMetadataAsync(id);
            if (!found || metadata == null)
                return NotFound(new { error = "Blob not found" });

            var stream = await _blobService.GetBlobStreamAsync(id);
            if (stream == null)
                return NotFound(new { error = "Blob not found" });

            foreach (var header in metadata.Headers)
            {
                Response.Headers[header.Key] = header.Value;
            }

            // RON: content type can be calculated once when storing it, rather than calculating it while serving.
            if (!metadata.Headers.ContainsKey("Content-Type"))
            {
                var contentType = MimeTypeMap.GetMimeType(Path.GetExtension(id)) ?? "application/octet-stream";
                Response.Headers["Content-Type"] = contentType;
            }

            return new FileStreamResult(stream, Response.Headers["Content-Type"].ToString());
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBlob(string id)
        {
            if (!_blobService.IsReady)
                return StatusCode(503, new { error = "Server is not ready" });

            var idError = ValidationHelper.ValidateId(id);
            if (idError != null)
                return BadRequest(new { error = idError });

            await _blobService.DeleteBlobAsync(id);
            return Ok(new { message = "Blob deleted" });
        }
    }
}
