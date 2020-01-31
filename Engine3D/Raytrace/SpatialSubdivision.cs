//#define VISUALISE_TREE      // visualise the tree nodes as colored blocks
#define SPLIT_LONGEST_AXIS
//#define ADAPTIVE_SPLIT    // split node at centroid of contained triangles
//#define COLOR_NODES       // color triangles based on their containing node
//#define LOG_SPLITS

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    /// <summary>
    /// A binary tree of axis-aligned splitting planes, with geometry stored only at leaf nodes.
    /// Effectively an Octree, although the data structure is a Binary Tree.
    /// </summary>
    public class SpatialSubdivision : IRayIntersectable
    {
        #region SpatialSubdivision.Node class

        private class Node
        {
            public readonly ICollection<Triangle> geometry = new List<Triangle>();
            public readonly AxisAlignedBox boundingBox;

            public Plane splittingPlane;
            public Node normalSide;
            public Node backSide;

            public uint leafColor;

            // TODO: store per-tree stats in SpatialSubdivision object instead of global stats here
            public static int totalTreeDepth;
            public static int totalNodes;
            public static int leafNodes;

            private static Random random = new Random();

            public Node(ICollection<Triangle> geometry, AxisAlignedBox boundingBox)
            {
                Assert.IsTrue(geometry != null, "Node geometry is null");
                Assert.IsTrue(boundingBox != null, "Node bounding box is null");

                this.geometry = geometry;
                this.boundingBox = boundingBox;
                totalNodes++;
            }

            public void RecursivePlaneSplit(int treeDepth, int maxTreeDepth, int maxGeometryPerNode, int parentSplitAxis)
            {
                Contract.Requires(treeDepth > 0);
                Contract.Requires(maxTreeDepth > 0);
                Contract.Requires(maxGeometryPerNode > 0);
                Contract.Requires(0 <= parentSplitAxis && parentSplitAxis <= 2);
                Contract.Requires(splittingPlane == null, "splitting-plane is null");
                Assert.IsTrue(normalSide == null, "normal-side is null");
                Assert.IsTrue(backSide == null, "back-side is null");
                Assert.IsTrue(geometry != null, "geometry collection is null");
                Assert.IsTrue(geometry.Count > 0, "geometry collection is empty");

                leafColor = (uint)random.Next();
                totalTreeDepth = Math.Max(totalTreeDepth, treeDepth);


                if (treeDepth >= maxTreeDepth               // Have we made the tree deep enough?
                 || geometry.Count <= maxGeometryPerNode)   // Stop splitting nodes when current node contains little enough geometry.
                {
                    leafNodes++;
                    ProcessLeafNode();
                    return;
                }

                // Pick an axis-aligned plane to split the current sector.
                // TODO: balance tree better by choosing splitting plane direction based on
                // longest bounding box length, or most even split of geometry?
                int axis;
#if SPLIT_LONGEST_AXIS
                Vector boxExtent = boundingBox.Max - boundingBox.Min;
                boxExtent = new Vector(Math.Abs(boxExtent.x), Math.Abs(boxExtent.y), Math.Abs(boxExtent.z));
                if (boxExtent.x > boxExtent.y)
                {
                    if (boxExtent.x > boxExtent.z)
                    {
                        axis = 0; // X-axis
                    }
                    else
                    {
                        axis = 2; // Z-axis
                    }
                }
                else
                {
                    if (boxExtent.y > boxExtent.z)
                    {
                        axis = 1; // Y-axis
                    }
                    else
                    {
                        axis = 2; // Z-axis
                    }
                }
#else
                // Split on the next axis after the parent's split axis (i.e. X -> Y -> Z -> X ...)
                axis = (parentSplitAxis + 1) % 3;
#endif
                
                Vector splitPt = boundingBox.Centre;
#if ADAPTIVE_SPLIT
                // Compute centroid of all triangle vertices in this node.
                Vector centroid = new Vector(0, 0, 0);
                int vertexCount = 0;
                foreach (Triangle tri in geometry)
                {
                    //if (boundingBox.ContainsPoint(tri.Vertex1))
                    //{
                    //    centroid += tri.Vertex1;
                    //    vertexCount++;
                    //}
                    //if (boundingBox.ContainsPoint(tri.Vertex2))
                    //{
                    //    centroid += tri.Vertex2;
                    //    vertexCount++;
                    //}
                    //if (boundingBox.ContainsPoint(tri.Vertex3))
                    //{
                    //    centroid += tri.Vertex3;
                    //    vertexCount++;
                    //}
                    centroid += tri.Vertex1;
                    centroid += tri.Vertex2;
                    centroid += tri.Vertex3;
                    vertexCount += 3;
                }
                splitPt = centroid / vertexCount;
#endif

                // Create splitting plane.
                switch (axis)
                {
                    case 0: splittingPlane = new Plane(splitPt, new Vector(1, 0, 0)); break; // X-axis
                    case 1: splittingPlane = new Plane(splitPt, new Vector(0, 1, 0)); break; // Y-axis
                    case 2: splittingPlane = new Plane(splitPt, new Vector(0, 0, 1)); break; // Z-axis
                    default: Assert.Fail("Unexpected axis index"); break;
                }

                // Copy all geometry from current node to the appropriate child nodes.
                ICollection<Triangle> normalSideGeom = new List<Triangle>();
                ICollection<Triangle> backSideGeom = new List<Triangle>();
                foreach (Triangle geom in geometry)
                {
                    PlaneHalfSpace planeHalfSpace = geom.IntersectPlane(splittingPlane);
                    Assert.IsTrue((planeHalfSpace & ~(PlaneHalfSpace.NormalSide | PlaneHalfSpace.BackSide)) == 0,
                        "Unexpected PlaneHalfSpace enumeration bit value");

                    if ((planeHalfSpace & PlaneHalfSpace.NormalSide) != 0)
                    {
                        normalSideGeom.Add(geom);
                    }
                    if ((planeHalfSpace & PlaneHalfSpace.BackSide) != 0)
                    {
                        backSideGeom.Add(geom);
                    }
                }

                // If either child node ends up with no geometry or all geometry, keep the geometry in the current node.
                // TODO: if we cannot split along this axis, attempt to split along the other two axes?
                bool rejectSplit = (normalSideGeom.Count == geometry.Count || backSideGeom.Count == geometry.Count);

#if LOG_SPLITS
                Logger.Log("Tree node: axis {0}, depth: {1}, {2} tri => {3},{4} {5}",
                    axis, treeDepth, geometry.Count, normalSideGeom.Count, backSideGeom.Count, rejectSplit ? "(rejected split)" : "");
#endif

                if(rejectSplit)
                {
                    // Throw away the child nodes, i.e. reverse the splitting of the current node.
                    splittingPlane = null;
                    leafNodes++;
                    ProcessLeafNode();
                    return;
                }

                // Delete all geometry from current node, so that geometry only exists in child nodes.
                geometry.Clear();

                // Create bounding boxes for child nodes.
                Vector backSideBoxMax = boundingBox.Max;
                Vector normalSideBoxMin = boundingBox.Min;
                switch (axis)
                {
                    case 0: backSideBoxMax.x = normalSideBoxMin.x = splitPt.x; break; // X-axis
                    case 1: backSideBoxMax.y = normalSideBoxMin.y = splitPt.y; break; // Y-axis
                    case 2: backSideBoxMax.z = normalSideBoxMin.z = splitPt.z; break; // Z-axis
                    default: Assert.Fail("Unexpected axis index"); break;
                }
                AxisAlignedBox backSideBox = new AxisAlignedBox(boundingBox.Min, backSideBoxMax);
                AxisAlignedBox normalSideBox = new AxisAlignedBox(normalSideBoxMin, boundingBox.Max);

                // Create child nodes and recursively split them.
                //axis = (axis + 1) % 3;
                treeDepth++;
                if (normalSideGeom.Count > 0)
                {
                    normalSide = new Node(normalSideGeom, normalSideBox);
                    normalSide.RecursivePlaneSplit(treeDepth, maxTreeDepth, maxGeometryPerNode, axis);
                }
                if (backSideGeom.Count > 0)
                {
                    backSide = new Node(backSideGeom, backSideBox);
                    backSide.RecursivePlaneSplit(treeDepth, maxTreeDepth, maxGeometryPerNode, axis);
                }

                // Is current node a leaf node?
                if (normalSide == null && backSide == null)
                {
                    // Yes, so increment the leaf node count.
                    // TODO: we never seem to reach this code!
                    leafNodes++;
                    splittingPlane = null;
                    // TODO: Contracts analyser complains that this assert is always false, due to "initialisation of this.backSide" !?!
                    Contract.Assert(geometry.Count > 0, "Leaf node must contain some geometry");
                    Contract.Assert(splittingPlane == null, "Leaf node must not have a splitting plane");
                }
                else
                {
                    // No, current node is an internal node. Check that no internal node has any geometry.
                    Contract.Assert(geometry.Count == 0, "Internal node must not contain any geometry");
                    Contract.Assert(splittingPlane != null, "Internal node must have a splitting plane");
                }
            }

            //public bool RecursiveTriangleFind(int triIndex)
            //{
            //    if (geometry.Any(t => t.TriangleIndex == triIndex))
            //        return true;

            //    normalSide.RecursiveTriangleFind(triIndex);
            //    backSide.RecursiveTriangleFind(triIndex);

            //}

            /// <summary>
            /// Current node is a leaf node, so perform any processing required for leaf nodes.
            /// </summary>
            private void ProcessLeafNode()
            {
                // Give each triangle an opaque reference to the leaf node containing the triangle.
                // TODO: a triangle may be contained in multiple leaf nodes. Here each triangle is linked to only one leaf node, arbitrarily.
                foreach(var tri in geometry)
                {
                    tri.HandleToLeafNode = this;
                }
            }
        }

        #endregion

        #region Private data members

        private readonly Node root;
        private int numRayTests;
        //private int currRayId;

        #endregion

        #region Public methods and properties

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="geometry">Collection of geometry to subdivide. Collection will not be modified.</param>
        /// <param name="boundingBox">Bounding box of the geometry collection</param>
        /// <param name="maxTreeDepth">The maximum depth of the constructed tree. This can
        /// result in leaf nodes with more than <paramref name="maxGeometryPerNode"/></param>
        /// <param name="maxGeometryPerNode">Nodes are split during tree construction until we have no more than
        /// this amount of geometry per node. <paramref name="maxTreeDepth"/> will override this behaviour.</param>
        public SpatialSubdivision(ICollection<Triangle> geometry,
                                  AxisAlignedBox boundingBox,
                                  int maxTreeDepth = 15,
                                  int maxGeometryPerNode = 25)
        {
            Contract.Requires(boundingBox != null, "SpatialSubdivision bounding box is null");
            Contract.Requires(geometry != null, "SpatialSubdivision geometry is null");
            Contract.Requires(Contract.ForAll(geometry, tri => tri != null), "Some SpatialSubdivision triangles are null");
            Contract.Requires(maxTreeDepth > 0);
            Contract.Requires(maxGeometryPerNode > 0);

            if (geometry == null)
                throw new ArgumentNullException("geometry");

            // TODO: hacky, and not multi-thread safe. Instead pass stats from root node back to this class.
            Node.totalTreeDepth = 0;
            Node.totalNodes = 0;
            Node.leafNodes = 0;

            // all triangle vertices must lie within the bounding box
            foreach (var tri in geometry)
            {
                if (!boundingBox.ContainsPoint(tri.Vertex1) ||
                    !boundingBox.ContainsPoint(tri.Vertex2) ||
                    !boundingBox.ContainsPoint(tri.Vertex3))
                {
                    throw new ArgumentOutOfRangeException("geometry", "A triangle vertex is outside the bounding box");
                }
            }

            // create a modifiable clone of the geometry list, since Node will clear the list
            var geometryClone = new List<Triangle>(geometry);

            // Build a binary tree of axis-aligned splitting planes, with geometry stored only at leaf nodes.
            // As we descend through the tree, we cycle between planes aligned to each axis. Effectively this
            // creates an Octree, although the data structure is a Binary Tree.
            root = new Node(geometryClone, boundingBox);
            root.RecursivePlaneSplit(1, maxTreeDepth, maxGeometryPerNode, 2);

            // TODO: hacky, and not multi-thread safe. Pass stats from root node to this class?
            TreeDepth = Node.totalTreeDepth;
            NumNodes = Node.totalNodes;
            NumLeafNodes = Node.leafNodes;
            NumInternalNodes = NumNodes - NumLeafNodes;

            // TODO: stats accumulate across trees, so this logging is only correct for the first tree
            Logger.Log("Tree has {0} nodes, {1} leaf nodes, {2} internal nodes",
                       Node.totalNodes, Node.leafNodes, Node.totalNodes - Node.leafNodes);
        }

        /// <summary>
        /// The depth of this tree, counted in layers of nodes (root node counts as depth 1)
        /// </summary>
        public int TreeDepth { set; get; }

        /// <summary>
        /// The number of nodes in this tree.
        /// </summary>
        public int NumNodes { set; get; }

        /// <summary>
        /// The number of leaf nodes in this tree.
        /// </summary>
        public int NumLeafNodes { set; get; }

        /// <summary>
        /// The number of internal (non-leaf) nodes in this tree.
        /// </summary>
        public int NumInternalNodes { set; get; }

        /// <summary>
        /// A running total of the number of ray-geometry tests performed. This is never reset.
        /// </summary>
        public int RayGeometryTestCount { set;  get; }

        /// <summary>
        /// A running total of the number of clipped rays, i.e. outside the global bounding box. This is never reset.
        /// </summary>
        public int ClippedRayCount { set; get; }

        /// <summary>
        /// A running total of the number of rays traced through the binary tree. This is never reset.
        /// </summary>
        public int TracedRayCount { set; get; }

        /// <summary>
        /// The number of nodes visited in the tree during the last ray intersection test.
        /// </summary>
        public int NumNodesVisited { set; get; }

        /// <summary>
        /// The number of leaf nodes visited in the tree during the last ray intersection test.
        /// </summary>
        public int NumLeafNodesVisited { set; get; }

        /// <summary>
        /// The number of basic ray tests performed during the last call to IntersectRay.
        /// For simple objects this should always be 1.
        /// For complex objects this will be the number of sub-objects tested against the ray.
        /// </summary>
        public int NumRayTests
        {
            get
            {
                return numRayTests;
            }
        }

        /// <summary>
        /// Intersect a ray against this object.
        /// </summary>
        /// <param name="start">The start position of the ray, in object space.</param>
        /// <param name="dir">The direction of the ray, in object space (not a unit vector).</param>
        /// <returns>Information about the nearest intersection, or null if no intersection.</returns>
        public IntersectionInfo IntersectRay(Vector start, Vector dir, RenderContext context)
        {
            numRayTests = 0;
            NumNodesVisited = 0;
            NumLeafNodesVisited = 0;
            //currRayId++;

            // TODO: some rays should be short (e.g. ambient occlusion rays). Make the max ray distance an optional parameter?
            Vector end = start + dir * 1000;
            //Assert.IsTrue(false, "{0} {1}", start, end);
            // TODO: profiler says this (ClipLineSegment) uses 17% of overall time
            // Without this the Solid Octree mode causes the splitting planes to fill the screen!
            if (!root.boundingBox.OverlapsLineSegment(start, end))
            // TODO: clipping line segment breaks IntersectionInfo.rayFrac, because ray is truncated!
//            if (!root.boundingBox.ClipLineSegment(ref start, ref end))
            {
                //Assert.Fail("foo");
                ClippedRayCount++;
                return null;
            }
            //Assert.IsTrue(false, "{0} {1}", start, end);

            //dir.Normalise();
            //return new IntersectionInfo { color = 0x00ff0000, pos = start, normal = -dir };

            TracedRayCount++;
            // TODO: this optimisation may actually slow down the code! On the Couch model, in 10x res, it is definitely slower!
            //var testedTriangles = new HashSet<Triangle>();
            ISet<Triangle> testedTriangles = null;
            return RecursiveRayTrace(root, start, end, dir, testedTriangles, context);
        }

        /// <summary>
        /// Intersect a ray against the leaf node that contains a given triangle.
        /// </summary>
        /// <param name="start">The start position of the ray, in object space.</param>
        /// <param name="dir">The direction of the ray, in object space (not a unit vector).</param>
        /// <param name="tri"></param>
        /// <returns>Information about the nearest intersection, or null if no intersection.</returns>
        public IntersectionInfo IntersectRayWithLeafNode(Vector start, Vector dir, Triangle tri, RenderContext context)
        {
            Contract.Requires(tri != null);
            Contract.Requires(tri.HandleToLeafNode != null);
            Contract.Ensures(Contract.Result<IntersectionInfo>() == null || Contract.Result<IntersectionInfo>().normal.IsUnitVector);
            Contract.Ensures(Contract.Result<IntersectionInfo>() == null || Contract.Result<IntersectionInfo>().rayFrac >= 0);
            Contract.Ensures(Contract.Result<IntersectionInfo>() == null || Contract.Result<IntersectionInfo>().triIndex >= -1);

            numRayTests = 0;
            NumNodesVisited = 0;
            NumLeafNodesVisited = 0;

            // TODO: some rays should be short (e.g. ambient occlusion rays). Make the max ray distance an optional parameter?
            Vector end = start + dir * 1000;

            var node = (Node)tri.HandleToLeafNode; // retrieve (one of) the leaf node containing the given triangle
            Contract.Assert(node != null);

            // TODO: this optimisation may actually slow down the code! On the Couch model, in 10x res, it is definitely slower!
            //var testedTriangles = new HashSet<Triangle>();
            ISet<Triangle> testedTriangles = null;

            // TODO: avoid testing ray against given triangle? Triangle has likely already been tested.
            return RecursiveRayTrace(node, start, end, dir, testedTriangles, context);
        }

        #endregion

        #region Private methods

        private IntersectionInfo RecursiveRayTrace(Node node, Vector start, Vector end, Vector dir, ISet<Triangle> testedTriangles, RenderContext context)
        {
            // Does this node exist?
            if (node == null)
            {
                // No, so there is no geometry that the ray could intersect.
                return null;
            }

            // Clip the line segment against the bounding box of this node.
            // TODO: ray-box test may be too slow. Speed up?
            //if (!node.boundingBox.ClipLineSegment(ref start, ref end))
            //{
            //    return null;
            //}
            //IntersectionInfo intersection = node.boundingBox.IntersectLineSegment(start, end);
            //numRayTests += node.boundingBox.NumRayTests;
            //RayGeometryTestCount++;
            //if(intersection == null)
            //{
            //    return null;
            //}
            //start = intersection.pos;
            //intersection = node.boundingBox.IntersectLineSegment(end, start);
            //numRayTests += node.boundingBox.NumRayTests;
            //RayGeometryTestCount++;
            //if (intersection == null)
            //{
            //    return null;
            //}
            //end = intersection.pos;

            NumNodesVisited++;
            if (node.normalSide == null && node.backSide == null)
            {
                NumLeafNodesVisited++;
            }

            IntersectionInfo intersection;

#if VISUALISE_TREE

            if (node.geometry.Count > 0)
            {
                // Check that this is a leaf node.
                Assert.IsTrue(node.normalSide == null && node.backSide == null && node.splittingPlane == null, "Must be leaf node");

                RayGeometryTestCount++;

                uint color = node.leafColor;
                //uint color = (uint)(1 << (node.geometry.Count + 10));

                // Clip line segment to find intersection point with bounding box.
                // Ensure rayFrac is calculated correctly.
                Vector originalStart = start;
                bool lineInBox = root.boundingBox.ClipLineSegment(ref start, ref end);
                Assert.IsTrue(lineInBox, "Line segment is outside bounding box!");
                double rayFrac = start.Distance(originalStart);

                dir.Normalise();
                intersection = new IntersectionInfo { rayFrac = rayFrac, pos = start, normal = -dir, color = color };
                return intersection;
            }

#else
            // Trace ray against all geometry in this node.
            IntersectionInfo closestIntersection = new IntersectionInfo();
            closestIntersection.rayFrac = double.MaxValue;
            foreach (Triangle tri in node.geometry)
            {
                // Has the ray already been tested against this triangle?
                //if (!testedTriangles.Contains(tri))
                // TODO: this optimisation is probably not multithread safe
                //if (tri.LastRayId != currRayId)
                {
                    // No, so test the ray against this triangle.
                    //testedTriangles.Add(tri);
                    //bool triNoLongerNeeded = true;

                    intersection = tri.IntersectRay(start, dir, context);
                    if (intersection != null && intersection.rayFrac < closestIntersection.rayFrac)
                    {
                        Assert.IsTrue(intersection.rayFrac >= 0.0, "Ray fraction is negative");

                        // If intersection is outside of this node's bounding box
                        // then there may be a closer intersection in another node.
                        if (node.boundingBox.ContainsPoint(intersection.pos)) // TODO: this check might not be needed
                        {
                            closestIntersection = intersection;
                        }
                        //else
                        //{
                        //    triNoLongerNeeded = false;
                        //}
                    }
                    numRayTests += tri.NumRayTests;
                    RayGeometryTestCount++;

//                    if (triNoLongerNeeded)
                    {
                        // Update ray id.
                        // TODO: probably not multithread safe
                        //tri.LastRayId = currRayId;
                    }
                }
            }

            // Did ray intersect any geometry?
            if (closestIntersection.rayFrac < double.MaxValue)
            {
                //dir.Normalise();
                //return new IntersectionInfo { color = 0x00ff0000, pos = start, normal = -dir };

                // Yes, so return information about the closest intersection.
                return closestIntersection;
            }
#endif

            // Is this node a leaf node?
            if (node.normalSide == null && node.backSide == null)
            {
                // Yes, so this node does not have any child nodes. The ray did not intersect any geometry.
                Assert.IsTrue(node.splittingPlane == null, "Leaf node must not have a splitting plane");
                return null;
            }
            else
            {
                Assert.IsTrue(node.splittingPlane != null, "Internal node must have a splitting plane");
            }

/*
            // Intersect the line segment against the splitting plane to determine
            // the position where the line segment crosses the splitting plane.
            Vector middle;
            intersection = node.splittingPlane.IntersectLineSegment(start, end);
            if(intersection == null)
            {
                middle = end;
            }
            else
            {
                middle = intersection.pos;
            }
*/
            Vector middle;

            // Process the child sector that the ray starts within.
            //bool rayInPlaneNormalDir = (dir.DotProduct(node.splittingPlane.Normal) > 0);
            PlaneHalfSpace startHalfSpaces = new Point(start).IntersectPlane(node.splittingPlane);
            Assert.IsTrue(startHalfSpaces == PlaneHalfSpace.NormalSide || startHalfSpaces == PlaneHalfSpace.BackSide, "Start half space is invalid");
            PlaneHalfSpace endHalfSpaces = new Point(end).IntersectPlane(node.splittingPlane);
            Assert.IsTrue(endHalfSpaces == PlaneHalfSpace.NormalSide || endHalfSpaces == PlaneHalfSpace.BackSide, "End half space is invalid");
            switch (startHalfSpaces)
            {
                case PlaneHalfSpace.NormalSide:
                    middle = end;
                    intersection = RecursiveRayTrace(node.normalSide, start, middle, dir, testedTriangles, context);
                    if (intersection != null)
                    {
#if COLOR_NODES
                        intersection.color = 0x00ff0000;
#endif
                        return intersection;
                    }

                    // Process the other child sector too?
                    if(endHalfSpaces == PlaneHalfSpace.BackSide)
                    //if (!rayInPlaneNormalDir)
                    {
                        middle = start;
                        intersection = RecursiveRayTrace(node.backSide, middle, end, dir, testedTriangles, context);
#if COLOR_NODES
                        if (intersection != null)
                        {
                            intersection.color = 0x00ff00ff;
                        }
#endif
                        return intersection;
                    }
                    break;

                case PlaneHalfSpace.BackSide:
                    middle = end;
                    intersection = RecursiveRayTrace(node.backSide, start, middle, dir, testedTriangles, context);
                    if (intersection != null)
                    {
#if COLOR_NODES
                        intersection.color = 0x0000ff00;
#endif
                        return intersection;
                    }

                    // Process the other child sector too?
                    if (endHalfSpaces == PlaneHalfSpace.NormalSide)
                    //if (rayInPlaneNormalDir)
                    {
                        middle = start;
                        intersection = RecursiveRayTrace(node.normalSide, middle, end, dir, testedTriangles, context);
#if COLOR_NODES
                        if (intersection != null)
                        {
                            intersection.color = 0x0000ffff;
                        }
#endif
                        return intersection;
                    }
                    break;

                default:
                    Assert.Fail("Unexpected PlaneHalfSpace enumeration value");
                    break;
            }

            // The ray did not intersect any geometry.
            return null;
        }

        #endregion
    }
}