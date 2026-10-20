using System;
using System.Collections.Generic;
using System.Linq;
using Infostructure.MyBigBro.BusinessLogic.Services;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.DataModel.Models;
using Infostructure.MyBigBro.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Infostructure.MyBigBro.Web4.Controllers
{
    [ApiController]
    public class GeoMarkersController : ControllerBase
    {
        private readonly IMyBigBroRepository _myBigBroRepository;
        private readonly IGeoMarkerService _geoMarkerService;

        public GeoMarkersController(
            IMyBigBroRepository myBigBroRepository,
            IGeoMarkerService geoMarkerService)
        {
            _myBigBroRepository = myBigBroRepository;
            _geoMarkerService = geoMarkerService;
        }

        // GET api/geomarkers
        [HttpGet("api/geomarkers")]
        public IEnumerable<GeoMarker> Get()
        {
            return _myBigBroRepository.Set<GeoMarker>().ToList();
        }

        // GET api/geomarkers/latest?deviceName=...
        [HttpGet("api/geomarkers/latest")]
        public ActionResult<GeoMarker> GetLatest([FromQuery] string deviceName)
        {
            var latestGeoMarker = _geoMarkerService.GetLatestGeoMarkerForDevice(deviceName);
            return Ok(latestGeoMarker as GeoMarker);
        }

        // GET api/geomarkers/5
        [HttpGet("api/geomarkers/{id:int}", Name = "GetGeoMarkerById")]
        public ActionResult<GeoMarker> Get(int id)
        {
            var result = _myBigBroRepository.Set<GeoMarker>().FirstOrDefault(x => x.Id == id);
            if (result == null)
                return NotFound();
            return result;
        }

        // POST api/geomarkers
        [HttpPost("api/geomarkers")]
        public IActionResult Post([FromBody] GeoMarkerDto value)
        {
            if (value == null || !ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (value.MarkerDateTime == default)
            {
                value.MarkerDateTime = DateTime.UtcNow;
            }

            var matchedWebCams = _geoMarkerService.ProcessGeoMarker(value);
            var locationUri = $"/api/geomarkers/{value.Id}";
            return Created(locationUri, new
            {
                Id = value.Id,
                DeviceId = value.DeviceId,
                DeviceName = value.DeviceName,
                MatchedWebCams = matchedWebCams
            });
        }
    }
}
