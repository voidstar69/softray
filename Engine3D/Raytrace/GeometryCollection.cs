using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    public class GeometryCollection : IRayIntersectable
    {
        private readonly List<IRayIntersectable> _geomList = new List<IRayIntersectable>();
        private int numRayTests;

        public void Add(IRayIntersectable geometry)
        {
            _geomList.Add(geometry);
        }

        public int Count
        {
            get
            {
                return _geomList.Count;
            }
        }

        public IRayIntersectable this[int index]
        {
            get
            {
                Contract.Requires(index >= 0);
                return _geomList[index];
            }
        }

        /// <summary>
        /// A running total of the number of ray-geometry tests performed.
        /// </summary>
        public int RayGeometryTestCount { set; get; }

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

            foreach (IRayIntersectable geometry in _geomList)
            {
                IntersectionInfo curr = geometry.IntersectRay(start, dir, context);
                if (curr != null && curr.rayFrac < closest.rayFrac)
                {
                    closest = curr;
                }
                numRayTests += geometry.NumRayTests;
                RayGeometryTestCount++;
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
    }
}