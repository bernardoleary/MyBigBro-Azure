using System;
using System.Collections.Generic;
using System.Linq;
using Infostructure.MyBigBro.BusinessLogic.Services;
using Infostructure.MyBigBro.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Infostructure.MyBigBro.Web4.Controllers
{
    [ApiController]
    public class CapturedImagesGeoMarkerController : ControllerBase
    {
        private readonly IGeoMarkerService _geoMarkerService;

        public CapturedImagesGeoMarkerController(IGeoMarkerService geoMarkerService)
        {
            _geoMarkerService = geoMarkerService;
        }

        // Unified GET endpoint supporting all query parameter combinations:
        // - GET /api/geomarkers/capturedimages
        // - GET /api/geomarkers/capturedimages?top=...
        // - GET /api/geomarkers/capturedimages?top=...&deviceName=...
        // - GET /api/geomarkers/capturedimages?startDateTime=...&endDateTime=...
        // - GET /api/geomarkers/capturedimages?startDateTime=...&endDateTime=...&deviceName=...
        [HttpGet("api/geomarkers/capturedimages")]
        [HttpGet("api/geomarker/capturedimages")]
        public IEnumerable<CapturedImageGeoMarkerDetail> GetCapturedImages(
            [FromQuery] int? top = null,
            [FromQuery] string startDateTime = null,
            [FromQuery] string endDateTime = null,
            [FromQuery] string deviceName = null)
        {
            if (!string.IsNullOrWhiteSpace(startDateTime) && !string.IsNullOrWhiteSpace(endDateTime))
            {
                if (!string.IsNullOrWhiteSpace(deviceName))
                {
                    return Get(startDateTime, endDateTime, deviceName).ToList();
                }
                return Get(startDateTime, endDateTime).ToList();
            }

            if (top.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(deviceName))
                {
                    return Get(top.Value, deviceName).ToList();
                }
                return Get(top.Value).ToList();
            }

            if (!string.IsNullOrWhiteSpace(deviceName))
            {
                return Get(100, deviceName).ToList();
            }

            return Get().ToList();
        }

        // GET api/geomarkers/capturedimages/count?deviceName=...
        [HttpGet("api/geomarkers/capturedimages/count")]
        [HttpGet("api/geomarker/capturedimages/count")]
        public int GetCount([FromQuery] string deviceName)
        {
            return Get(deviceName);
        }

        [NonAction]
        public IEnumerable<CapturedImageGeoMarkerDetail> Get()
        {
            return _geoMarkerService.GetMarkersWithImage(100);
        }

        [NonAction]
        public IEnumerable<CapturedImageGeoMarkerDetail> Get(int top)
        {
            return _geoMarkerService.GetMarkersWithImage(top);
        }

        [NonAction]
        public IEnumerable<CapturedImageGeoMarkerDetail> Get(int top, string deviceName)
        {
            return _geoMarkerService.GetMarkersWithImage(top, deviceName);
        }

        [NonAction]
        public int Get(string deviceName)
        {
            return _geoMarkerService.GetCountOfMarkersWithImage(deviceName);
        }

        [NonAction]
        public IEnumerable<CapturedImageGeoMarkerDetail> Get(string startDateTime, string endDateTime, string deviceName)
        {
            DateTime.TryParse(startDateTime, out var startDateTimeStronglyTyped);
            DateTime.TryParse(endDateTime, out var endDateTimeStronglyTyped);
            return _geoMarkerService.GetMarkersWithImageForTimeframe(startDateTimeStronglyTyped, endDateTimeStronglyTyped, deviceName);
        }

        [NonAction]
        public IEnumerable<CapturedImageGeoMarkerDetail> Get(string startDateTime, string endDateTime)
        {
            DateTime.TryParse(startDateTime, out var startDateTimeStronglyTyped);
            DateTime.TryParse(endDateTime, out var endDateTimeStronglyTyped);
            return _geoMarkerService.GetMarkersWithImageForTimeframe(startDateTimeStronglyTyped, endDateTimeStronglyTyped);
        }
    }
}
