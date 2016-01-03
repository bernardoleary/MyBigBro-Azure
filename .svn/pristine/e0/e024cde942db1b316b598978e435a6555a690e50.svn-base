using Infostructure.MyBigBro.Domain;
using Infostructure.MyBigBro.Domain.Factory;
using NUnit.Framework;

namespace UnitTests.Infostructure.MyBigBro.Domain
{
    [TestFixture]
    public class UnitTestDomainModelFactory
    {
        [Test]
        public void TestGetObject()
        {
            // arrange
            string userName = "test1";
            string password = "password1";
            var domainModelFactory = new DomainModelFactory();

            // act
            var obj = domainModelFactory.GetObject<Credentials>(userName, password);

            // assert
            Assert.NotNull(obj);
        }
    }
}
