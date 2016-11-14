using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    public enum PlaneHalfSpace
    {
        NormalSide = 1,
        BackSide = 2
    }

    [ContractClass(typeof(ContractForIPlaneIntersectable))]
    public interface IPlaneIntersectable
    {
        /// <summary>
        /// Intersect a plane against this object.
        /// </summary>
        /// <param name="plane">The plane to test for intersection against.</param>
        /// <returns>The plane half-spaces intersected by the object, as a bitwise enumeration.
        /// The normal side is the half-space in the direction of the plane normal.
        /// The back side is the half-space in the opposite direction.</returns>
        PlaneHalfSpace IntersectPlane(Plane plane);
    }

    [ContractClassFor(typeof(IPlaneIntersectable))]
    public abstract class ContractForIPlaneIntersectable : IPlaneIntersectable
    {
        public PlaneHalfSpace IntersectPlane(Plane plane)
        {
            // Only these two enum flags are allowed in the bitmask
            Contract.Ensures((Contract.Result<PlaneHalfSpace>() & ~(PlaneHalfSpace.NormalSide | PlaneHalfSpace.BackSide)) == 0);
            return default(PlaneHalfSpace);
        }
    }
}