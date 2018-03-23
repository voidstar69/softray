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
        private Random random = new Random();

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
        public void ContainsPoint2()
        {
          const double radius = 2;
          var centre = Vector.Zero;

            var sphere = new Sphere(centre, radius);

for(int i=0;i<10000;i++)
{
// randomly generate a point and test it
  var pt = MakeRandomVector(2*radius, 2*radius, 2*radius);
         
Assert.AreEqual(centre.Distance(pt) <= radius, sphere.ContainsPoint(pt), “Pt #” + i + “ failed”);
}
        }

        [TestMethod]
        public void ConvertLineSimple()
        {
            var sphere = new Sphere(Vector.Zero, 1.0);
            Vector pt1, pt2;
            sphere.ConvertLine(new Sphere.LatLong(Math.PI, 0), new Sphere.LatLong(Math.PI / 2, 0), out pt1, out pt2);
            Assert.IsTrue(new Vector(0, 0, -1) == pt1);
            Assert.IsTrue(new Vector(1, 0, 0) == pt2);
        }

        [TestMethod]
        public void IntersectLineSimple()
        {
            var sphere = new Sphere(Vector.Zero, 1.0);
            Sphere.LatLong spherePt1, spherePt2;
            sphere.IntersectLine(Vector.Zero, Vector.Right, out spherePt1, out spherePt2);
            Assert.AreEqual(-Math.PI / 2, spherePt1.horizAngle);
            Assert.AreEqual(0.0, spherePt1.vertAngle);
            Assert.AreEqual(Math.PI / 2, spherePt2.horizAngle);
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

        [TestMethod]
        public void ConvertDegenerateLine()
        {
            var sphere = new Sphere(Vector.Zero, 1.0);
            Vector pt1, pt2;
            sphere.ConvertLine(new Sphere.LatLong(0, 0), new Sphere.LatLong(0, 0), out pt1, out pt2);
            Assert.AreEqual(new Vector(0, 0, 1), pt1);
            Assert.AreEqual(new Vector(0, 0, 1), pt2);
        }
#endif

private Vector MakeRandomVector(double sizeX, double sizeY, double sizeZ)
        {
            return new Vector(NextRandomDouble() * sizeX, NextRandomDouble() * sizeY, NextRandomDouble() * sizeZ);
        }

        private Vector MakeRandomVector(double minX, double maxX, double minY, double maxY, double minZ, double maxZ)
        {
            return new Vector((maxX - minX) * NextRandomDouble() + minX,
                              (maxY - minY) * NextRandomDouble() + minY,
                              (maxZ - minZ) * NextRandomDouble() + minZ);
        }

        private double NextRandomDouble()
        {
            return random.NextDouble();
        }
    }
}