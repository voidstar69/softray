using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    public class Triangle : IRayPlaneIntersectable
    {
        private readonly Plane plane;
        private readonly Point vertex1;
        private readonly Point vertex2;
        private readonly Point vertex3;

        private readonly Vector edge1;
        private readonly Vector edge2;

        private readonly Vector edge1Perp;
        private readonly Vector edge2Perp;

        private readonly uint color;

        /// <summary>
        /// Define a 3D triangle. The triangle is one-sided.
        /// </summary>
        /// <param name="v1">The first vertex of the triangle.</param>
        /// <param name="v2">The second vertex of the triangle.</param>
        /// <param name="v3">The third vertex of the triangle.</param>
        /// <param name="color">The color of the triangle.</param>
        public Triangle(Vector v1, Vector v2, Vector v3, uint color)
        {
            Contract.Requires((color & 0xff000000) == 0xff000000); // no transparency allowed

            vertex1 = new Point(v1);
            vertex2 = new Point(v2);
            vertex3 = new Point(v3);
            this.color = color;

            // Compute this triangle's normal
            edge1 = v2 - v1;
            edge2 = v3 - v1;
            Vector normal = edge1.CrossProduct(edge2);
            if (normal.IsZeroVector)
                normal = new Vector(1, 0, 0);

            // Create the plane that this triangle lies within.
            plane = new Plane(v1, normal);

            // Calculate properties of two edges, to quickly determine if a point in the plane is also in the triangle.
            edge1Perp = edge1.CrossProduct(normal);
            edge2Perp = edge2.CrossProduct(normal);

            // Only used by ray tracer when using lightfield storing triangle indices
            TriangleIndex = -1;
        }

        public Vector Vertex1 { get { return vertex1.Position; } }
        public Vector Vertex2 { get { return vertex2.Position; } }
        public Vector Vertex3 { get { return vertex3.Position; } }

        public uint Color { get { return color; } }

        public Plane Plane { get { return plane; } }

        // Only used by ray tracer with lightfield storing triangle indices
        public int TriangleIndex { get; set; }

        // Only used by ray tracer with lightfield storing triangle indices
        public object HandleToLeafNode { get; set; }

        /// <summary>
        /// Intersect a ray against this object.
        /// </summary>
        /// <param name="start">The start position of the ray, in object space.</param>
        /// <param name="dir">The direction of the ray, in object space (not a unit vector).</param>
        /// <returns>Information about the nearest intersection, or null if no intersection.</returns>
        public IntersectionInfo IntersectRay(Vector start, Vector dir, RenderContext context)
        {
            IntersectionInfo info = plane.IntersectRay(start, dir, context);
            if (info != null)
            {
                Assert.IsTrue(info.rayFrac >= 0.0, "Ray fraction is negative");
                Vector v1ToIntersection = info.pos - vertex1.Position;
                double s = v1ToIntersection.DotProduct(edge2Perp) / edge1.DotProduct(edge2Perp);
                // TODO: bail out early if s < 0 or s > 1? Speeds up this code.
                double t = v1ToIntersection.DotProduct(edge1Perp) / edge2.DotProduct(edge1Perp);
                if (s >= 0.0 && t >= 0.0 && s + t <= 1.0)
                {
                    info.color = color;
                    info.triIndex = TriangleIndex;
                    return info;
                }
            }
            return null;
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

        /// <summary>
        /// Intersect a plane against this object.
        /// </summary>
        /// <param name="plane">The plane to test for intersection against.</param>
        /// <returns>The plane half-spaces intersected by the object, as a bitwise enumeration.
        /// The normal side is the half-space in the direction of the plane normal.
        /// The back side is the half-space in the opposite direction.</returns>
        public PlaneHalfSpace IntersectPlane(Plane plane)
        {
            PlaneHalfSpace planeHalfSpace = vertex1.IntersectPlane(plane);
            planeHalfSpace |= vertex2.IntersectPlane(plane);
            planeHalfSpace |= vertex3.IntersectPlane(plane);
            return planeHalfSpace;
        }
    }
}