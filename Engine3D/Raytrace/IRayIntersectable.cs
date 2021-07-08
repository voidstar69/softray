using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    public class IntersectionInfo
    {
        // TODO: make fields readonly?
        public double rayFrac;  // distance to intersection in multiples of the ray direction
        public Vector pos;      // position of the point of intersection (in object space)
        public Vector normal;   // normal to the surface at the point of intersection (unit length; in object space)
//        public double u;        // 1st material coordinate (normalised)
//        public double v;        // 2nd material coordinate (normalised)
        public uint color; // TODO: change this to a Color to avoid bit manipulation when modulating colors

        // TODO: store triangle index or reference to intersected object?
        //public IRayIntersectable objHit;
        public int triIndex = -1;   // -1 means that the intersected geometry is not a triangle

        public override bool Equals(object obj)
        {
            var other = obj as IntersectionInfo;
            if (other == null)
                return false;

            return rayFrac == other.rayFrac &&
                   pos == other.pos &&
                   normal == other.normal &&
                   color == other.color;
        }

        public override int GetHashCode()
        {
            return ((rayFrac.GetHashCode()
                   * 31 + pos.GetHashCode())
                   * 31 + normal.GetHashCode())
                   * 31 + color.GetHashCode();
        }
    }

    public class RenderContext
    {
        public RenderContext(System.Random randomNumberGenerator)
        {
            RNG = randomNumberGenerator;
        }

        public System.Random RNG { get; private set; }
    }

    [ContractClass(typeof(ContractForIRayIntersectable))]
    public interface IRayIntersectable
    {
        /// <summary>
        /// Intersect a ray against this object.
        /// </summary>
        /// <param name="start">The start position of the ray, in object space.</param>
        /// <param name="dir">The direction of the ray, in object space (not a unit vector).</param>
        /// <returns>Information about the nearest intersection, or null if no intersection.</returns>
        /// <remarks>Must be thread safe.</remarks>
        IntersectionInfo IntersectRay(Vector start, Vector dir, RenderContext context);

        /// <summary>
        /// The number of basic ray tests performed during the last call to IntersectRay.
        /// For simple objects this should always return 1.
        /// For complex objects this will return the number of sub-objects tested against the ray.
        /// </summary>
        int NumRayTests { get; }
    }

    [ContractClassFor(typeof(IRayIntersectable))]
    public abstract class ContractForIRayIntersectable : IRayIntersectable
    {
        public IntersectionInfo IntersectRay(Vector start, Vector dir, RenderContext context)
        {
            Contract.Ensures(Contract.Result<IntersectionInfo>() == null || Contract.Result<IntersectionInfo>().normal.IsUnitVector);
            Contract.Ensures(Contract.Result<IntersectionInfo>() == null || Contract.Result<IntersectionInfo>().rayFrac >= 0);
            Contract.Ensures(Contract.Result<IntersectionInfo>() == null || Contract.Result<IntersectionInfo>().triIndex >= -1);
            Contract.Ensures(Contract.Result<IntersectionInfo>() == null || (Contract.Result<IntersectionInfo>().color & 0xff000000) == 0xff000000); // no transparency allowed
            //Contract.Ensures(Contract.Result<IntersectionInfo>() == null || Contract.Result<IntersectionInfo>().objHit != null);
            return default(IntersectionInfo);
        }

        public int NumRayTests
        {
            get
            {
                Contract.Ensures(Contract.Result<int>() >= 0);
                return default(int);
            }
        }
    }
}