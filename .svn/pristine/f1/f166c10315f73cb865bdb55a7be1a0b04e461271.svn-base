using Infostructure.MyBigBro.BusinessLogic.GeoSpatial;
using NUnit.Framework;

namespace UnitTests.Infostructure.MyBigBro.BusinessLogic
{
    [TestFixture]
    public class TestGeometry
    {
        [Test]
        public void TestGetDistancePythagoras()
        {
            // test variables - 3/4/5 triangle test
            var x1 = 2.5;
            var y1 = 2.5;
            var x2 = 5.5;
            var y2 = 6.5;
            var geometry = new Geometry();

            // run test object        
            var output = geometry.GetDistancePythagoras(x1, y1, x2, y2);

            // assert
            Assert.AreEqual(5.0, output);
            Assert.AreNotEqual(1.0, output);
        }
    }
}
