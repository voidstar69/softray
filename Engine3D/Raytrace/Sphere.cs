using System;
using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    public class Sphere : IRayIntersectable, ILineIntersectable
    {
        private const double epsilon = 1e-10;
        private readonly Vector center;
        private readonly double radius;
        private readonly double radiusSqr;

        [ContractInvariantMethod]
        private void ClassContract()
        {
            Contract.Invariant(radius > 0);
            Contract.Invariant(Math.Abs(radius * radius - radiusSqr) < epsilon);
        }

        /// <summary>
        /// Define a 3D sphere
        /// </summary>
        /// <param name="center">The center of the sphere</param>
        /// <param name="radius">The radius of the sphere</param>
        public Sphere(Vector center, double radius)
        {
            Contract.Requires(radius > 0);
            this.center = center;
            this.radius = radius;
            this.radiusSqr = radius * radius;
            this.Color = Color.White;
        }

        // TODO: turn this into a struct for performance?
        /// <summary>
        /// Longitude and latitude of a point on the surface of a sphere
        /// </summary>
        public class LatLong
        {
            public readonly double horizAngle; // longitude of point on sphere,            -π ≤ horizAngle ≤ π    (zero degrees at back of sphere, increasing clockwise)
            public readonly double vertAngle;  // latitude of point on sphere, (bottom)  -π/2 ≤ vertAngle  ≤ π/2  (top)

            public LatLong(double horizAngle, double vertAngle)
            {
                this.horizAngle = horizAngle;
                this.vertAngle = vertAngle;
            }
        }

        private uint color;
        public Color Color
        {
            set { color = value.ToARGB(); }
        }

        [Pure]
        public bool ContainsPoint(Vector pt)
        {
            return (pt - center).LengthSqr < radiusSqr;
        }

        /// <summary>
        /// Intersect an (infinite) line againt this sphere, and get spherical coordinates of any intersection points.
        /// </summary>
        /// <param name="linePt1">The first point on the line.</param>
        /// <param name="linePt2">The second point on the line. Must not be the same point as <paramref name="linePt1"/></param>
        /// <param name="spherePt1">Spherical coordinates of first intersection point between line and sphere. Null if no intersections at all.</param>
        /// <param name="spherePt2">Spherical coordinates of second intersection point between line and sphere. Null if no intersections at all.</param>
        public void IntersectLine(Vector linePt1, Vector linePt2, out LatLong spherePt1, out LatLong spherePt2)
        {
            Contract.Requires(linePt1 != linePt2);
            Contract.Ensures((Contract.ValueAtReturn(out spherePt1) == null && Contract.ValueAtReturn(out spherePt1) == null) ||
                              Contract.ValueAtReturn(out spherePt1) != Contract.ValueAtReturn(out spherePt2));

            // TODO: pass in line origin and line direction instead? Faster inside and outside this method!
            // TODO: optimise line-sphere intersection code

            // Make dir a unit vector.
            // TODO: not strictly neccessary, but it makes the quadratic equation a bit simpler
            var dir = linePt2 - linePt1;
            dir.Normalise();

            // Solve quadratic equation to determine number of intersection points between (infinite) line and sphere: 0, 1 or 2 intersections
            var sphereToStart = linePt1 - center; // o - c
            var sphereToStartProjDir = sphereToStart.DotProduct(dir); // l.(o - c)
            var sphereToStartDistSqr = sphereToStart.LengthSqr; // |o - c|^2
            var termUnderSqrRoot = sphereToStartProjDir * sphereToStartProjDir - sphereToStartDistSqr + radiusSqr; // (l.(o - c))^2 - |o - c|^2 + r^2
            if (termUnderSqrRoot < epsilon) // use epsilon to avoid unstable pixels flickering between one root and zero roots
            {
                // line does not intersect sphere (or line touches sphere at exactly one point)
                spherePt1 = null;
                spherePt2 = null;
                return;
            }

            var positiveSqrRoot = Math.Sqrt(termUnderSqrRoot);
            var intersectLineFrac1 = -sphereToStartProjDir - positiveSqrRoot; // distance along line from first line point to first intersection
            var intersectLineFrac2 = -sphereToStartProjDir + positiveSqrRoot; // distance along line from first line point to second intersection (might be same as first intersection)

            // work out spherical angles of first intersection point
            var intersectPt = linePt1 + dir * intersectLineFrac1;
            var sphereToHitPt = intersectPt - center;
            var horizAngle = Math.Atan2(sphereToHitPt.x, sphereToHitPt.z);  // longitude of point on sphere, -π ≤ θ ≤ π
            var vertAngle = Math.Asin(sphereToHitPt.y / radius);            // latitude of point on sphere, -π/2 ≤ θ ≤ π/2
            spherePt1 = new LatLong(horizAngle, vertAngle);

            // work out spherical angles of second intersection point
            intersectPt = linePt1 + dir * intersectLineFrac2;
            sphereToHitPt = intersectPt - center;
            horizAngle = Math.Atan2(sphereToHitPt.x, sphereToHitPt.z);  // longitude of point on sphere, -π ≤ θ ≤ π
            vertAngle = Math.Asin(sphereToHitPt.y / radius);            // latitude of point on sphere, -π/2 ≤ θ ≤ π/2
            spherePt2 = new LatLong(horizAngle, vertAngle);
        }

        /// <summary>
        /// Convert two spherical coordinates (each have latitude and longitude) to the line segment joining the two spherical coordinates
        /// </summary>
        /// <param name="spherePt1">Spherical coordinates of first point on sphere</param>
        /// <param name="spherePt2">Spherical coordinates of second point on sphere. Must not be the same coordinates as <paramref name="spherePt1"/></param>
        /// <param name="linePt1">The first point on sphere (in 3D orthogonal coordinates)</param>
        /// <param name="linePt2">The second point on sphere (in 3D orthogonal coordinates)</param>
        public void ConvertLine(LatLong spherePt1, LatLong spherePt2, out Vector linePt1, out Vector linePt2)
        {
            Contract.Requires(spherePt1 != null);
            Contract.Requires(spherePt2 != null);
            Contract.Requires(spherePt1.horizAngle != spherePt2.horizAngle || spherePt1.vertAngle != spherePt2.vertAngle);
            Contract.Ensures(Contract.ValueAtReturn(out linePt1) != Contract.ValueAtReturn(out linePt2));

            // determine 3D orthogonal coordinates of first spherical coordinate
            var horizLength = Math.Cos(spherePt1.vertAngle) * radius;
            linePt1 = new Vector(
                Math.Sin(spherePt1.horizAngle) * horizLength + center.x,
                Math.Sin(spherePt1.vertAngle) * radius + center.y,
                Math.Cos(spherePt1.horizAngle) * horizLength + center.z);

            // determine 3D orthogonal coordinates of second spherical coordinate
            horizLength = Math.Cos(spherePt2.vertAngle) * radius;
            linePt2 = new Vector(
                Math.Sin(spherePt2.horizAngle) * horizLength + center.x,
                Math.Sin(spherePt2.vertAngle) * radius + center.y,
                Math.Cos(spherePt2.horizAngle) * horizLength + center.z);
        }

        #region IRayIntersectable

        /// <summary>
        /// Intersect a ray against this object.
        /// </summary>
        /// <param name="start">The start position of the ray, in object space.</param>
        /// <param name="dir">The direction of the ray, in object space (not a unit vector).</param>
        /// <returns>Information about the nearest intersection, or null if no intersection.</returns>
        public IntersectionInfo IntersectRay(Vector start, Vector dir, RenderContext context)
        {
            // TODO: optimise ray-sphere intersection code

            // Make dir a unit vector.
            // TODO: not strictly neccessary, but it makes the quadratic equation a bit simpler
            dir.Normalise();

            // Solve quadratic equation to determine number of intersection points between (infinite) line and sphere: 0, 1 or 2 intersections
            Vector sphereToStart = start - center; // o - c
            double sphereToStartProjDir = sphereToStart.DotProduct(dir); // l.(o - c)
            double sphereToStartDistSqr = sphereToStart.LengthSqr; // |o - c|^2

            // Is the entire sphere behind the start of the ray?
            if(sphereToStartProjDir > radiusSqr)
                return null;

            // TODO: ignore rays starting inside the sphere?
            // Is any part of the sphere behind the start of the ray?
//            if(sphereToStartProjDir > -radiusSqr)
//                return null;
            // Is the sphere surrounding the ray start? TODO: is this ever true after the previous check?
//            if(sphereToStartDistSqr < radiusSqr)
//                return null;
            
            double termUnderSqrRoot = sphereToStartProjDir * sphereToStartProjDir - sphereToStartDistSqr + radiusSqr; // (l.(o - c))^2 - |o - c|^2 + r^2
            if (termUnderSqrRoot < epsilon) // use epsilon to avoid unstable pixels flickering between one root and zero roots
                // (infinite) line does not intersect sphere
                return null;
 
            double positiveSqrRoot = Math.Sqrt(termUnderSqrRoot);
            double intersectRayFrac1 = -sphereToStartProjDir - positiveSqrRoot; // distance along line from ray-start to first intersection
            double intersectRayFrac2 = -sphereToStartProjDir + positiveSqrRoot; // distance along line from ray-start to second intersection (might be same as first intersection)

            double rayFrac = (intersectRayFrac1 >= 0 ? intersectRayFrac1 : intersectRayFrac2);
            if (rayFrac < 0)
                // (infinite) line intersects sphere, but ray does not
                return null;

            // Determine details of nearest intersection point
            // TODO: rearchitect to avoid calculating intersection position and normal unless this is the first object hit
            IntersectionInfo info = new IntersectionInfo();
            info.rayFrac = rayFrac;
            info.pos = start + dir * rayFrac;
            info.normal = info.pos - center;
            info.normal.Normalise();
            info.color = color;

            // TODO: this is for debugging spherical angles!
/*
            // work out spherical angles of intersection point
            var sphereToHitPt = info.pos - center;
            var horizAngle = Math.Atan2(sphereToHitPt.x, sphereToHitPt.z);  // longitude of point on sphere, -π ≤ θ ≤ π
            var vertAngle = Math.Asin(sphereToHitPt.y / radius);            // latitude of point on sphere, -π/2 ≤ θ ≤ π/2

            //var visualNorm = horizAngle / Math.PI / 2.0 + 0.5;  // [0, 1]
            var visualNorm = vertAngle / Math.PI + 0.5;         // [0, 1]
            //byte visualNormByte = (byte)(visualNorm * 255);     // [0, 255]
            var visualNormInt = (int)(visualNorm * 255);        // [0, 255]
            info.color = (uint)(visualNormInt << 8);
            //info.color = (uint)((visualNormByte << 16) + (visualNormByte << 8) + visualNormByte);
*/

            return info;
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
                return 1;
            }
        }

        #endregion

        #region ILineIntersectable

        /// <summary>
        /// Intersect a line segment this object.
        /// </summary>
        /// <param name="start">The start position of the line segment, in object space.</param>
        /// <param name="end">The end position of the line segment, in object space.</param>
        /// <returns>Information about the first intersection, or null if no intersection.</returns>
        public IntersectionInfo IntersectLineSegment(Vector start, Vector end)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
