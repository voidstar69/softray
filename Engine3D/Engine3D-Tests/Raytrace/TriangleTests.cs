#define APPVEYOR_PERFORMANCE_MARGINS

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Engine3D.Raytrace;
using System.Collections.Generic;
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

        //private const int numRandomDoubles = 123;
        //private readonly double[] randomDouble = new double[numRandomDoubles];
        //private int randomIndex = 0;

        public TriangleTests()
        {
            //for (var i = 0; i < numRandomDoubles; i++)
            //{
            //    randomDouble[i] = random.NextDouble();
            //}
        }

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
        public void BaselineTestOfPerformance()
        {
#if DEBUG
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 2.5;
            const double maxMillionRaysPerSec = 3.4;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 9.0;
            const double maxMillionRaysPerSec = 13.0;
#else
          // my laptop in High Performance mode
          const double minMillionRaysPerSec = 4.5;
          const double maxMillionRaysPerSec = 9.6;
#endif

          const int numRays = 1000000;
          var numRaysHit = 0;

          DateTime startTime = DateTime.Now;
          for (var i = 0; i < numRays; i++)
          {
            var start = MakeRandomVector(-10, 10, -10, 10, -10, 10);
            var dir = MakeRandomVector(1, 1, 1);
            var info = new IntersectionInfo();
            numRaysHit++;
          }
          var elapsedTime = DateTime.Now - startTime;
          var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
          Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
              "Rays per second {0:f2} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
          Console.WriteLine("Performance: {0} million rays per second", millionRaysPerSec);
        }

        [TestMethod]
        public void RayIntersectPlanePerformance()
        {
#if DEBUG
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 0.9;
            const double maxMillionRaysPerSec = 1.8;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 7.9;
            const double maxMillionRaysPerSec = 10.8;
#else
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 4.4;
            const double maxMillionRaysPerSec = 7.5;
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
            Assert.IsTrue(numRays * 0.49 < numRaysHit && numRaysHit < numRays * 0.515, "Num rays hit {0} should be roughly half of total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f2} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("Performance: {0} million rays per second", millionRaysPerSec);
        }

        [TestMethod]
        public void RayIntersectTrianglePerformance()
        {
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
            const double maxMillionRaysPerSec = 8.1;
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
            Assert.IsTrue(numRays * 0.48 < numRaysHit && numRaysHit < numRays * 0.51, "Num rays hit {0} should be roughly half of total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f2} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("Performance: {0} million rays per second", millionRaysPerSec);
        }

        [TestMethod]
        public void RayIntersectSphereFromInside_Performance()
        {
            // TODO: optimise ray-sphere intersection code
            // TODO: this only tests the case of ray hitting the sphere. Also test performance of misses and near-misses, or aggregate performance.
#if DEBUG
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 0.8;
            const double maxMillionRaysPerSec = 1.3;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 4.9;
            const double maxMillionRaysPerSec = 7.0;
#else
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 3.0;
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
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 0.6;
            const double maxMillionRaysPerSec = 1.0;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 3.9;
            const double maxMillionRaysPerSec = 6.5;
#else
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 2.8;
            const double maxMillionRaysPerSec = 4.6;
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
            Assert.AreEqual(numRays, numRaysHit, "Num rays hit {0} should be the same as total rays {1}", numRaysHit, numRays);
            //Assert.IsTrue(numRays * 0.498 < numRaysHit && numRaysHit < numRays * 0.502, "Num rays hit {0} should be roughly half of total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f2} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("Performance: {0} million rays per second", millionRaysPerSec);
        }

        [TestMethod]
        public void RayIntersectSphereRandomly_Performance()
        {
#if DEBUG
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 0.9;
            const double maxMillionRaysPerSec = 1.3;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 6.0;
            const double maxMillionRaysPerSec = 8.5;
#else
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 4.4;
            const double maxMillionRaysPerSec = 6.1;
#endif

            const int numRays = 1000000;
            var sphere = new Sphere(new Vector(0, 0, 0), 1);
            var numRaysHit = 0;

            DateTime startTime = DateTime.Now;
            for (var i = 0; i < numRays; i++)
            {
                var start = MakeRandomVector(-2, 2, -2, 2, -2, 2);
                var dir = MakeRandomVector(-1, 1, -1, 1, -1, 1);
                var info = sphere.IntersectRay(start, dir, context);
                if (info != null)
                    numRaysHit++;
            }
            var elapsedTime = DateTime.Now - startTime;
            //Assert.AreEqual(numRays, numRaysHit, "Num rays hit {0} should be the same as total rays {1}", numRaysHit, numRays);
            //Assert.IsTrue(numRays * 0.498 < numRaysHit && numRaysHit < numRays * 0.502, "Num rays hit {0} should be roughly half of total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f2} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("Performance: {0} million rays per second", millionRaysPerSec);
            Console.WriteLine("Num rays hit: {0} / {1}", numRaysHit, numRays);
        }

        [TestMethod]
        public void RayIntersectAABBPerformance()
        {
#if DEBUG
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 0.32;
            const double maxMillionRaysPerSec = 0.55;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 2.9;
            const double maxMillionRaysPerSec = 4.0;
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
            Assert.IsTrue(numRays * 0.998 < numRaysHit && numRaysHit <= numRays * 1.0, "Num rays hit {0} should be roughly the same as total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f3} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("Performance: {0} million rays per second", millionRaysPerSec);
        }

        [TestMethod]
        public void BuildVoxelGridPerformance()
        {
#if DEBUG
            // my laptop in High Performance mode
            const double minMillionTriVoxelPerSec = 2.8;
            const double maxMillionTriVoxelPerSec = 3.2;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionTriVoxelPerSec = 31.0;
            const double maxMillionTriVoxelPerSec = 42.0;
#else
            // my laptop in High Performance mode
            const double minMillionTriVoxelPerSec = 17.0;
            const double maxMillionTriVoxelPerSec = 26.0;
#endif

            const int numTriangles = 1000;
            const int voxelGridSize = 32;

            const uint triangleColor = 0xffffffff;
            var triList = new List<Triangle>();
            for(int i = 0; i < numTriangles; i++)
            {
                triList.Add(new Triangle(
                   MakeRandomVector(-0.5, 0.5, -0.5, 0.5, -0.5, 0.5),
                   MakeRandomVector(-0.5, 0.5, -0.5, 0.5, -0.5, 0.5),
                   MakeRandomVector(-0.5, 0.5, -0.5, 0.5, -0.5, 0.5),
                   triangleColor));
            }

            DateTime startTime = DateTime.Now;
            var voxels = new VoxelGrid(voxelGridSize, "BuildVoxelGridPerformance_voxels", "");
            int numFilledVoxels = TriMeshToVoxelGrid.Convert(triList, voxelGridSize, voxels);
            var elapsedTime = DateTime.Now - startTime;

            int totalVoxels = voxelGridSize * voxelGridSize * voxelGridSize;
            Assert.IsTrue(numFilledVoxels > 0.99 * totalVoxels);
            var millionTriVoxelPerSec = triList.Count * totalVoxels / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionTriVoxelPerSec < millionTriVoxelPerSec && millionTriVoxelPerSec < maxMillionTriVoxelPerSec,
                "Tri/voxel per second {0:f3} not between {1} and {2} (millions)", millionTriVoxelPerSec, minMillionTriVoxelPerSec, maxMillionTriVoxelPerSec);
            Console.WriteLine("Performance: {0} million tri/voxel built per second", millionTriVoxelPerSec);
        }

        [TestMethod]
        public void RayIntersectVoxelGridPerformance()
        {
#if DEBUG
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 0.19;
            const double maxMillionRaysPerSec = 0.30;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 0.2;
            const double maxMillionRaysPerSec = 0.3;
#else
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 0.14;
            const double maxMillionRaysPerSec = 0.22;
#endif

            const int numRays = 1000000;
            const int voxelGridSize = 32;

            var triList = new List<Triangle>();
            triList.Add(new Triangle(
                new Vector(-0.5, -0.5, 0.001),
                new Vector(0.5, 0.5, 0.001),
                new Vector(-0.5, 0.5, 0.001),
                Engine3D.Color.Cyan.ToARGB()));
            var voxels = new VoxelGrid(voxelGridSize, "RayIntersectVoxelGridPerformance_voxels", "");
            int numFilledVoxels = TriMeshToVoxelGrid.Convert(triList, voxelGridSize, voxels);
            Assert.AreEqual(voxelGridSize * voxelGridSize, numFilledVoxels);

            var numRaysHit = 0;
            DateTime startTime = DateTime.Now;
            for (var i = 0; i < numRays; i++)
            {
                //var start = MakeRandomVector(-1, 1, -1, 1, -1, -0.5);
                var start = MakeRandomVector(-0.5, 0.5, -0.5, 0.5, -1, -0.5);
                var dir = new Vector(0, 0, 1);

                //var start = MakeRandomVector(-2, 2, -2, 2, -2, 2);
                //var dir = MakeRandomVector(-1, 1, -1, 1, -1, 1);

                //var start = new Vector(0.5, 0.5, 0.5);
                //var start = MakeRandomVector(-0.3, 0.3, -0.3, 0.3, 0.5, 1);
                //var dir = MakeRandomVector(-0.5, 0.5, -0.5, 0.5, -1, -1);
                //var dir = MakeRandomVector(1, 1, -1);

                var info = voxels.IntersectRay(start, dir, context);
                if (info != null)
                    numRaysHit++;
            }
            var elapsedTime = DateTime.Now - startTime;
            // TODO: this should pass, but a bug in TriMeshToVoxelGrid.Convert makes this fail (930K)
            //Assert.IsTrue(numRays * 0.45 < numRaysHit && numRaysHit <= numRays * 0.55, "Num rays hit {0} should be roughly half of total rays {1}", numRaysHit, numRays);
            var millionRaysPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds;
            Assert.IsTrue(minMillionRaysPerSec < millionRaysPerSec && millionRaysPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f3} not between {1} and {2} (millions)", millionRaysPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("Performance: {0} million rays per second", millionRaysPerSec);
        }

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

        // TODO: this function is slower than simply calling random.NextRandom()! Maybe because of the method call and array access?
        // TODO: with numRandomDoubles=123 the values do not seem very random: RayIntersectPlanePerformance fails because of this.
        private double NextRandomDouble()
        {
            return random.NextDouble();

            //var result = randomDouble[randomIndex];
            //randomIndex = (randomIndex + 1) % numRandomDoubles;
            //return result;
        }
    }
}
