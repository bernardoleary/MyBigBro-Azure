using Infostructure.MyBigBro.BusinessLogic.GeoSpatial;
using Infostructure.MyBigBro.DataModel.DataAccess;
using IntegrationTests.Properties;
using NUnit.Framework;

namespace IntegrationTests.Infostructure.MyBigBro.BusinessLogic
{
    [TestFixture]
    public class TestLocation
    {
        [Test]
        public void TestIsPointWithinWebCamRadiusOfVisibility()
        {
            //Arrange (using seeded NZTA webcam coordinates in PostgreSQL)
            var pointInRangeOfBondSt = new Point { XCoord = 174.746, YCoord = -36.869 };
            var pointInRangeOfGtNorthRd = new Point { XCoord = 174.706, YCoord = -36.872 };
            var pointNotInRange = new Point { XCoord = 0.0, YCoord = 0.0 };
            using var context = new MyBigBroContext(Settings.Default.connectionString);
            var repository = new MyBigBroRepository() { Context = context };
            var location = new Location(repository);

            //Act
            var inRange1 = location.IsPointWithinWebCamRadiusOfVisibility(pointInRangeOfBondSt);
            var inRange2 = location.IsPointWithinWebCamRadiusOfVisibility(pointInRangeOfGtNorthRd);
            var notInRange = location.IsPointWithinWebCamRadiusOfVisibility(pointNotInRange);

            //Assert
            Assert.IsTrue(inRange1);
            Assert.IsTrue(inRange2);
            Assert.IsFalse(notInRange);
        }

        [Test]
        public void TestGetWebCams()
        {
            //Arrange
            using var context = new MyBigBroContext(Settings.Default.connectionString);
            var repository = new MyBigBroRepository() { Context = context };
            var location = new Location(repository);

            //Act
            var webCams = location.GetWebCams();

            //Assert
            Assert.Greater(webCams.Count, 10);
        }
    }
}
