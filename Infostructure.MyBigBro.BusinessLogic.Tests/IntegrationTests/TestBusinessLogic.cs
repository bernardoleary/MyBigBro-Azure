﻿using Infostructure.MyBigBro.BusinessLogic.GeoSpatial;
using Infostructure.MyBigBro.BusinessLogic.Tests.Properties;
using Infostructure.MyBigBro.BusinessLogic.WebCam;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.DataModel.Models;
using Infostructure.MyBigBro.Domain;
using Infostructure.MyBigBro.ImageStorageServiceAgent;
using NUnit.Framework;

namespace Infostructure.MyBigBro.BusinessLogic.Tests.IntegrationTests
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
            repository.Context = context;
            location.MyBigBroRepository = repository;
            var pointInRangeOfWebCamId4Only = new Point { XCoord = 174.706, YCoord = -36.872 };
            if (location.IsPointWithinWebCamRadiusOfVisibility(pointInRangeOfWebCamId4Only))
            {
                var webCamControl = new WebCamControl(new AwsStorageServiceAgent(), repository);
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
