using System;
using Infostructure.MyBigBro.Domain;
using Infostructure.MyBigBro.ImageStorageServiceAgent;
using NUnit.Framework;

namespace IntegrationTests.Infostructure.MyBigBro.ImageStorageAgent
{
    [TestFixture]
    public class UnitTestsServiceAgent
    {
        private static readonly byte[] SampleJpegBytes = Convert.FromBase64String(
            "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=");

        [Test]
        public void TestPutImage()
        {
            // arrange
            var webCamImageData = new WebCamImage { Data = SampleJpegBytes };
            var base64StringImage = Convert.ToBase64String(SampleJpegBytes);
            Assert.IsNotEmpty(base64StringImage);

            // act
            var agent = new ConfigurableStorageServiceAgent(
                Microsoft.Extensions.Options.Options.Create(new StorageOptions()));
            var output = agent.PutImage(webCamImageData, out var key);

            // assert
            Assert.Greater(output.Length, 0);
            Assert.Greater(key.Length, 0);
            Assert.Greater(output.Length, key.Length);
        }
    }
}
