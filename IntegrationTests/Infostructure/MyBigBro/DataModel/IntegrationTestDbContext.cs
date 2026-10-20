using System;
using System.Linq;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.DataModel.Models;
using IntegrationTests.Properties;
using NUnit.Framework;

namespace IntegrationTests.Infostructure.MyBigBro.DataModel
{
    [TestFixture]
    public class IntegrationTestDbContext
    {
        [Test]
        public void TestSetWrapper()
        {
            //Arrange            
            using var context = new MyBigBroContext(Settings.Default.connectionString);  

            //Act
            var items = context.SetWrapper<WebCam>().OrderBy(w => w.Id).ToList();

            //Assert
            Assert.GreaterOrEqual(items.Count, 105);
            Assert.AreEqual("SH16 1 Bond St", items[0].Name);
        }

        [Test]
        public void TestAdd()
        {
            //Arrange            
            using var context = new MyBigBroContext(Settings.Default.connectionString);
            var beforeCount = context.SetWrapper<CapturedImage>().Count();
            var capturedImage = new CapturedImage()
            {
                DateTimeCaptured = DateTime.UtcNow,
                Url = "https://test",
                WebCamId = 3,
                IsThumbnailed = false,
                Key = "test-key.jpg"
            };

            //Act
            context.Add<CapturedImage>(capturedImage);
            context.SaveChanges();
            var afterCount = context.SetWrapper<CapturedImage>().Count();

            //Assert
            Assert.AreEqual(beforeCount + 1, afterCount);
        }
    }
}
