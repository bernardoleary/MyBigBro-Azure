﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Infostructure.MyBigBro.BusinessLogic.GeoSpatial;
using Infostructure.MyBigBro.BusinessLogic.WebCam;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.DataModel.Models;
using Infostructure.MyBigBro.Domain;

//using Microsoft.Practices.Unity;

namespace Infostructure.MyBigBro.BusinessLogic.Services
{
    public class GeoMarkerService : IGeoMarkerService
    {
        private IWebCamControl _webCamControl = null;
        private ILocation _location = null;
        private IMyBigBroRepository _myBigBroRepository = null;

        public IWebCamControl WebCamControl
        {
            get { return _webCamControl; }
            set { _webCamControl = value; }
        }

        public IMyBigBroRepository MyBigBroRepository
        {
            get { return _myBigBroRepository; }
            set { _myBigBroRepository = value; }
        }

        public ILocation Location
        {
            get { return _location; }
            set { _location = value; }
        }

        public GeoMarkerService() {}

        //[InjectionConstructor]
        public GeoMarkerService(
            IWebCamControl webCamControl,
            ILocation location,
            IMyBigBroRepository myBigBroRepository)
        {
            _webCamControl = webCamControl;
            _location = location;
            _myBigBroRepository = myBigBroRepository;
        }
        
        // Returns the number of webcams that can see us at our current location.
        // Is this method doing too much?
        public int ProcessGeoMarker(IGeoMarkerDto geoMarkerDto)
        {
            IEnumerable<IDevice> devices = null; 
                
            // Get devices or create the device if it does not exit and then get it
            devices = GetDevices(geoMarkerDto.DeviceName);
            if (!devices.Any())
            {
                var device = new Device {DeviceName = geoMarkerDto.DeviceName};
                _myBigBroRepository.Add(device);
                _myBigBroRepository.SaveChanges();
                devices = GetDevices(geoMarkerDto.DeviceName);
            }

            // Get the device ID
            var deviceId = devices.First().Id;

            // Create the GeoMarker
            var geoMarker = new GeoMarker
            {
                Id = geoMarkerDto.Id,
                DeviceId = deviceId,
                MarkerDateTime = geoMarkerDto.MarkerDateTime,
                XCoord = geoMarkerDto.XCoord,
                YCoord = geoMarkerDto.YCoord
            };

            // Process
            return ProcessGeoMarker(geoMarker);
        }

        // Get the Device if it exists
        private IEnumerable<IDevice> GetDevices(string deviceName)
        {
            var devices = from device in _myBigBroRepository.Set<Device>()
                where device.DeviceName == deviceName
                select device;
            return devices.AsEnumerable();
        }

        // Returns the number of webcams that can see us at our current location.
        // Is this method doing too much?
        public int ProcessGeoMarker(IGeoMarker geoMarker)
        {
            // Add the GeoMarker to the database and get the ID.
            var geoMarkerId = _location.AppendGeoMarker(geoMarker);

            // Get the list of webcams that we might be able to see us at our current location.
            var webCamsWithinWebCamRadiusOfVisibility = 
                _location.GetWebCamsWithinWebCamRadiusOfVisibility(new Point
                                                                    {
                                                                        XCoord = geoMarker.XCoord,
                                                                        YCoord = geoMarker.YCoord
                                                                    });

            // If there are any webcams that can see us...
            if (webCamsWithinWebCamRadiusOfVisibility.Count > 0)
            {
                foreach (var webCam in webCamsWithinWebCamRadiusOfVisibility)
                {
                    using (var trans = new TransactionScope())
                    {
                        // Capture and add the image to the DB.
                        // Future modification - only capture an image if one has not been recently (within seconds) captured for that webcam.
                        // Future modification - remove this internal dependency on WebCamImage (make a factory?).
                        // Future modification - remove this internal dependency on AwsStorageServiceAgent (could be done using the IoC container?).
                        _webCamControl.WebCam = webCam;
                        _webCamControl.WebCamImage = new WebCamImage();
                        _webCamControl.CaptureCurrentImage(new WebCamDataRequest());
                        _webCamControl.CapturedImage = new CapturedImage();
                        var capturedImageId = _webCamControl.StoreCapturedImage();

                        // Map the image to the GeoMarker
                        // Refactor this method so that it takes a complex type, rather than two primitives.
                        _location.MapCapturedImageToGeoMarker(capturedImageId, geoMarkerId);

                        // commit the change
                        trans.Complete();
                    }
                }
            }

            // Return the count of images collected.
            return webCamsWithinWebCamRadiusOfVisibility.Count;
        }

        // Get the last n geomarkers where an image has been captured, or all
        public IEnumerable<CapturedImageGeoMarkerDetail> GetMarkersWithImage(int top)
        {
            var capturedImagesGeoMarkersDetail = (from geoMarker in _myBigBroRepository.Set<GeoMarker>()
                join capturedImageGeoMarker in
                    _myBigBroRepository.Set<CapturedImageGeoMarker>() on geoMarker.Id equals
                    capturedImageGeoMarker.GeoMarkerId
                join capturedImage in _myBigBroRepository.Set<CapturedImage>() on
                    capturedImageGeoMarker.CapturedImageId equals capturedImage.Id
                select new CapturedImageGeoMarkerDetail {CapturedImage = capturedImage, GeoMarker = geoMarker})
                .OrderByDescending(q => q.CapturedImage.Id)
                .Take(top);
            return capturedImagesGeoMarkersDetail;
        }

        public IEnumerable<CapturedImageGeoMarkerDetail> GetMarkersWithImage(int top, string deviceName)
        {
            var capturedImagesGeoMarkersDetail = (from geoMarker in _myBigBroRepository.Set<GeoMarker>()
                join capturedImageGeoMarker in
                    _myBigBroRepository.Set<CapturedImageGeoMarker>() on geoMarker.Id equals
                    capturedImageGeoMarker.GeoMarkerId
                join capturedImage in _myBigBroRepository.Set<CapturedImage>() on
                    capturedImageGeoMarker.CapturedImageId equals capturedImage.Id
                join device in _myBigBroRepository.Set<Device>() on
                    geoMarker.DeviceId equals device.Id
                where device.DeviceName == deviceName
                select new CapturedImageGeoMarkerDetail {CapturedImage = capturedImage, GeoMarker = geoMarker})
                .OrderByDescending(q => q.CapturedImage.Id)
                .Take(top);
            return capturedImagesGeoMarkersDetail;
        }

        public int GetCountOfMarkersWithImage(string deviceName)
        {
            var countOfCapturedImages = (from geoMarker in _myBigBroRepository.Set<GeoMarker>()
                join capturedImageGeoMarker in
                    _myBigBroRepository.Set<CapturedImageGeoMarker>() on geoMarker.Id equals
                    capturedImageGeoMarker.GeoMarkerId
                join capturedImage in _myBigBroRepository.Set<CapturedImage>() on
                    capturedImageGeoMarker.CapturedImageId equals capturedImage.Id
                join device in _myBigBroRepository.Set<Device>() on
                    geoMarker.DeviceId equals device.Id
                where device.DeviceName == deviceName
                select new CapturedImageGeoMarkerDetail {CapturedImage = capturedImage, GeoMarker = geoMarker})
                .Count();
            return countOfCapturedImages;
        }

        public IEnumerable<CapturedImageGeoMarkerDetail> GetMarkersWithImageForTimeframe(DateTime startDateTime, DateTime endDateTime)
        {
            var capturedImagesGeoMarkersDetail = (from geoMarker in _myBigBroRepository.Set<GeoMarker>()
                join capturedImageGeoMarker in
                    _myBigBroRepository.Set<CapturedImageGeoMarker>() on geoMarker.Id
                    equals
                    capturedImageGeoMarker.GeoMarkerId
                join capturedImage in _myBigBroRepository.Set<CapturedImage>() on
                    capturedImageGeoMarker.CapturedImageId equals capturedImage.Id
                select
                    new CapturedImageGeoMarkerDetail
                    {CapturedImage = capturedImage, GeoMarker = geoMarker})
                .OrderByDescending(q => q.CapturedImage.Id)
                .Where(t => t.GeoMarker.MarkerDateTime > startDateTime && t.GeoMarker.MarkerDateTime < endDateTime);
            return capturedImagesGeoMarkersDetail;
        }

        public IEnumerable<CapturedImageGeoMarkerDetail> GetMarkersWithImageForTimeframe(DateTime startDateTime, DateTime endDateTime, string deviceName)
        {
            var capturedImagesGeoMarkersDetail = (from geoMarker in _myBigBroRepository.Set<GeoMarker>()
                join capturedImageGeoMarker in
                    _myBigBroRepository.Set<CapturedImageGeoMarker>() on geoMarker.Id
                    equals
                    capturedImageGeoMarker.GeoMarkerId
                join capturedImage in _myBigBroRepository.Set<CapturedImage>() on
                    capturedImageGeoMarker.CapturedImageId equals capturedImage.Id
                join device in _myBigBroRepository.Set<Device>() on
                    geoMarker.DeviceId equals device.Id
                where device.DeviceName == deviceName
                select
                    new CapturedImageGeoMarkerDetail {CapturedImage = capturedImage, GeoMarker = geoMarker})
                .OrderByDescending(q => q.CapturedImage.Id)
                .Where(t => t.GeoMarker.MarkerDateTime > startDateTime && t.GeoMarker.MarkerDateTime < endDateTime);
            return capturedImagesGeoMarkersDetail;
        }

        public IGeoMarker GetLatestGeoMarkerForDevice(string deviceName)
        {
            var geoMarker = (from geoMarkers in _myBigBroRepository.Set<GeoMarker>()
                join device in _myBigBroRepository.Set<Device>() on
                    geoMarkers.DeviceId equals device.Id
                where device.DeviceName == deviceName
                select geoMarkers).OrderByDescending(g => g.Id);
            return geoMarker.FirstOrDefault();
        }
    }
}
