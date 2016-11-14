namespace Engine3D.Raytrace
{
    public class Point : IPlaneIntersectable
    {
        private Vector pos;

        /// <summary>
        /// Define a 3D point.
        /// </summary>
        /// <param name="p">The vector giving the position of the point (relative to the original of space).</param>
        public Point(Vector p)
        {
            pos = p;
        }

        /// <summary>
        /// Get the position of this point in space.
        /// </summary>
        public Vector Position
        {
            get
            {
                return pos;
            }
        }

        /// <summary>
        /// Intersect a plane against this object.
        /// </summary>
        /// <param name="plane">The plane to test for intersection against.</param>
        /// <returns>The plane half-spaces intersected by the object, as a bitwise enumeration.
        /// The normal side is the half-space in the direction of the plane normal.
        /// The back side is the half-space in the opposite direction.</returns>
        /// <remarks>The point is guarenteed to intersect one of the half-spaces, but never both.</remarks>
        public PlaneHalfSpace IntersectPlane(Plane plane)
        {
            // Calculate the distance from the origin of space to this point, measured along the plane normal.
            double distToOriginAlongNormal = pos.DotProduct(plane.Normal);

            // Which of the plane's two half-spaces does this point lie within?
            // Note that if this point lies on the plane, we assign it to an arbitrary half-space.
            if (distToOriginAlongNormal > plane.DistanceToOrigin)
            {
                return PlaneHalfSpace.NormalSide;
            }
            else
            {
                return PlaneHalfSpace.BackSide;
            }
        }
    }
}
