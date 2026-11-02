#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Infostructure.MyBigBro.Web4.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Infostructure.MyBigBro.Web4.Controllers
{
    [ApiController]
    [Route("api/commute")]
    public class CommuteController : ControllerBase
    {
        private readonly CommuteStripService _commuteStripService;

        public CommuteController(CommuteStripService commuteStripService)
        {
            _commuteStripService = commuteStripService;
        }

        [HttpGet("corridors")]
        public ActionResult<IReadOnlyList<CommuteCorridorDto>> GetCorridors([FromQuery] string? city = null)
        {
            return Ok(_commuteStripService.GetCorridors(city));
        }

        [HttpGet("corridors/{id}")]
        public ActionResult<CommuteCorridorDto> GetCorridor(string id)
        {
            var corridor = _commuteStripService.GetCorridorById(id);
            if (corridor == null)
            {
                return NotFound();
            }
            return Ok(corridor);
        }

        [HttpPost("poll")]
        public async Task<ActionResult> TriggerPoll([FromQuery] string? corridorId = null, CancellationToken cancellationToken = default)
        {
            int count = await _commuteStripService.PollCorridorCamerasNowAsync(corridorId, cancellationToken);
            return Ok(new
            {
                PolledCameras = count,
                CorridorId = corridorId ?? "all"
            });
        }

        [HttpGet("frames/{cameraId}/{frameId}")]
        public ActionResult GetFrame(string cameraId, string frameId)
        {
            var frame = _commuteStripService.GetFrame(cameraId, frameId);
            if (frame == null || frame.ImageBytes.Length == 0)
            {
                return NotFound();
            }

            Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] = "public, max-age=3600";
            return File(frame.ImageBytes, frame.ContentType);
        }
    }
}
