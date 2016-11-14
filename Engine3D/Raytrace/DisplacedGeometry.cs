namespace Engine3D.Raytrace
{
    /// <summary>
    /// TODO
    /// </summary>
    public class DisplacedGeometry : IRayIntersectable
    {
        /// <summary>
        /// Intersect a ray against this object.
        /// </summary>
        /// <param name="start">The start position of the ray, in object space.</param>
        /// <param name="dir">The direction of the ray, in object space. This is a unit vector.</param>
        /// <returns>Information about the nearest intersection, or null if no intersection.</returns>
        public IntersectionInfo IntersectRay(Vector start, Vector dir)
        {
            // TODO
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
    }
}
