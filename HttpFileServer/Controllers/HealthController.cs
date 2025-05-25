using Microsoft.AspNetCore.Mvc;
using HttpFileServer.Services;

namespace HttpFileServer.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly IBlobStorageService _blobService;

        public HealthController(IBlobStorageService blobService)
        {
            _blobService = blobService;
        }

        [HttpGet]
        public IActionResult GetHealth()
        {
            return Ok(new
            {
                ready = _blobService.IsReady,
                diskUsage = _blobService.DiskUsage
            });
        }
    }
}