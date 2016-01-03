using Infostructure.MyBigBro.BusinessLogic.GeoSpatial;
using Infostructure.MyBigBro.BusinessLogic.Tests.Properties;
using Infostructure.MyBigBro.DataModel.DataAccess;
using NUnit.Framework;

namespace Infostructure.MyBigBro.BusinessLogic.Tests.IntegrationTests
{
    [TestFixture]
    public class TestLocation
    {
        [Test]
        public void TestIsPointWithinWebCamRadiusOfVisibility()
        {
            //Arrange
            var pointInRangeOfId0Only = new Point { XCoord = 1, YCoord = 1 };
            var pointInRangeOfId2Only = new Point { XCoord = 9, YCoord = 9 };
            var pointInRangeOfId1AndId2 = new Point { XCoord = 4, YCoord = 4 };
            var pointNotInRange = new Point { XCoord = 100, YCoord = 100 };
            var repository = new MyBigBroRepository() { Context = new MyBigBroContext(Settings.Default.connectionString) };
            var location = new Location(repository);

            //Act
            var pointInRangeOfId0OnlyResult = location.IsPointWithinWebCamRadiusOfVisibility(pointInRangeOfId0Only);
            var pointInRangeOfId2OnlyResult = location.IsPointWithinWebCamRadiusOfVisibility(pointInRangeOfId2Only);
            var pointInRangeOfId1AndId2Result = location.IsPointWithinWebCamRadiusOfVisibility(pointInRangeOfId1AndId2);
            var pointNotInRangeResult = location.IsPointWithinWebCamRadiusOfVisibility(pointNotInRange);

            //Assert
            Assert.IsTrue(pointInRangeOfId0OnlyResult);
            Assert.IsTrue(pointInRangeOfId2OnlyResult);
            Assert.IsTrue(pointInRangeOfId1AndId2Result);
            Assert.IsFalse(pointNotInRangeResult);
        }

        [Test]
        public void TestGetWebCams()
        {
            //Arrange
            string connectionString =
                //"Server=tcp:lcjoa99y8j.database.windows.net,1433;Database=bernardtest;User ID=bernardtest@lcjoa99y8j;Password=4Uckland;Trusted_Connection=False;Encrypt=True;Connection Timeout=30;";
                Settings.Default.connectionString;
            var repository = new MyBigBroRepository() { Context = new MyBigBroContext(Settings.Default.connectionString) };
            var location = new Location(repository);

            //Act
            var webCams = location.GetWebCams();

            //Assert
            Assert.Greater(webCams.Count, 10);
        }
    }
}
