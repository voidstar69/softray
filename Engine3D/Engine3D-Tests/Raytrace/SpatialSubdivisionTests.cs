using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Engine3D.Raytrace;
using Vector = Engine3D.Vector;

// TODO: intersect rays against subdivision trees, and verify ray-node-triangle stats make sense

namespace Engine3D_Tests
{
    [TestClass]
    public class SpatialSubdivisionTests
    {
        private static Random random = new Random();

        public SpatialSubdivisionTests()
        {
            // Prevent Assert failure from displaying UI
            Debug.Listeners.Clear();
        }

        [TestMethod, ExpectedException(typeof(NullReferenceException))]
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
            Assert.AreEqual(11, tree.NumNodes);
            Assert.AreEqual(6, tree.NumLeafNodes);
            Assert.AreEqual(5, tree.NumInternalNodes);
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
            Assert.AreEqual(3, tree.TreeDepth);
            Assert.AreEqual(5, tree.NumNodes);
            Assert.AreEqual(3, tree.NumLeafNodes);
            Assert.AreEqual(2, tree.NumInternalNodes);
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
            Assert.AreEqual(7, tree.NumNodes);
            Assert.AreEqual(4, tree.NumLeafNodes);
            Assert.AreEqual(3, tree.NumInternalNodes);
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
            Assert.AreEqual(3, tree.TreeDepth);
            Assert.AreEqual(5, tree.NumNodes);
            Assert.AreEqual(3, tree.NumLeafNodes);
            Assert.AreEqual(2, tree.NumInternalNodes);
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
            Assert.AreEqual(915, tree.NumNodes);
            Assert.AreEqual(458, tree.NumLeafNodes);
            Assert.AreEqual(457, tree.NumInternalNodes);
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
                triangles.Add(new Triangle(v1, v2, v3, color));
            }
            return triangles;
        }

        private AxisAlignedBox GetBoundingBoxOfRandomTriangles()
        {
            // TODO: ensure this remains in sync with MakeRandomVector
            return new AxisAlignedBox(new Vector(0, 0, 0), new Vector(triangleSpaceSize, triangleSpaceSize, triangleSpaceSize));
        }

        private Vector MakeRandomVector(double size)
        {
            return new Vector(random.NextDouble() * size, random.NextDouble() * size, random.NextDouble() * size);
        }

/*
        public void PexTest(int randomSeed = 12345)
        {
            const int maxTreeDepth = 5;
            const int maxGeometryPerNode = 3;
            random = new Random(randomSeed);
            var triangles = MakeRandomTriangles(10);
            var boundingBox = GetBoundingBoxOfRandomTriangles();
            var tree = new SpatialSubdivision(triangles, boundingBox, maxTreeDepth, maxGeometryPerNode);
            Assert.IsNotNull(tree);
            Assert.AreEqual(4, tree.TreeDepth);
            Assert.AreEqual(11, tree.NumNodes);
            Assert.AreEqual(6, tree.NumLeafNodes);
            Assert.AreEqual(5, tree.NumInternalNodes);
        }
*/
    }
}