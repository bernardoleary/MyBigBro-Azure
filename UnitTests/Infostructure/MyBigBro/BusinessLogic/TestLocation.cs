using System.Collections.Generic;
using System.Linq;
using Infostructure.MyBigBro.BusinessLogic.GeoSpatial;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.DataModel.Models;
using Moq;
using NUnit.Framework;

namespace UnitTests.Infostructure.MyBigBro.BusinessLogic
{
    [TestFixture]
    public class TestLocation
    {
        private Mock<IMyBigBroRepository> myBigBroRepository = null;

        [SetUp]
        public void SetUpUnitTestsLocation()
        {
            myBigBroRepository = new Mock<IMyBigBroRepository>();
        }

        [Test]
        public void TestIsPointWithinWebCamRadiusOfVisibility()
        {
            //Arrange
            var pointInRangeOfId0Only = new Point { XCoord = 1, YCoord = 1 };
            var pointInRangeOfId2Only = new Point { XCoord = 9, YCoord = 9 };
            var pointInRangeOfId1AndId2 = new Point { XCoord = 4, YCoord = 4 };
            var pointNotInRange = new Point { XCoord = 100, YCoord = 100 };
            var mockRepository = new Mock<IMyBigBroRepository>();
            mockRepository.Setup(x => x.Set<global::Infostructure.MyBigBro.DataModel.Models.WebCam>()).Returns(new List<WebCam>()
            {
                new global::Infostructure.MyBigBro.DataModel.Models.WebCam() {Id = 0, Name = "bernard", XCoord = 2, YCoord = 2, RadiusOfVisibility = 3},
                new global::Infostructure.MyBigBro.DataModel.Models.WebCam() {Id = 2, Name = "bernard2", XCoord = 8, YCoord = 8, RadiusOfVisibility = 7}
            }.AsQueryable());
            var location = new Location(mockRepository.Object);

            //Act
            var pointInRangeOfId0OnlyResult = location.IsPointWithinWebCamRadiusOfVisibility(pointInRangeOfId0Only, false);
            var pointInRangeOfId2OnlyResult = location.IsPointWithinWebCamRadiusOfVisibility(pointInRangeOfId2Only, false);
            var pointInRangeOfId1AndId2Result = location.IsPointWithinWebCamRadiusOfVisibility(pointInRangeOfId1AndId2, false);
            var pointNotInRangeResult = location.IsPointWithinWebCamRadiusOfVisibility(pointNotInRange, false);

            //Assert
            Assert.IsTrue(pointInRangeOfId0OnlyResult);
            Assert.IsTrue(pointInRangeOfId2OnlyResult);
            Assert.IsTrue(pointInRangeOfId1AndId2Result);
            Assert.IsFalse(pointNotInRangeResult);
        }

        [Test]
        public void TestMapCapturedImageToGeoMarker()
        {
            //Arrange
            var location = new Location(myBigBroRepository.Object);

            //Act
            var result = location.MapCapturedImageToGeoMarker(1, 2);

            //Assert
            Assert.NotNull(result);
        }
    }
}
