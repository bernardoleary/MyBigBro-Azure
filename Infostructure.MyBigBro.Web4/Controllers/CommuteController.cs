#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
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

        [HttpGet("support")]
        public ActionResult<SupportStatusDto> GetSupportStatus()
        {
            return Ok(_commuteStripService.GetSupportStatus());
        }

        [HttpPost("support/webhook")]
        public async Task<ActionResult<SupportStatusDto>> ReceiveSupportWebhook([FromQuery] string? token = null)
        {
            string? expectedToken = Environment.GetEnvironmentVariable("ONRAMP_WEBHOOK_TOKEN");
            string payloadJson = string.Empty;

            if (Request.HasFormContentType && Request.Form.TryGetValue("data", out var kofiData))
            {
                payloadJson = kofiData.ToString();
            }
            else
            {
                using var reader = new StreamReader(Request.Body);
                payloadJson = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return BadRequest(new { error = "Empty webhook payload" });
            }

            decimal amount = 3m;
            string? payloadToken = null;

            try
            {
                using var doc = JsonDocument.Parse(payloadJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("verification_token", out var vt))
                {
                    payloadToken = vt.GetString();
                }

                // Ko-fi format: { "amount": "3.00" }
                if (root.TryGetProperty("amount", out var amtProp))
                {
                    if (amtProp.ValueKind == JsonValueKind.Number)
                        amount = amtProp.GetDecimal();
                    else if (amtProp.ValueKind == JsonValueKind.String && decimal.TryParse(amtProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                        amount = parsed;
                }
                // Buy Me a Coffee format: { "type": "donation.created", "data": { "amount": 3 } }
                else if (root.TryGetProperty("data", out var dataObj) && dataObj.ValueKind == JsonValueKind.Object)
                {
                    if (dataObj.TryGetProperty("amount", out var bmcAmt))
                    {
                        if (bmcAmt.ValueKind == JsonValueKind.Number)
                            amount = bmcAmt.GetDecimal();
                        else if (bmcAmt.ValueKind == JsonValueKind.String && decimal.TryParse(bmcAmt.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedBmc))
                            amount = parsedBmc;
                    }
                }
            }
            catch
            {
                return BadRequest(new { error = "Invalid JSON webhook payload" });
            }

            if (!string.IsNullOrWhiteSpace(expectedToken))
            {
                string? providedToken = payloadToken ?? token;
                if (!string.Equals(expectedToken, providedToken, StringComparison.Ordinal))
                {
                    return Unauthorized(new { error = "Invalid webhook token" });
                }
            }

            var updated = _commuteStripService.RecordSupportContribution(amount);
            return Ok(updated);
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

        [HttpGet("og-preview.jpg")]
        public ActionResult GetOgPreviewImage([FromQuery] string? cam = null)
        {
            var frame = _commuteStripService.GetLatestPreviewFrame(cam);
            if (frame != null && frame.ImageBytes.Length > 0)
            {
                Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] = "public, max-age=180";
                return File(frame.ImageBytes, frame.ContentType);
            }

            return Redirect("https://www.trafficnz.info/camera/50.jpg");
        }
    }
}
