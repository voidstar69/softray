using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    using Voxel = System.UInt32;

    public class VoxelGrid : IRayIntersectable
    {
        private readonly int gridSize;
        private readonly Voxel[,,] voxels; // 32-bit colours

        private readonly AxisAlignedBox boundingBox;

        public VoxelGrid(int gridSize, Voxel[,,] voxels)
        {
            Contract.Requires(gridSize > 0);
            Contract.Requires(voxels != null);
            Contract.Requires(voxels.Length == gridSize * gridSize * gridSize);
            this.gridSize = gridSize;
            this.voxels = voxels;
            boundingBox = new AxisAlignedBox(new Vector(-1, -1, -1), new Vector(1, 1, 1));
        }

        /// <summary>
        /// Intersect a ray against this object.
        /// </summary>
        /// <param name="start">The start position of the ray, in object space.</param>
        /// <param name="dir">The direction of the ray, in object space (not a unit vector).</param>
        /// <returns>Information about the nearest intersection, or null if no intersection.</returns>
        public IntersectionInfo IntersectRay(Vector start, Vector dir, RenderContext context)
        {
            // TODO: some rays should be short (e.g. ambient occlusion rays). Make the max ray distance an optional parameter?
            Vector end = start + dir * 10;

            //if (!boundingBox.OverlapsLineSegment(start, end))
            // TODO: clipping line segment breaks IntersectionInfo.rayFrac, because ray is truncated, but rayFrac does not reflect this!
            if (!boundingBox.ClipLineSegment(ref start, ref end))
            {
                return null;
            }

            // Scale up from (unit) object space to voxel grid resolution
            var half = new Vector(0.5, 0.5, 0.5);
            start = (start * 0.5 + half) * ((double)gridSize - 0.001);
            end = (end * 0.5 + half) * ((double)gridSize - 0.001);

            // TODO: a larger minStep (e.g. 1) makes the voxels look like thin squares facing a single direction. Why?
            foreach (var pos in LineWalker3D.WalkLine(start, end, 0.1))
            {
                int x = (int)pos.x;
                var y = (int)pos.y;
                var z = (int)pos.z;

                if (x >= 0 && x < gridSize &&
                    y >= 0 && y < gridSize &&
                    z >= 0 && z < gridSize)
                {
                    //var voxelIndex = (x * gridSize * gridSize) + (y * gridSize) + z;
                    //Voxel sample = voxels[voxelIndex];
                    Voxel sample = voxels[x,y,z];

                    // Treat black voxels as transparent
                    if (sample != 0)
                    {
                        // TODO: convert sample to a colour, and return it along with rayFrac, pos and normal
                        return new IntersectionInfo { color = sample, normal = Vector.Up };
                        //return new IntersectionInfo { color = sample, pos = pos, rayFrac = (pos - start).Length, normal = Vector.Up };
                    }
                }
            }

            return null;
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
    }
}