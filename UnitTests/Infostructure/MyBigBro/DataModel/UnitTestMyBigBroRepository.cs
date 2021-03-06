﻿using System.Collections.Generic;
using System.Linq;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.DataModel.Models;
using Moq;
using NUnit.Framework;

namespace UnitTests.Infostructure.MyBigBro.DataModel
{
    [TestFixture]
    public class UnitTestMyBigBroRepository
    {
        [Test]
        public void TestSet()
        {
            //Arrange
            var mockContext = new Mock<IDbContext>();
            mockContext.Setup(x => x.SetWrapper<WebCam>()).Returns(new List<WebCam>()
            {
                new WebCam() {Id = 0, Name = "bernard", XCoord = 12.45, YCoord = 23.56},
                new WebCam() {Id = 2, Name = "bernard2", XCoord = 77.9, YCoord = 23.56}
            }.AsQueryable());
            var repository = new MyBigBroRepository() { Context = mockContext.Object };

            //Act
            var items = (from webCam in repository.Set<WebCam>()
                        where webCam.Id == 0
                        select webCam).ToList<WebCam>();

            //Assert
            mockContext.Verify(x => x.SetWrapper<WebCam>());
            Assert.AreEqual("bernard", items[0].Name);
        }
    }
}
