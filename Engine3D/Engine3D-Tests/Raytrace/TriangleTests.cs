using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Engine3D.Raytrace;
using Vector = Engine3D.Vector;

namespace Engine3D_Tests
{
    [TestClass]
    public class TriangleTests
    {
        private static Random random = new Random();

        const uint color = 12345;
        readonly static Vector origin = new Vector();
        readonly static Vector right = new Vector(1, 0, 0);
        readonly static Vector up = new Vector(0, 1, 0);
        readonly static Vector forward = new Vector(0, 0, -1);
        readonly static Vector backward = new Vector(0, 0, 1);

        [TestMethod]
        public void CreateZeroSizeTriangle()
        {
            var tri = new Triangle(origin, origin, origin, 0);
            Assert.IsNotNull(tri);
            Assert.AreEqual(origin, tri.Vertex1);
            Assert.AreEqual(origin, tri.Vertex2);
            Assert.AreEqual(origin, tri.Vertex3);
        }

        [TestMethod]
        public void RayHitsTriangle()
        {
            var tri = new Triangle(origin, right, up, color);
            var info = tri.IntersectRay(origin + backward, forward);
            Assert.IsNotNull(info);
            Assert.AreEqual(origin, info.pos);
            Assert.AreEqual(backward, info.normal);
            Assert.AreEqual(1.0, info.rayFrac);
            Assert.AreEqual(color, info.color);
        }

        [TestMethod]
        public void RayFromTriangleVertex_HitsTriangle()
        {
            var tri = new Triangle(origin, right, up, color);
            var info = tri.IntersectRay(right, forward);
            Assert.IsNotNull(info);
            Assert.AreEqual(right, info.pos);
            Assert.AreEqual(backward, info.normal);
            Assert.AreEqual(0.0, info.rayFrac);
            Assert.AreEqual(color, info.color);
        }

        [TestMethod]
        public void TriangleIsOneSided_RayFromOtherSideMisses()
        {
            var tri = new Triangle(origin, right, up, color);
            var info = tri.IntersectRay(origin + forward, backward);
            Assert.IsNull(info);
        }

        [TestMethod]
        public void RayIntersectPlanePerformance()
        {
            // a million rays takes ~300ms in Release mode; ~650ms in Debug mode (all with laptop on Power Saver mode)
#if DEBUG
            const double minMillionRaysPerSec = 1.4;
            const double maxMillionRaysPerSec = 1.7;
#else
            // AppVeyor build server
            const double minMillionRaysPerSec = 7.9;
            const double maxMillionRaysPerSec = 10.8;

            // my laptop on Power Saver mode
            //const double minMillionRaysPerSec = 2.7;
            //const double maxMillionRaysPerSec = 3.1;
#endif

            const int numRays = 1000000;
            var plane = new Plane(forward * 100, new Vector(1, 1, 1));
            var numRaysHit = 0;

            DateTime startTime = DateTime.Now;
            for (var i = 0; i < numRays; i++)
            {
                var start = MakeRandomVector(1, 1, 1);
                var dir = MakeRandomVector(-1, 1, -1, 1, -1, 1);
                //var dir = forward;
                var info = plane.IntersectRay(start, dir);
                if (info != null)
                    numRaysHit++;
            }
            var elapsedTime = DateTime.Now - startTime;
            Assert.IsTrue(numRays * 0.498 < numRaysHit && numRaysHit < numRays * 0.502, "Num rays hit {0} should be roughly half of total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f2} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("A test of Console.WriteLine");
            Console.Out.WriteLine("A test of standard output");
            Console.Error.WriteLine("A test of standard error");
        }

        [TestMethod]
        public void RayIntersectTrianglePerformance()
        {
            // a million rays takes ~300ms in Release mode; ~850ms in Debug mode (all with laptop on Power Saver mode)
#if DEBUG
            const double minMillionRaysPerSec = 1.1;
            const double maxMillionRaysPerSec = 1.3;
#else
            // AppVeyor build server
            const double minMillionRaysPerSec = 7.9;
            const double maxMillionRaysPerSec = 11.0;

            // my laptop on Power Saver mode
            //const double minMillionRaysPerSec = 3.0;
            //const double maxMillionRaysPerSec = 3.7;
#endif

            const int numRays = 1000000;
            var tri = new Triangle(origin, right, up, color);
            var numRaysHit = 0;

            DateTime startTime = DateTime.Now;
            for (var i = 0; i < numRays; i++)
            {
                var start = MakeRandomVector(1, 1, 1);
                //var dir = MakeRandomVector(1, 1, -1);
                var dir = forward;
                var info = tri.IntersectRay(start, dir);
                if (info != null)
                    numRaysHit++;
            }
            var elapsedTime = DateTime.Now - startTime;
            Assert.IsTrue(numRays * 0.498 < numRaysHit && numRaysHit < numRays * 0.502, "Num rays hit {0} should be roughly half of total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f2} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
        }

        [TestMethod]
        public void RayIntersectSpherePerformance()
        {
            // TODO: a million rays takes ~500ms in Release mode; ~1s in Debug mode (all with laptop on Power Saver mode)
            // TODO: optimise ray-sphere intersection code
#if DEBUG
            // TODO
            const double minMillionRaysPerSec = 0.70;
            const double maxMillionRaysPerSec = 1.15;
#else
            // AppVeyor build server
            const double minMillionRaysPerSec = 3.9;
            const double maxMillionRaysPerSec = 5.9;

            // my laptop on Power Saver mode
            //const double minMillionRaysPerSec = 1.7;
            //const double maxMillionRaysPerSec = 2.0;
#endif

            const int numRays = 1000000;
            var sphere = new Sphere(new Vector(0.5, 0.5, 0.5), 1);
            var numRaysHit = 0;

            DateTime startTime = DateTime.Now;
            for (var i = 0; i < numRays; i++)
            {
                var start = MakeRandomVector(1, 1, 1);
                var dir = MakeRandomVector(-1, 1, -1, 1, -1, 1);
                //var dir = forward;
                var info = sphere.IntersectRay(start, dir);
                if (info != null)
                    numRaysHit++;
            }
            var elapsedTime = DateTime.Now - startTime;
            Assert.AreEqual(numRays, numRaysHit, "Num rays hit {0} should the same as total rays {1}", numRaysHit, numRays);
            //Assert.IsTrue(numRays * 0.498 < numRaysHit && numRaysHit < numRays * 0.502, "Num rays hit {0} should be roughly half of total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f2} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
        }

        [TestMethod]
        public void RayIntersectAABBPerformance()
        {
            // a million rays takes ~900ms in Release mode; ~4s in Debug mode (all with laptop on Power Saver mode)
#if DEBUG
            const double minMillionRaysPerSec = 0.50;
            const double maxMillionRaysPerSec = 0.55;
#else
            // AppVeyor build server
            const double minMillionRaysPerSec = 2.9;
            const double maxMillionRaysPerSec = 3.8;

            // my laptop on Power Saver mode
            //const double minMillionRaysPerSec = 0.9;
            //const double maxMillionRaysPerSec = 1.2;
#endif

            const int numRays = 1000000;
            var box = new AxisAlignedBox(new Vector(-0.5, -0.5, -0.5), new Vector(0.5, 0.5, 0.5));
            var numRaysHit = 0;

            DateTime startTime = DateTime.Now;
            for (var i = 0; i < numRays; i++)
            {
                //var start = new Vector(0.5, 0.5, 0.5);
                var start = MakeRandomVector(-0.3, 0.3, -0.3, 0.3, 0.5, 1);
                var dir = MakeRandomVector(-0.5, 0.5, -0.5, 0.5, -1, -1);
                //var dir = MakeRandomVector(1, 1, -1);
                var info = box.IntersectRay(start, dir);
                if (info != null)
                    numRaysHit++;
            }
            var elapsedTime = DateTime.Now - startTime;
            Assert.IsTrue(numRays * 0.998 < numRaysHit && numRaysHit < numRays * 1.0, "Num rays hit {0} should be roughly the same as total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f3} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
        }

        private Vector MakeRandomVector(double sizeX, double sizeY, double sizeZ)
        {
            return new Vector(random.NextDouble() * sizeX, random.NextDouble() * sizeY, random.NextDouble() * sizeZ);
        }

        private Vector MakeRandomVector(double minX, double maxX, double minY, double maxY, double minZ, double maxZ)
        {
            return new Vector((maxX - minX) * random.NextDouble() + minX,
                              (maxY - minY) * random.NextDouble() + minY,
                              (maxZ - minZ) * random.NextDouble() + minZ);
        }
    }
}