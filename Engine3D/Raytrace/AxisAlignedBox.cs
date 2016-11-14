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
        public IntersectionInfo IntersectRay(Vector start, Vector dir)
        {
            numRayTests = 0;
            IntersectionInfo closest = new IntersectionInfo();
            closest.rayFrac = double.MaxValue;

            foreach (Plane plane in planes)
            {
                // Does ray intersect this plane?
                IntersectionInfo curr = plane.IntersectRay(start, dir);
                numRayTests += plane.NumRayTests;
                if (curr != null && curr.rayFrac < closest.rayFrac)
                {
                    // Does the intersection point lie on the surface of this box?
                    if(ContainsPoint(curr.pos))
                    //if (Close(curr.pos.x, min.x) || Close(curr.pos.x, max.x) ||
                    //    Close(curr.pos.y, min.y) || Close(curr.pos.y, max.y) ||
                    //    Close(curr.pos.z, min.z) || Close(curr.pos.z, max.z))
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

            foreach (Plane plane in planes)
            {
                // Does ray intersect this plane?
                IntersectionInfo curr = plane.IntersectLineSegment(start, end);
                numRayTests += plane.NumRayTests;
                if (curr != null && curr.rayFrac < closest.rayFrac)
                {
                    // Does the intersection point lie on the surface of this box?
                    if(ContainsPoint(curr.pos))
                    //if (Close(curr.pos.x, min.x) || Close(curr.pos.x, max.x) ||
                    //    Close(curr.pos.y, min.y) || Close(curr.pos.y, max.y) ||
                    //    Close(curr.pos.z, min.z) || Close(curr.pos.z, max.z))
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

            //return min.x <= pos.x && pos.x <= max.x &&
            //       min.y <= pos.y && pos.y <= max.y &&
            //       min.z <= pos.z && pos.z <= max.z;
        }

        //private bool PointInOrNearBox(Vector pos)
        //{
        //    return min.x - epsilon < pos.x && pos.x < max.x + epsilon &&
        //           min.y - epsilon < pos.y && pos.y < max.y + epsilon &&
        //           min.z - epsilon < pos.z && pos.z < max.z + epsilon;
        //}

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
                end = intersection.pos;
            }
            return true;
        }

        /// <summary>
        /// Are two numbers very close together?
        /// </summary>
        //private bool Close(double a, double b)
        //{
        //    return Math.Abs(a - b) < 1e-10;
        //}
    }
}
