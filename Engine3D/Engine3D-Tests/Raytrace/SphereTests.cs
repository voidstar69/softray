using System;
using Engine3D;
using Engine3D.Raytrace;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Engine3D_Tests.Raytrace
{
    [TestClass]
    public class SphereTests
    {
        [TestMethod, ExpectedException(typeof(NotImplementedException))]
        public void IntersectLineSegmentTest()
        {
            var sphere = new Sphere(Vector.Zero, 1.0);
            sphere.IntersectLineSegment(Vector.Zero, Vector.Zero);
        }

        [TestMethod]
        public void ContainsPointTest()
        {
            var sphere = new Sphere(Vector.Zero, 1.0);
            Assert.IsTrue(sphere.ContainsPoint(Vector.Zero));
            Assert.IsTrue(sphere.ContainsPoint(Vector.Right * 0.999));
            Assert.IsFalse(sphere.ContainsPoint(Vector.Right));
            Assert.IsFalse(sphere.ContainsPoint(Vector.Right * 1.0001));
            Assert.IsFalse(sphere.ContainsPoint(Vector.Right * 2));
        }
    }
}