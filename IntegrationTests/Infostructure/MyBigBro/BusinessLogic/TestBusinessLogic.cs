using System;
using System.IO;
using Infostructure.MyBigBro.BusinessLogic.GeoSpatial;
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
        private static readonly byte[] SampleJpegBytes = Convert.FromBase64String(
            "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=");

        [Test]
        public void TestImageCaptureAndStorage()
        {
            using var context = new MyBigBroContext(Settings.Default.connectionString);
            var repository = new MyBigBroRepository { Context = context };
            var location = new Location(repository);
            var geometry = new Geometry();
            var pointInRangeOfWebCamId4Only = new Point { XCoord = 174.706, YCoord = -36.872 };

            Assert.IsTrue(location.IsPointWithinWebCamRadiusOfVisibility(pointInRangeOfWebCamId4Only));

            var storageAgent = new AzureStorageServiceAgent(new StorageOptions());
            var webCamControl = new WebCamControl(storageAgent, repository, geometry);
            var webCams = location.GetWebCamsWithinWebCamRadiusOfVisibility(pointInRangeOfWebCamId4Only);
            Assert.IsNotEmpty(webCams);

            foreach (var webCam in webCams)
            {
                webCamControl.WebCam = webCam;
                webCamControl.WebCamImage = new WebCamImage { Data = SampleJpegBytes };
                webCamControl.CapturedImage = new CapturedImage();
                var capturedId = webCamControl.StoreCapturedImage();
                Assert.Greater(capturedId, 0);
            }
        }
    }
}
