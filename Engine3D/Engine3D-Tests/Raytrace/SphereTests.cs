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
        public void IntersectLineSegment()
        {
            var sphere = new Sphere(Vector.Zero, 1.0);
            sphere.IntersectLineSegment(Vector.Zero, Vector.Zero);
        }

        [TestMethod]
        public void ContainsPoint()
        {
            var sphere = new Sphere(Vector.Zero, 1.0);
            Assert.IsTrue(sphere.ContainsPoint(Vector.Zero));
            Assert.IsTrue(sphere.ContainsPoint(Vector.Right * 0.999));
            Assert.IsFalse(sphere.ContainsPoint(Vector.Right));
            Assert.IsFalse(sphere.ContainsPoint(Vector.Right * 1.0001));
            Assert.IsFalse(sphere.ContainsPoint(Vector.Right * 2));
        }

        [TestMethod]
        public void IntersectLineSimple()
        {
            var sphere = new Sphere(Vector.Zero, 1.0);
            Sphere.LatLong spherePt1, spherePt2;
            sphere.IntersectLine(Vector.Zero, Vector.Right, out spherePt1, out spherePt2);
            Assert.AreEqual(-1.5707963267948966, spherePt1.horizAngle);
            Assert.AreEqual(0.0, spherePt1.vertAngle);
            Assert.AreEqual(1.5707963267948966, spherePt2.horizAngle);
            Assert.AreEqual(0.0, spherePt2.vertAngle);
        }

        [TestMethod]
        public void LineGrazesSphereButDoesNotIntersect()
        {
            var sphere = new Sphere(Vector.Zero, 1.0);
            Sphere.LatLong spherePt1, spherePt2;
            sphere.IntersectLine(Vector.Forward, Vector.Right + Vector.Forward, out spherePt1, out spherePt2);
            Assert.IsNull(spherePt1);
            Assert.IsNull(spherePt2);
        }

#if !DEBUG
        [TestMethod]
        public void IntersectDegenerateLineCausesNans()
        {
            var sphere = new Sphere(Vector.Zero, 1.0);
            Sphere.LatLong spherePt1, spherePt2;
            sphere.IntersectLine(Vector.Zero, Vector.Zero, out spherePt1, out spherePt2);
            Assert.AreEqual(double.NaN, spherePt1.horizAngle);
            Assert.AreEqual(double.NaN, spherePt1.vertAngle);
            Assert.AreEqual(double.NaN, spherePt2.horizAngle);
            Assert.AreEqual(double.NaN, spherePt2.vertAngle);
        }
#endif
    }
}