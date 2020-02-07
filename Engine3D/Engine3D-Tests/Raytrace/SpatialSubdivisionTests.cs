#define APPVEYOR_PERFORMANCE_MARGINS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Engine3D.Raytrace;
using Vector = Engine3D.Vector;

namespace Engine3D_Tests
{
    [TestClass]
    public class SpatialSubdivisionTests
    {
        private static Random random = new Random();
        private static RenderContext context = new RenderContext(random);

        public SpatialSubdivisionTests()
        {
            // Prevent Assert failure from displaying UI
            Debug.Listeners.Clear();
        }

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void NullGeometryToConstructor_Error()
        {
            new SpatialSubdivision(null, new AxisAlignedBox(new Vector(0, 0, 0), new Vector(1, 1, 1)));
        }

        [TestMethod]
        public void NullBoundingBoxToConstructor_NoError()
        {
            var obj = new SpatialSubdivision(new List<Triangle>(), null);
            Assert.IsNotNull(obj);
        }

        [TestMethod]
        public void EmptyInputToConstructor_NoError()
        {
            var obj = new SpatialSubdivision(new List<Triangle>(), new AxisAlignedBox(new Vector(), new Vector()));
            Assert.IsNotNull(obj);
        }

        [TestMethod, ExpectedException(typeof(NullReferenceException))]
        public void CollectionsOfNullToConstructor_Error()
        {
            var obj = new SpatialSubdivision(new Triangle[] { null, null, null }, new AxisAlignedBox(new Vector(0, 0, 0), new Vector(1, 1, 1)));
            Assert.IsNotNull(obj);
        }

        [TestMethod]
        public void DegenerateTrianglesToConstructor_NoError()
        {
            var tri = new Triangle(new Vector(), new Vector(), new Vector(), 0);
            var obj = new SpatialSubdivision(new Triangle[] { tri }, new AxisAlignedBox(new Vector(0, 0, 0), new Vector(1, 1, 1)));
            Assert.IsNotNull(obj);
        }

        [TestMethod]
        public void ConstructArbitraryTree()
        {
            const int maxTreeDepth = 5;
            const int maxGeometryPerNode = 3;
            random = new Random(12345);
            var triangles = MakeRandomTriangles(10);
            var boundingBox = GetBoundingBoxOfRandomTriangles();
            var tree = new SpatialSubdivision(triangles, boundingBox, maxTreeDepth, maxGeometryPerNode);
            Assert.IsNotNull(tree);
            Assert.AreEqual(4, tree.TreeDepth);
            Assert.AreEqual(9, tree.NumNodes);
            Assert.AreEqual(5, tree.NumLeafNodes);
            Assert.AreEqual(4, tree.NumInternalNodes);
        }

        [TestMethod]
        public void ConstructMaxDepthTree()
        {
            const int maxTreeDepth = 3;
            const int maxGeometryPerNode = 1;
            random = new Random(12345);
            var triangles = MakeRandomTriangles(5);
            var boundingBox = GetBoundingBoxOfRandomTriangles();
            var tree = new SpatialSubdivision(triangles, boundingBox, maxTreeDepth, maxGeometryPerNode);
            Assert.IsNotNull(tree);
            Assert.AreEqual(2, tree.TreeDepth);
            Assert.AreEqual(3, tree.NumNodes);
            Assert.AreEqual(2, tree.NumLeafNodes);
            Assert.AreEqual(1, tree.NumInternalNodes);
        }

        [TestMethod]
        public void ConstructBalancedTree()
        {
            const int maxTreeDepth = 3;
            const int maxGeometryPerNode = 1;
            random = new Random(12345);
            var triangles = MakeRandomTriangles(8);
            var boundingBox = GetBoundingBoxOfRandomTriangles();
            var tree = new SpatialSubdivision(triangles, boundingBox, maxTreeDepth, maxGeometryPerNode);
            Assert.IsNotNull(tree);
            Assert.AreEqual(3, tree.TreeDepth);
            Assert.AreEqual(5, tree.NumNodes);
            Assert.AreEqual(3, tree.NumLeafNodes);
            Assert.AreEqual(2, tree.NumInternalNodes);
        }

        [TestMethod]
        public void ConstructUnbalancedTree()
        {
            const int maxTreeDepth = 100;
            const int maxGeometryPerNode = 1;
            random = new Random(12345);
            var triangles = MakeRandomTriangles(4);
            var boundingBox = GetBoundingBoxOfRandomTriangles();
            var tree = new SpatialSubdivision(triangles, boundingBox, maxTreeDepth, maxGeometryPerNode);
            Assert.IsNotNull(tree);
            Assert.AreEqual(2, tree.TreeDepth);
            Assert.AreEqual(3, tree.NumNodes);
            Assert.AreEqual(2, tree.NumLeafNodes);
            Assert.AreEqual(1, tree.NumInternalNodes);
        }

        [TestMethod]
        public void ConstructBigTree()
        {
            const int maxTreeDepth = 10;
            const int maxGeometryPerNode = 5;
            random = new Random(12345);
            var triangles = MakeRandomTriangles(1000);
            var boundingBox = GetBoundingBoxOfRandomTriangles();
            var tree = new SpatialSubdivision(triangles, boundingBox, maxTreeDepth, maxGeometryPerNode);
            Assert.IsNotNull(tree);
            Assert.AreEqual(10, tree.TreeDepth);
            Assert.AreEqual(885, tree.NumNodes);
            Assert.AreEqual(443, tree.NumLeafNodes);
            Assert.AreEqual(442, tree.NumInternalNodes);
        }

        private ICollection<Triangle> BuildRandomTree(int numTriangles, int maxTreeDepth, int maxGeometryPerNode, int randomSeed, out SpatialSubdivision tree)
        {
            random = new Random(randomSeed);
            var triangles = MakeRandomTriangles(numTriangles);
            var boundingBox = GetBoundingBoxOfRandomTriangles();
            tree = new SpatialSubdivision(triangles, boundingBox, maxTreeDepth, maxGeometryPerNode);
            Assert.IsNotNull(tree);
            return triangles;
        }

        [TestMethod]
        public void RayIntersectTreeFromInside_Performance()
        {
#if DEBUG
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 10.0;
            const double maxMillionRaysPerSec = 13.0;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 57.0;
            const double maxMillionRaysPerSec = 59.0;
#else
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 26.0;
            const double maxMillionRaysPerSec = 37.0;
#endif

            const int numTriangles = 1000;
            SpatialSubdivision tree;
            BuildRandomTree(numTriangles, maxTreeDepth: 10, maxGeometryPerNode: 5, randomSeed: 12345, tree: out tree);
            Assert.AreEqual(10, tree.TreeDepth);
            Assert.AreEqual(885, tree.NumNodes);
            Assert.AreEqual(443, tree.NumLeafNodes);
            Assert.AreEqual(442, tree.NumInternalNodes);

            const int numRays = 10000;
            var numRaysHit = 0;
            DateTime startTime = DateTime.Now;
            for (var i = 0; i < numRays; i++)
            {
                var start = MakeRandomVector(triangleSpaceSize);
                var dir = MakeRandomVector(-1, 1, -1, 1, -1, 1);
                var info = tree.IntersectRay(start, dir, context);
                if (info != null)
                    numRaysHit++;
            }
            var elapsedTime = DateTime.Now - startTime;
            Assert.IsTrue(numRays * 0.2 < numRaysHit && numRaysHit < numRays * 0.3, "Num rays hit {0} should be 20-30% of total rays {1}", numRaysHit, numRays);
            var millionRayTriPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds * numTriangles;
            Assert.IsTrue(minMillionRaysPerSec < millionRayTriPerSec && millionRayTriPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f2} not between {1} and {2} (millions)", millionRayTriPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("Performance: {0} million ray/tri per second", millionRayTriPerSec);
        }

        [TestMethod]
        public void RayIntersectTreeMostlyFromOutside_Performance()
        {
#if DEBUG
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 2;
            const double maxMillionRaysPerSec = 3;
#elif APPVEYOR_PERFORMANCE_MARGINS
            // AppVeyor build server
            const double minMillionRaysPerSec = 23.0;
            const double maxMillionRaysPerSec = 26.0;
#else
            // my laptop in High Performance mode
            const double minMillionRaysPerSec = 10.0;
            const double maxMillionRaysPerSec = 12.0;
#endif

            const int numTriangles = 1000;
            SpatialSubdivision tree;
            BuildRandomTree(numTriangles, maxTreeDepth: 10, maxGeometryPerNode: 5, randomSeed: 12345, tree: out tree);
            Assert.AreEqual(10, tree.TreeDepth);
            Assert.AreEqual(885, tree.NumNodes);
            Assert.AreEqual(443, tree.NumLeafNodes);
            Assert.AreEqual(442, tree.NumInternalNodes);

            const int numRays = 10000;
            var numRaysHit = 0;
            DateTime startTime = DateTime.Now;
            for (var i = 0; i < numRays; i++)
            {
                var start = MakeRandomVector(triangleSpaceSize * 10);
                var end = MakeRandomVector(triangleSpaceSize);
                var dir = end - start;
                var info = tree.IntersectRay(start, dir, context);
                if (info != null)
                    numRaysHit++;
            }
            var elapsedTime = DateTime.Now - startTime;
            //Assert.IsTrue(numRays * 0.2 < numRaysHit && numRaysHit < numRays * 0.3, "Num rays hit {0} should be 20-30% of total rays {1}", numRaysHit, numRays);
            var millionRayTriPerSec = numRays / 1000000.0 / elapsedTime.TotalSeconds * numTriangles;
            Assert.IsTrue(minMillionRaysPerSec < millionRayTriPerSec && millionRayTriPerSec < maxMillionRaysPerSec,
                "Rays per second {0:f2} not between {1} and {2} (millions)", millionRayTriPerSec, minMillionRaysPerSec, maxMillionRaysPerSec);
            Console.WriteLine("Performance: {0} million ray/tri per second", millionRayTriPerSec);
        }

        [TestMethod]
        public void TreeCorrectness1()
        {
            SpatialSubdivision tree;
            var triList = BuildRandomTree(100, maxTreeDepth: 10, maxGeometryPerNode: 5, randomSeed: 12345, tree: out tree);
            TestTree_InsideOut(tree, triList);
        }

        [TestMethod]
        public void TreeCorrectness2()
        {
            SpatialSubdivision tree;
            var triList = BuildRandomTree(20, maxTreeDepth: 10, maxGeometryPerNode: 1, randomSeed: 123456, tree: out tree);
            TestTree_InsideOut(tree, triList);
        }

        [TestMethod]
        public void TreeCorrectness_OutsideIn()
        {
            SpatialSubdivision tree;
            var triList = BuildRandomTree(20, maxTreeDepth: 10, maxGeometryPerNode: 1, randomSeed: 123456, tree: out tree);
            TestTree_OutsideIn(tree, triList);
        }

        [TestMethod]
        public void TreeCorrectness_RandomInput()
        {
            random = new Random();
            var numTriangles = random.Next(10000);
            var maxTreeDepth = random.Next(50) + 10;
            var maxGeometryPerNode = random.Next(10) + 5;
            var randomSeed = random.Next();

            Console.WriteLine("Setup: {0} triangles, {1} tree depth, {2} max geometry, {3} random seed",
                numTriangles, maxTreeDepth, maxGeometryPerNode, randomSeed);

            SpatialSubdivision tree;
            var triList = BuildRandomTree(numTriangles, maxTreeDepth, maxGeometryPerNode, randomSeed, out tree);
            TestTree_InsideOut(tree, triList, 10000);
        }

        [TestMethod]
        public void TreeCorrectness_EnsureIntersectionCheckedAgainstTreeNodeBoundingBox()
        {
            SpatialSubdivision tree;
            var triList = BuildRandomTree(100, maxTreeDepth: 10, maxGeometryPerNode: 5, randomSeed: 1234567, tree: out tree);
            TestTree_InsideOut(tree, triList, 265896);
        }

        [TestMethod]
        public void TreeCorrectness_EnsureIntersectionCheckedAgainstTreeNodeBoundingBox2()
        {
            SpatialSubdivision tree;
            var triList = BuildRandomTree(10000, maxTreeDepth: 10, maxGeometryPerNode: 5, randomSeed: 1234567, tree: out tree);
            TestTree_InsideOut(tree, triList, 34);
        }

        private void TestTree_InsideOut(SpatialSubdivision tree, ICollection<Triangle> triList, int numRays = 100000)
        {
            var triSet = new GeometryCollection();
            foreach(var tri in triList)
            {
                triSet.Add(tri);
            }

            for (var i = 1; i <= numRays; i++)
            {
                var start = MakeRandomVector(triangleSpaceSize);
                var dir = MakeRandomVector(-1, 1, -1, 1, -1, 1);
                var info = tree.IntersectRay(start, dir, context);
                var infoBase = triSet.IntersectRay(start, dir, context);
                Assert.AreEqual(infoBase == null, info == null, "Ray " + i + ": SpatialSubdivision and GeometryCollection differ in intersection status");
                if (info != null)
                {
                    Assert.AreEqual(infoBase.triIndex, info.triIndex, "triIndex diff on ray " + i);
                    Assert.AreEqual(infoBase.rayFrac, info.rayFrac, "rayFrac diff on ray " + i);
                    Assert.AreEqual(infoBase.pos, info.pos, "pos diff on ray " + i);
                    Assert.AreEqual(infoBase.normal, info.normal, "normal diff on ray " + i);
                    Assert.AreEqual(infoBase.color, info.color, "color diff on ray " + i);
                }
            }
        }

        private void TestTree_OutsideIn(SpatialSubdivision tree, ICollection<Triangle> triList, int numRays = 100000)
        {
            var triSet = new GeometryCollection();
            foreach(var tri in triList)
            {
                triSet.Add(tri);
            }

            for (var i = 1; i <= numRays; i++)
            {
                var start = MakeRandomVector(triangleSpaceSize * 10);
                var end = MakeRandomVector(triangleSpaceSize);
                var dir = end - start;
                var info = tree.IntersectRay(start, dir, context);
                var infoBase = triSet.IntersectRay(start, dir, context);
                Assert.AreEqual(infoBase == null, info == null, "Ray " + i + ": SpatialSubdivision and GeometryCollection differ in intersection status");
                if (info != null)
                {
                    Assert.AreEqual(infoBase.triIndex, info.triIndex, "triIndex diff on ray " + i);
                    Assert.AreEqual(infoBase.rayFrac, info.rayFrac, 1e-10, "rayFrac diff on ray " + i);
                    Assert.AreEqual(infoBase.pos, info.pos, "pos diff on ray " + i);
                    Assert.AreEqual(infoBase.normal, info.normal, "normal diff on ray " + i);
                    Assert.AreEqual(infoBase.color, info.color, "color diff on ray " + i);
                }
            }
        }

        private const int triangleSpaceSize = 100;
        private const int triangleExtentSize = 10;

        private ICollection<Triangle> MakeRandomTriangles(int numTriangles)
        {
            var triangles = new List<Triangle>();
            for(int i = 0; i < numTriangles; i++)
            {
                var v1 = MakeRandomVector(triangleSpaceSize);
                var v2 = v1 + MakeRandomVector(triangleExtentSize);
                var v3 = v1 + MakeRandomVector(triangleExtentSize);
                var color = (uint)random.Next();
                var tri = new Triangle(v1, v2, v3, color);
                tri.TriangleIndex = i;
                triangles.Add(tri);
            }
            return triangles.AsReadOnly();
        }

        private AxisAlignedBox GetBoundingBoxOfRandomTriangles()
        {
            // TODO: ensure this remains in sync with MakeRandomVector
            return new AxisAlignedBox(new Vector(0, 0, 0),
                new Vector(triangleSpaceSize + triangleExtentSize, triangleSpaceSize + triangleExtentSize, triangleSpaceSize + triangleExtentSize));
        }

        private Vector MakeRandomVector(double size)
        {
            return new Vector(random.NextDouble() * size, random.NextDouble() * size, random.NextDouble() * size);
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