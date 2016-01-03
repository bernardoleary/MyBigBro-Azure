using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Infostructure.MyBigBro.BusinessLogic.Services;
using Infostructure.MyBigBro.BusinessLogic.WebCam;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.DataModel.Models;

namespace Infostructure.MyBigBro.Web3.Controllers
{
    public class GeoMarkersController : ApiController
    {
        private IMyBigBroRepository _myBigBroRepository = null;
        private IWebCamControl _webCamControl = null;
        private IGeoMarkerService _geoMarkerService = null;

        public GeoMarkersController() {}

        public GeoMarkersController(IMyBigBroRepository myBigBroRepository,
            IWebCamControl webCamControl,
            IGeoMarkerService geoMarkerService)
        {
            _myBigBroRepository = myBigBroRepository;
            _webCamControl = webCamControl;
            _geoMarkerService = geoMarkerService;
        }

        // GET api/geomarker
        public IEnumerable<GeoMarker> Get()
        {
            if (ModelState.IsValid)
            {
                var result = _myBigBroRepository.Set<GeoMarker>();
                return result;
            }
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        // GET api/geomarker/5
        public GeoMarker Get(int id)
        {
            if (ModelState.IsValid)
            {
                var result = _myBigBroRepository.Set<GeoMarker>().FirstOrDefault(x => x.Id == id);
                return result;
            }
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        // GET api/geomarker/5/test
        public GeoMarker Test(int id)
        {
            if (ModelState.IsValid)
            {
                var result = _myBigBroRepository.Set<GeoMarker>().FirstOrDefault(x => x.Id == id);
                return result;
            }
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        // POST api/geomarker
        public HttpResponseMessage Post(GeoMarker value)
        {
            if (ModelState.IsValid)
            {
                // Refeactor this to use the IoC container, as soon as I fugure out how it works!
                _geoMarkerService.ProcessGeoMarker(value);

                // Created.
                var response = new HttpResponseMessage(HttpStatusCode.Created);

                // Advise where the new GeoMarker is.
                string uri = Url.Route(null, new { id = value.Id });
                response.Headers.Location = new Uri(Request.RequestUri, uri);

                return response;
            }
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }
    }
}
