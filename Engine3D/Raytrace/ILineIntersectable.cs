using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    [ContractClass(typeof(ContractForILineIntersectable))]
    public interface ILineIntersectable
    {
        /// <summary>
        /// Intersect a line segment this object.
        /// </summary>
        /// <param name="start">The start position of the line segment, in object space.</param>
        /// <param name="end">The end position of the line segment, in object space.</param>
        /// <returns>Information about the first intersection, or null if no intersection.</returns>
        IntersectionInfo IntersectLineSegment(Vector start, Vector end);
    }

    [ContractClassFor(typeof(ILineIntersectable))]
    public abstract class ContractForILineIntersectable : ILineIntersectable
    {
        public IntersectionInfo IntersectLineSegment(Vector start, Vector dir)
        {
            Contract.Ensures(Contract.Result<IntersectionInfo>() == null || Contract.Result<IntersectionInfo>().normal.IsUnitVector);
            Contract.Ensures(Contract.Result<IntersectionInfo>() == null || Contract.Result<IntersectionInfo>().rayFrac >= 0);
            Contract.Ensures(Contract.Result<IntersectionInfo>() == null || Contract.Result<IntersectionInfo>().color > 0);
            return default(IntersectionInfo);
        }
    }
}