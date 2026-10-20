using System.Collections.Generic;
using System.Linq;
using Infostructure.MyBigBro.BusinessLogic.Services;
using Infostructure.MyBigBro.BusinessLogic.WebCam;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.DataModel.Models;
using Infostructure.MyBigBro.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Infostructure.MyBigBro.Web4.Controllers
{
    [ApiController]
    public class WebCamsController : ControllerBase
    {
        private readonly IMyBigBroRepository _myBigBroRepository;
        private readonly IWebCamControl _webCamControl;
        private readonly IGeoMarkerService _geoMarkerService;

        public WebCamsController(
            IMyBigBroRepository myBigBroRepository,
            IWebCamControl webCamControl,
            IGeoMarkerService geoMarkerService)
        {
            _myBigBroRepository = myBigBroRepository;
            _webCamControl = webCamControl;
            _geoMarkerService = geoMarkerService;
        }

        // GET api/webcams
        [HttpGet("api/webcams")]
        [HttpGet("api/webcam")]
        public IEnumerable<WebCam> Get()
        {
            return _myBigBroRepository.Set<WebCam>().ToList();
        }

        // GET api/webcams/5
        [HttpGet("api/webcams/{id:int}")]
        [HttpGet("api/webcam/{id:int}")]
        public ActionResult<WebCam> Get(int id)
        {
            var result = _myBigBroRepository.Set<WebCam>().FirstOrDefault(x => x.Id == id);
            if (result == null)
                return NotFound();
            return result;
        }

        // GET api/webcams/nearest?deviceName=...
        [HttpGet("api/webcams/nearest")]
        [HttpGet("api/webcam/nearest")]
        public ActionResult<WebCamExtendedInfo> GetNearest([FromQuery] string deviceName)
        {
            var latestGeoMarker = _geoMarkerService.GetLatestGeoMarkerForDevice(deviceName);
            if (latestGeoMarker == null)
                return Ok(null);
            var nearestWebCam = _webCamControl.GetNearestWebCam(latestGeoMarker);
            return (WebCamExtendedInfo)nearestWebCam;
        }

        // GET api/webcams/nearestmany?top=...&deviceName=...
        [HttpGet("api/webcams/nearestmany")]
        [HttpGet("api/webcam/nearestmany")]
        public ActionResult<IEnumerable<WebCamExtendedInfo>> GetNearestMany([FromQuery] int top, [FromQuery] string deviceName)
        {
            var latestGeoMarker = _geoMarkerService.GetLatestGeoMarkerForDevice(deviceName);
            if (latestGeoMarker == null)
                return Ok(null);
            var nearestWebCams = _webCamControl.GetNearestWebCams(top, latestGeoMarker);
            return Ok(nearestWebCams.Cast<WebCamExtendedInfo>().ToList());
        }
    }
}
