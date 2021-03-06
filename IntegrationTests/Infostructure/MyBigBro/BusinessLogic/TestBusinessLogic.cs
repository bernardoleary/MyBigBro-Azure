﻿using Infostructure.MyBigBro.BusinessLogic.GeoSpatial;
using Infostructure.MyBigBro.BusinessLogic.WebCam;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.DataModel.Models;
using Infostructure.MyBigBro.Domain;
using Infostructure.MyBigBro.ImageStorageServiceAgent;
using IntegrationTests.Properties;
using NUnit.Framework;

namespace IntegrationTests.Infostructure.MyBigBro.BusinessLogic
{
    [TestFixture]
    public class TestBusinessLogic
    {
        [Test]
        public void TestImageCaptureAndStorage()
        {
            var context = new MyBigBroContext(Settings.Default.connectionString);
            var repository = new MyBigBroRepository();
            var location = new Location(repository);
            var geometry = new Geometry();
            repository.Context = context;
            location.MyBigBroRepository = repository;
            var pointInRangeOfWebCamId4Only = new Point { XCoord = 174.706, YCoord = -36.872 };
            if (location.IsPointWithinWebCamRadiusOfVisibility(pointInRangeOfWebCamId4Only))
            {
                var webCamControl = new WebCamControl(new AwsStorageServiceAgent(), repository, geometry);
                webCamControl.MyBigBroRepository = repository;
                webCamControl.StorageServiceAgent = new AwsStorageServiceAgent();
                var webCams = location.GetWebCamsWithinWebCamRadiusOfVisibility(pointInRangeOfWebCamId4Only);
                foreach (var webCam in webCams)
                {
                    webCamControl.WebCam = webCam;
                    webCamControl.WebCamImage = new WebCamImage();
                    webCamControl.CapturedImage = new CapturedImage();
                    webCamControl.CaptureCurrentImage(new WebCamDataRequest());
                    webCamControl.StoreCapturedImage();
                }
            }
        }
    }
}
