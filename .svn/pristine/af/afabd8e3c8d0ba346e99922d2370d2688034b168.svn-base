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
            var context = new MyBigBroContext(Settings.Default.connectionString);  

            //Act
            var items = context.SetWrapper<WebCam>().ToList();

            //Assert
            Assert.AreEqual(2, items.Count());
            Assert.AreEqual("bernard", items[0].Name);
            Assert.AreEqual("bernard2", items[1].Name);
        }

        [Test]
        public void TestAdd()
        {
            //Arrange            
            var context = new MyBigBroContext(Settings.Default.connectionString);
            var capturedImage = new CapturedImage()
                                    {DateTimeCaptured = DateTime.Now, Url = "https://test", WebCamId = 1};

            //Act
            context.Add<CapturedImage>(capturedImage);
            context.SaveChanges();
            var items = context.SetWrapper<CapturedImage>().ToList();

            //Assert
            Assert.AreEqual(1, items.Count());
        }
    }
}
