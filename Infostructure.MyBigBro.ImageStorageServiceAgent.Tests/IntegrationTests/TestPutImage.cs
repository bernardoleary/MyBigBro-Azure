﻿using System;
using System.Drawing;
using System.IO;
using System.Net;
using Infostructure.MyBigBro.Domain;
using NUnit.Framework;

namespace Infostructure.MyBigBro.ImageStorageServiceAgent.Tests.IntegrationTests
{
    [TestFixture]
    public class UnitTestsServiceAgent
    {
        [Test]
        public void TestPutImage()
        {
            // arrange - get an image
            var request = WebRequest.Create("http://www.rta.nsw.gov.au/trafficreports/cameras/camera_images/harbourbridge.jpg");
            Stream stream = null;
            IWebCamImage webCamImageData = null;
            string key = null;
            using (var response = request.GetResponse())
            {
                stream = response.GetResponseStream();
                var image = Image.FromStream(stream);
                webCamImageData = new WebCamImage { Data = ImageToByteArray(image) };

                // Temp - collecting data
                var base64StringImage = ImageToByteString(image);

            }            

            // act - load the stream into an image     
            //var agent = new AwsStorageServiceAgent();
            var agent = new AzureStorageServiceAgent();
            var output = agent.PutImage(webCamImageData, out key);

            // assert - if greater than zero, something happened
            Assert.Greater(output.Length, 0);
            Assert.Greater(key.Length, 0);
            Assert.Greater(output.Length, key.Length);
        }

        private byte[] ImageToByteArray(System.Drawing.Image image)
        {
            MemoryStream ms = new MemoryStream();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            return ms.ToArray();
        }

        private string ImageToByteString(System.Drawing.Image image)
        {
            using (var ms = new MemoryStream())
            {
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }
}
