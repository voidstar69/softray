using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    public class Plane : IRayIntersectable, ILineIntersectable
    {
        private const double epsilon = 1e-10;
        private readonly Vector _normal; // this does not have to be a unit vector; we only make it a unit vector to be able to return a unit normal.
        private readonly double _originDist; // distance from origin to plane along normal, in multiples of the normal.

        [ContractInvariantMethod]
        private void ClassContract()
        {
            Contract.Invariant(_normal.IsUnitVector);
        }

        /// <summary>
        /// Define a 3D plane. The plane is one-sided.
        /// </summary>
        /// <param name="point">Any point in the plane.</param>
        /// <param name="normal">The normal to the plane. This does not need to be a unit vector.</param>
        public Plane(Vector point, Vector normal)
        {
            Contract.Requires(!normal.IsZeroVector);
            _normal = normal;
            _normal.Normalise();
            _originDist = point.DotProduct(_normal);
            Color = Color.White;
        }

        private uint color;
        public Color Color
        {
            set { color = value.ToARGB(); }
        }

        /// <summary>
        /// Get the normal to this plane. This is a unit vector.
        /// </summary>
        public Vector Normal
        {
            get
            {
                Assert.IsTrue(_normal.IsUnitVector, "Normal must be unit vector");
                return _normal;
            }
        }

        /// <summary>
        /// Get the distance from the origin to this plane, measured along the direction of the normal.
        /// </summary>
        public double DistanceToOrigin
        {
            get
            {
                Assert.IsTrue(_normal.IsUnitVector, "Normal must be unit vector");
                return _originDist;
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
            // Project the start and direction vectors onto the plane normal.
            // Note that all distances are in multiples of the normal, NOT in space units!
            double startDist = start.DotProduct(_normal);
            double dirDist = dir.DotProduct(_normal);

            // Make the plane one-sided.
            if (dirDist >= 0.0)
            {
                return null;
            }

            double rayFrac = _originDist - startDist;
            if (rayFrac <= 0.0 /* epsilon */)
            {
                rayFrac /= dirDist;

                IntersectionInfo info = new IntersectionInfo();
                info.pos = start + dir * rayFrac;
                info.normal = _normal;
                info.rayFrac = rayFrac;

                info.color = color;

                // TODO: better parameterisation of the surface
//                info.u = info.pos.x;
//                info.v = info.pos.z;

                Assert.IsTrue(info.rayFrac >= 0.0, "Ray fraction is negative");
                return info;
            }
            else
            {
                return null;
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
            // Project the start and end vectors onto the plane normal.
            // Note that all distances are in multiples of the normal, NOT in space units!
            double startDist = start.DotProduct(_normal);
            double endDist = end.DotProduct(_normal);

            double lineFrac = (_originDist - startDist) / (endDist - startDist);
            if (0.0 <= lineFrac && lineFrac <= 1.0)
            {
                IntersectionInfo info = new IntersectionInfo();
                info.rayFrac = lineFrac;
                info.pos = start + (end - start) * lineFrac;

                // TODO: not strictly neccessary
                info.normal = _normal;
                info.color = 0x00ffffff;

                // TODO: better parameterisation of the surface
                //                info.u = info.pos.x;
                //                info.v = info.pos.z;
                return info;
            }
            else
            {
                return null;
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
                return 1;
            }
        }
    }
}
