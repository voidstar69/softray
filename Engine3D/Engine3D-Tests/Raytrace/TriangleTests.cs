﻿#define APPVEYOR_PERFORMANCE_MARGINS

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Engine3D.Raytrace;
using Vector = Engine3D.Vector;

namespace Engine3D_Tests
{
    [TestClass]
    public class TriangleTests
    {
        private const uint color = 0xffffffff; // opaque white
        private readonly static Vector origin = new Vector();
        private readonly static Vector right = new Vector(1, 0, 0);
        private readonly static Vector up = new Vector(0, 1, 0);
        private readonly static Vector forward = new Vector(0, 0, -1);
        private readonly static Vector backward = new Vector(0, 0, 1);
        private readonly static Random random = new Random();
        private readonly static RenderContext context = new RenderContext(random);

        [TestMethod]
        public void CreateZeroSizeTriangle()
        {
            var tri = new Triangle(origin, origin, origin, color);
            Assert.IsNotNull(tri);
            Assert.AreEqual(origin, tri.Vertex1);
            Assert.AreEqual(origin, tri.Vertex2);
            Assert.AreEqual(origin, tri.Vertex3);
        }

        [TestMethod]
        public void RayHitsTriangle()
        {
            var tri = new Triangle(origin, right, up, color);
            var info = tri.IntersectRay(origin + backward, forward, context);
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
            var info = tri.IntersectRay(right, forward, context);
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
            var info = tri.IntersectRay(origin + forward, backward, context);
            Assert.IsNull(info);
        }

        [TestMethod]
        public void RayIntersectPlanePerformance()
        {
            // a million rays takes ~300ms in Release mode; ~650ms in Debug mode (all with laptop on Power Saver mode)
#if DEBUG
            const double minMillionRaysPerSec = 0.9;
            const double maxMillionRaysPerSec = 1.7;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 7.9;
            const double maxMillionRaysPerSec = 10.8;
#else
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 4.0;
            const double maxMillionRaysPerSec = 6.0;
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
                var info = plane.IntersectRay(start, dir, context);
                if (info != null)
                    numRaysHit++;
            }
            var elapsedTime = DateTime.Now - startTime;
            Assert.IsTrue(numRays * 0.498 < numRaysHit && numRaysHit < numRays * 0.502, "Num rays hit {0} should be roughly half of total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f2} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("Performance: {0} million rays per second", millionRaysPerSec);
        }

        [TestMethod]
        public void RayIntersectTrianglePerformance()
        {
            // a million rays takes ~300ms in Release mode; ~850ms in Debug mode (all with laptop on Power Saver mode)
#if DEBUG
            const double minMillionRaysPerSec = 0.7;
            const double maxMillionRaysPerSec = 1.3;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 7.9;
            const double maxMillionRaysPerSec = 12.9;
#else
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 4.5;
            const double maxMillionRaysPerSec = 7.1;
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
                var info = tri.IntersectRay(start, dir, context);
                if (info != null)
                    numRaysHit++;
            }
            var elapsedTime = DateTime.Now - startTime;
            Assert.IsTrue(numRays * 0.498 < numRaysHit && numRaysHit < numRays * 0.502, "Num rays hit {0} should be roughly half of total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f2} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("Performance: {0} million rays per second", millionRaysPerSec);
        }

        [TestMethod]
        public void RayIntersectSphereFromInside_Performance()
        {
            // TODO: a million rays takes ~500ms in Release mode; ~1s in Debug mode (all with laptop on Power Saver mode)
            // TODO: optimise ray-sphere intersection code
            // TODO: this only tests the case of ray hitting the sphere. Also test performance of misses and near-misses, or aggregate performance.
#if DEBUG
            // TODO
            const double minMillionRaysPerSec = 0.9;
            const double maxMillionRaysPerSec = 1.3;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 3.9;
            const double maxMillionRaysPerSec = 5.9;
#else
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 3.5;
            const double maxMillionRaysPerSec = 4.5;
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
                var info = sphere.IntersectRay(start, dir, context);
                if (info != null)
                    numRaysHit++;
            }
            var elapsedTime = DateTime.Now - startTime;
            Assert.AreEqual(numRays, numRaysHit, "Num rays hit {0} should the same as total rays {1}", numRaysHit, numRays);
            //Assert.IsTrue(numRays * 0.498 < numRaysHit && numRaysHit < numRays * 0.502, "Num rays hit {0} should be roughly half of total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f2} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("Performance: {0} million rays per second", millionRaysPerSec);
        }

        [TestMethod]
        public void RayIntersectSphereMostlyFromOutside_Performance()
        {
#if DEBUG
            // TODO
            const double minMillionRaysPerSec = 0.7;
            const double maxMillionRaysPerSec = 1.0;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 3.9;
            const double maxMillionRaysPerSec = 5.9;
#else
            // my laptop on Power Saver mode
            const double minMillionRaysPerSec = 3.0;
            const double maxMillionRaysPerSec = 4.1;
#endif

            const int numRays = 1000000;
            var sphere = new Sphere(new Vector(0.5, 0.5, 0.5), 1);
            var numRaysHit = 0;

            DateTime startTime = DateTime.Now;
            for (var i = 0; i < numRays; i++)
            {
                var start = MakeRandomVector(-10, 10, -10, 10, -10, 10);
                var end = MakeRandomVector(1, 1, 1);
                var dir = end - start;
                var info = sphere.IntersectRay(start, dir, context);
                if (info != null)
                    numRaysHit++;
            }
            var elapsedTime = DateTime.Now - startTime;
            Assert.AreEqual(numRays, numRaysHit, "Num rays hit {0} should the same as total rays {1}", numRaysHit, numRays);
            //Assert.IsTrue(numRays * 0.498 < numRaysHit && numRaysHit < numRays * 0.502, "Num rays hit {0} should be roughly half of total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f2} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("Performance: {0} million rays per second", millionRaysPerSec);
        }

        [TestMethod]
        public void BaselineTestOfPerformance()
        {
#if DEBUG
            // TODO
            const double minMillionRaysPerSec = 1.8;
            const double maxMillionRaysPerSec = 2.9;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 8.5;
            const double maxMillionRaysPerSec = 13.0;
#else
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 4.5;
            const double maxMillionRaysPerSec = 8.0;
#endif

            const int numRays = 1000000;
            var numRaysHit = 0;

            DateTime startTime = DateTime.Now;
            for (var i = 0; i < numRays; i++)
            {
                var start = MakeRandomVector(-10, 10, -10, 10, -10, 10);
                var dir = MakeRandomVector(1, 1, 1);
                var info = new IntersectionInfo();
                if (info != null)
                    numRaysHit++;
            }
            var elapsedTime = DateTime.Now - startTime;
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f2} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("Performance: {0} million rays per second", millionRaysPerSec);
        }

        [TestMethod]
        public void RayIntersectAABBPerformance()
        {
            // a million rays takes ~900ms in Release mode; ~4s in Debug mode (all with laptop on Power Saver mode)
#if DEBUG
            const double minMillionRaysPerSec = 0.35;
            const double maxMillionRaysPerSec = 0.55;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 2.9;
            const double maxMillionRaysPerSec = 3.8;
#else
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 1.2;
            const double maxMillionRaysPerSec = 2.5;
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
                var info = box.IntersectRay(start, dir, context);
                if (info != null)
                    numRaysHit++;
            }
            var elapsedTime = DateTime.Now - startTime;
            Assert.IsTrue(numRays * 0.998 < numRaysHit && numRaysHit < numRays * 1.0, "Num rays hit {0} should be roughly the same as total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f3} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("Performance: {0} million rays per second", millionRaysPerSec);
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
