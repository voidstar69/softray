using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    using Voxel = System.UInt32;

    public class VoxelGrid : IRayIntersectable
    {
        private readonly int gridSize;
        private readonly Voxel[, ,] voxelColors;    // 32-bit colours
        private readonly Vector[, ,] voxelNormals;
        private readonly AxisAlignedBox boundingBox;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="gridSize">Resolution of 3D grid</param>
        /// <param name="voxelColors">3D grid of voxel colors</param>
        /// <param name="voxelNormals">3D grid of voxel normals</param>
        public VoxelGrid(int gridSize, Voxel[, ,] voxelColors, Vector[, ,] voxelNormals)
        {
            Contract.Requires(gridSize > 0);
            Contract.Requires(voxelColors != null);
            Contract.Requires(voxelColors.Length == gridSize * gridSize * gridSize);
            Contract.Requires(voxelNormals != null);
            Contract.Requires(voxelNormals.Length == gridSize * gridSize * gridSize);
            this.gridSize = gridSize;
            this.voxelColors = voxelColors;
            this.voxelNormals = voxelNormals;
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

            int oldX = -1, oldY = -1, oldZ = -1;

            // TODO: a larger minStep (e.g. 1) makes the voxels look like thin squares facing a single direction. Why?
            foreach (var pos in LineWalker3D.WalkLine(start, end, 0.1))
            {
                int x = (int)pos.x;
                var y = (int)pos.y;
                var z = (int)pos.z;
                Contract.Assert(x >= 0 && x < gridSize && y >= 0 && y < gridSize && z >= 0 && z < gridSize);

                if (x != oldX && y != oldY && z != oldZ)
                {
                    Voxel colorSample = voxelColors[x,y,z];

                    // Treat black voxels as transparent
                    if (colorSample != 0)
                    {
                        //var normal = new Vector(oldX - x, oldY - y, oldZ - z);
                        //normal.Normalise();

                        var normal = voxelNormals[x, y, z];
                        Contract.Assert(normal.IsUnitVector);

                        // DEBUGGING: visualise normals
                        //colorSample = new Color(normal.x, normal.y, normal.z).ToARGB();

                        // TODO: also return correct rayFrac, pos and normal
                        return new IntersectionInfo { color = colorSample, normal = normal /*, pos = pos, rayFrac = (pos - start).Length */ };
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