﻿using System;
using System.Collections.Generic;
using System.Data.Objects.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Transactions;
using Infostructure.MyBigBro.BusinessLogic.GeoSpatial;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.Domain;
using Infostructure.MyBigBro.ImageStorageServiceAgent;
using Infostructure.MyBigBro.DataModel.Models;

namespace Infostructure.MyBigBro.BusinessLogic.WebCam
{
    public class WebCamControl : IWebCamControl
    {
        private Infostructure.MyBigBro.Domain.IWebCamImage _webCamImage = null;
        private Infostructure.MyBigBro.Domain.IWebCam _webCam = null;
        private Infostructure.MyBigBro.BusinessLogic.GeoSpatial.IGeometry _geometry = null;
        private Infostructure.MyBigBro.ImageStorageServiceAgent.IStorageServiceAgent _storageServiceAgent = null;
        private Infostructure.MyBigBro.DataModel.DataAccess.IMyBigBroRepository _myBigBroRepository = null;
        private Infostructure.MyBigBro.Domain.ICapturedImage _capturedImage = null;

        public IMyBigBroRepository MyBigBroRepository 
        {
            get { return _myBigBroRepository; }
            set { _myBigBroRepository = value; }
        }

        public IGeometry Geometry
        {
            get { return _geometry; }
            set { _geometry = value; }
        }

        public IWebCam WebCam
        {
            get { return _webCam; }
            set { _webCam = value; }
        }

        public IStorageServiceAgent StorageServiceAgent
        {
            get { return _storageServiceAgent; }
            set { _storageServiceAgent = value; }
        }

        public IWebCamImage WebCamImage
        {
            get { return _webCamImage; }
            set { _webCamImage = value; }
        }

        public ICapturedImage CapturedImage
        {
            get { return _capturedImage; }
            set { _capturedImage = value; }
        }

        public WebCamControl(IStorageServiceAgent storageServiceAgent,
            IMyBigBroRepository myBigBroRepository,
            IGeometry geometry)
        {
            _storageServiceAgent = storageServiceAgent;
            _myBigBroRepository = myBigBroRepository;
            _geometry = geometry;
        }

        // Pass the WebCamDataRequest in as a parameter as it is not really part of the context of the WebCam.
        public void CaptureCurrentImage(IWebCamDataRequest webCamDataRequest)
        {
            // Capture the image and stream the data.
            using (var response = webCamDataRequest.GetWebCamResponse(_webCam.Url))
            {
                using (var stream = webCamDataRequest.GetWebCamResponseStream(response))
                {
                    // Copy the data to the WebCamImage.
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        _webCamImage.Data = memoryStream.ToArray();
                    }
                }
            }
        }

        public int StoreCapturedImage()
        {
            // verify the image is loaded
            if (_webCamImage.Data == null)
                throw new NullReferenceException("The image has not yet been loaded.");

            using (var trans = new TransactionScope())
            {
                // store the image
                string key = null;
                string fileName = _storageServiceAgent.PutImage(_webCamImage, out key);

                // update the database
                _capturedImage.DateTimeCaptured = DateTime.Now;
                _capturedImage.Url = fileName;
                _capturedImage.WebCamId = _webCam.Id;
                _capturedImage.IsThumbnailed = false;
                _capturedImage.Key = key;
                _myBigBroRepository.Add(_capturedImage);
                _myBigBroRepository.SaveChanges();

                // commit the change
                trans.Complete();

                // Return the new image ID.
                return _capturedImage.Id;
            }
        }

        public IWebCamExtendedInfo GetNearestWebCam(IGeoMarker geoMarker)
        {
            // Generate distances for all camera locations from this point
            var webCamsWithDistanceFromGeoMarker =
                from webCam in _myBigBroRepository.Set<Infostructure.MyBigBro.DataModel.Models.WebCam>()
                select new WebCamExtendedInfo
                {
                    //_geometry.GetDistancePythagoras(webCam.XCoord, webCam.YCoord, geoMarker.XCoord, geoMarker.YCoord)
                    Distance = SqlFunctions.SquareRoot(SqlFunctions.Square(webCam.XCoord - geoMarker.XCoord) + SqlFunctions.Square(webCam.YCoord - geoMarker.YCoord)).Value, 
                    Id = webCam.Id,
                    Name = webCam.Name,
                    RadiusOfVisibility = webCam.RadiusOfVisibility,
                    XCoord = webCam.XCoord,
                    YCoord = webCam.YCoord,
                    Url = webCam.Url
                };

            // Get the min distance
            var minDistance = webCamsWithDistanceFromGeoMarker.Min(cam => cam.Distance);

            // Select the camera with mon distance
            var webCamWithMinDistanceFromGeoMarker =
                from webCam in webCamsWithDistanceFromGeoMarker
                where webCam.Distance == minDistance
                select webCam;

            // Return it
            return webCamWithMinDistanceFromGeoMarker.First();
        }

        public IEnumerable<IWebCamExtendedInfo> GetNearestWebCams(int top, IGeoMarker geoMarker)
        {
            // Generate distances for all camera locations from this point
            // Need to do this using a loop as LINQ won't accept the "GetDistancePythagoras" otherwise.
            var webCamsWithDistanceFromGeoMarker = new List<WebCamExtendedInfo>();
            foreach (var webCam in _myBigBroRepository.Set<Infostructure.MyBigBro.DataModel.Models.WebCam>())
            {
                webCamsWithDistanceFromGeoMarker.Add(new WebCamExtendedInfo()
                {
                    Distance = _geometry.GetDistancePythagoras(webCam.XCoord, webCam.YCoord, geoMarker.XCoord, geoMarker.YCoord),
                    //Distance = SqlFunctions.SquareRoot(SqlFunctions.Square(webCam.XCoord - geoMarker.XCoord) + SqlFunctions.Square(webCam.YCoord - geoMarker.YCoord)).Value,
                    Id = webCam.Id,
                    Name = webCam.Name,
                    RadiusOfVisibility = webCam.RadiusOfVisibility,
                    XCoord = webCam.XCoord,
                    YCoord = webCam.YCoord,
                    Url = webCam.Url
                });
            }

            // Get the min distance
            var minDistance = webCamsWithDistanceFromGeoMarker.Min(cam => cam.Distance);

            // Select the camera with mon distance
            var webCamWithMinDistanceFromGeoMarker =
                from webCam in webCamsWithDistanceFromGeoMarker
                orderby webCam.Distance
                select webCam;

            // Return it
            return webCamWithMinDistanceFromGeoMarker.Take(top);
        }
    }
}
