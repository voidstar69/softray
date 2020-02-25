using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    public class AxisAlignedBox : IRayIntersectable, ILineIntersectable
    {
        private const double epsilon = 1e-10;
        private readonly Vector min;
        private readonly Vector max;
        private readonly ICollection<Plane> planes = new List<Plane>();
        private int numRayTests;

        public AxisAlignedBox(Vector min, Vector max)
        {
            Contract.Requires(min.x < max.x, "Axis aligned bounding box has bad coordinates");
            Contract.Requires(min.y < max.y, "Axis aligned bounding box has bad coordinates");
            Contract.Requires(min.z < max.z, "Axis aligned bounding box has bad coordinates");
            this.min = min;
            this.max = max;
            planes.Add(new Plane(min, new Vector(-1,  0,  0)));
            planes.Add(new Plane(min, new Vector( 0, -1,  0)));
            planes.Add(new Plane(min, new Vector( 0,  0, -1)));
            planes.Add(new Plane(max, new Vector(+1,  0,  0)));
            planes.Add(new Plane(max, new Vector( 0, +1,  0)));
            planes.Add(new Plane(max, new Vector( 0,  0, +1)));
        }

        public Vector Min
        {
            get
            {
                return min;
            }
        }

        public Vector Max
        {
            get
            {
                return max;
            }
        }

        public Vector Centre
        {
            get
            {
                return (min + max) * 0.5;
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
            IntersectionInfo closest = new IntersectionInfo();
            closest.rayFrac = double.MaxValue;

            foreach (Plane plane in planes)
            {
                // Does ray intersect this plane?
                IntersectionInfo curr = plane.IntersectRay(start, dir, context);
                numRayTests += plane.NumRayTests;
                if (curr != null && curr.rayFrac < closest.rayFrac)
                {
                    // Does the intersection point lie on the surface of this box?
                    if(ContainsPoint(curr.pos))
                    {
                        // Yes, so the ray intersects this box.
                        closest = curr;
                    }
                }
            }

            if (closest.rayFrac == double.MaxValue)
            {
                return null;
            }
            else
            {
                return closest;
            }
        }

        /// <summary>
        /// The number of basic ray tests performed during the last call to IntersectRay.
        /// For simple objects this should always return 1.
        /// For complex objects this will return the number of sub-objects tested against the ray.
        /// </summary>
        public int NumRayTests
        {
            get
            {
                return numRayTests;
            }
        }

        /// <summary>
        /// Intersect a line segment this object.
        /// </summary>
        /// <param name="start">The start position of the line segment, in object space.</param>
        /// <param name="end">The end position of the line segment, in object space.</param>
        /// <returns>Information about the first intersection, or null if no intersection.</returns>
        public IntersectionInfo IntersectLineSegment(Vector start, Vector end)
        {
            numRayTests = 0;
            IntersectionInfo closest = new IntersectionInfo();
            closest.rayFrac = double.MaxValue;

            // TODO: somehow intersect line against all planes at once, or guess at nearest plane?
            foreach (Plane plane in planes)
            {
                // Does ray intersect this plane?
                IntersectionInfo curr = plane.IntersectLineSegment(start, end);
                numRayTests += plane.NumRayTests;
                if (curr != null && curr.rayFrac < closest.rayFrac)
                {
                    // Does the intersection point lie on the surface of this box?
                    if(ContainsPoint(curr.pos))
                    {
                        // Yes, so the ray intersects this box.
                        closest = curr;
                    }
                }
            }

            if (closest.rayFrac == double.MaxValue)
            {
                return null;
            }
            else
            {
                return closest;
            }
        }

        public bool ContainsPoint(Vector pos)
        {
            // Does this box contain the given point?
            return min.x - epsilon < pos.x && pos.x < max.x + epsilon &&
                   min.y - epsilon < pos.y && pos.y < max.y + epsilon &&
                   min.z - epsilon < pos.z && pos.z < max.z + epsilon;
        }

        /// <summary>
        /// Is any portion of the line segment within this box?
        /// </summary>
        /// <param name="start">The start position of the line segment, in object space.</param>
        /// <param name="end">The end position of the line segment, in object space.</param>
        /// <returns>False if line segment entirely outside of box;
        /// True if some portion of line segment is inside box</returns>
        public bool OverlapsLineSegment(Vector start, Vector end)
        {
            var tempStart = start;
            var tempEnd = end;
            // TODO: this does some unneccessary work, and could be made more efficient
            return ClipLineSegment(ref tempStart, ref tempEnd);
        }

        /// <summary>
        /// Clip a line segment against this axis-aligned box.
        /// Any portion of the line segment within this box is returned.
        /// Otherwise false is returned.
        /// </summary>
        /// <param name="start">The start position of the line segment, in object space.</param>
        /// <param name="end">The end position of the line segment, in object space.</param>
        /// <returns>False if line segment entirely outside of box;
        /// True if some portion of line segment is inside box</returns>
        public bool ClipLineSegment(ref Vector start, ref Vector end)
        {
            bool startInside = ContainsPoint(start);
            bool endInside = ContainsPoint(end);
            if (startInside && endInside)
            {
                // Line segment is entirely inside this box.
                return true;
            }

            IntersectionInfo intersection = IntersectLineSegment(start, end);
            if (intersection == null)
            {
                // Line segment does not intersect box, so entirely outside of box.
                Contract.Assert(!startInside && !endInside, "Start and end must be outside box");
                return false;
            }

            // Line segment intersects box.

            if (startInside)
            {
                // Only end is outside of box, so clipped line is now entirely within the box.
                end = intersection.pos;
                return true;
            }

            // Start is outside box, so clip to surface of box.
            Vector originalStart = start;
            start = intersection.pos;

            // Is end outside of box?
            if (!endInside)
            {
                // End is outside of box, and original start was outside of box,
                // so original line segment must intersect box at two points.
                intersection = IntersectLineSegment(end, originalStart);
                Contract.Assume(intersection != null, "Intersection must exist");

                // TODO: HACK! This should never happen - logic bug!
                //if (intersection == null)
                //    return true;

                end = intersection.pos;
            }
            return true;
        }

        /// <summary>
        /// Intersect a triangle against this AABB.
        /// TODO: Currently broken!
        /// </summary>
        public bool IntersectsTriangle(Triangle tri)
        {
            // TODO: is this optimisation worth it?
            //if (IsTrianglesOutsidePlanes(tri, planes))
            //{
            //    return false;
            //}

            // This should test for overlap along all 13 axes (1 tri normal, 3 box axes, then 3 tri edges crossproduct 3 box axes)
            // TODO: Unit tests still failing!

            var normal = tri.Plane.Normal;
            if (HasOverlapAlongAxis(normal, tri))
            {
                return true;
            }

            var xAxis = new Vector(1, 0, 0);
            var yAxis = new Vector(0, 1, 0);
            var zAxis = new Vector(0, 0, 1);
            if (HasOverlapAlongAxis(xAxis, tri) ||
                HasOverlapAlongAxis(yAxis, tri) ||
                HasOverlapAlongAxis(zAxis, tri))
            {
                return true;
            }

            if (HasOverlapAlongAxis(tri.Edge1.CrossProduct(xAxis), tri) ||
                HasOverlapAlongAxis(tri.Edge1.CrossProduct(yAxis), tri) ||
                HasOverlapAlongAxis(tri.Edge1.CrossProduct(zAxis), tri) ||
                HasOverlapAlongAxis(tri.Edge2.CrossProduct(xAxis), tri) ||
                HasOverlapAlongAxis(tri.Edge2.CrossProduct(yAxis), tri) ||
                HasOverlapAlongAxis(tri.Edge2.CrossProduct(zAxis), tri) ||
                HasOverlapAlongAxis(tri.Edge3.CrossProduct(xAxis), tri) ||
                HasOverlapAlongAxis(tri.Edge3.CrossProduct(yAxis), tri) ||
                HasOverlapAlongAxis(tri.Edge3.CrossProduct(zAxis), tri))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Does the box overlap the triangle along the given axis?
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="tri"></param>
        /// <returns></returns>
        private bool HasOverlapAlongAxis(Vector axis, Triangle tri)
        {
            Contract.Requires(axis.IsUnitVector);

            double triMin, triMax;
            tri.ProjectOntoAxis(axis, out triMin, out triMax);

            double boxMin, boxMax;
            this.ProjectOntoAxis(axis, out boxMin, out boxMax);

            return triMin < boxMax && boxMin < triMax;
        }

        private void ProjectOntoAxis(Vector axis, out double intervalMin, out double intervalMax)
        {
            Contract.Requires(axis.IsUnitVector);
            Vector centre = (max + min) * 0.5;
            Vector halfSize = (max - min) * 0.5;

            double centreProj = centre.DotProduct(axis);

            // project halfSize vector onto positive axis. Or equivalently project all (centre,corner) vectors onto axis.
            double halfProjX = halfSize.x * Math.Abs(axis.x);
            double halfProjY = halfSize.y * Math.Abs(axis.y);
            double halfProjZ = halfSize.z * Math.Abs(axis.z);
            double halfIntervalSize = halfProjX + halfProjY + halfProjZ;

            intervalMin = centreProj - halfIntervalSize;
            intervalMax = centreProj + halfIntervalSize;
        }

        /// <summary>
        /// Tests whether a triangle is 'outside' one of a set of planes.
        /// </summary>
        /// <param name="tri"></param>
        /// <param name="planes"></param>
        /// <returns>True iff triangle is 'outside' one of the planes</returns>
        private static bool IsTrianglesOutsidePlanes(Triangle tri, IEnumerable<Plane> planes)
        {
            // TODO: a triangle can be 'inside' all planes (each plane has at least one triangle vertex 'inside' the plane),
            // yet the triangle can still not intersect the volume defined by the planes.
            // Do we need to clip the triangle to each plane to figure out if any part of the triangle lies 'inside' all planes?

            foreach (var plane in planes)
            {
                PlaneHalfSpace side = tri.IntersectPlane(plane);
                if ((side & PlaneHalfSpace.NormalSide) == 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}