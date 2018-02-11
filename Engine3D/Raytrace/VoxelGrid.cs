// Only one of these USE_* must be defined. If none are defined, data is not persisted to/from disk.
#if SILVERLIGHT // Silverlight supports isolated storage, but not normal files or memory-mapped files
//#define USE_ISOLATED_STORAGE // TODO: this clas does not support isolated storage
#else // ASP.NET (or standard C#) supports normal files or memory-mapped files, but not isolated storage
#define USE_FILES
#endif

using System;
using System.Diagnostics.Contracts;
using System.IO;

namespace Engine3D.Raytrace
{
    public class VoxelGrid : IRayIntersectable
    {
        public int GridSize { get; private set; }

        private readonly AxisAlignedBox boundingBox;

#if USE_FILES
        private readonly string voxelColorFilePath;
        private readonly string voxelNormalFilePath;
#endif

        private uint[, ,] voxelColors;      // 3D grid of 32-bit colours
        private Vector[, ,] voxelNormals;   // 3D grid of triple of doubles

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="gridSize">Resolution of N x N x N grid of voxels.
        /// A power-of-two size might make indexing into the 3D grid faster.</param>
        /// <param name="instanceKey">A text value unique to the current 3D model instance.</param>
        public VoxelGrid(int gridSize, string instanceKey, string cachePath)
        {
            Contract.Requires(gridSize > 0);
            GridSize = gridSize;
            boundingBox = new AxisAlignedBox(new Vector(-1, -1, -1), new Vector(1, 1, 1));

#if USE_FILES
            voxelColorFilePath = Path.Combine(cachePath, string.Format("{0}_res{1}.color", instanceKey, gridSize));
            voxelNormalFilePath = Path.Combine(cachePath, string.Format("{0}_res{1}.normal", instanceKey, gridSize));

            voxelColors = new uint[gridSize, gridSize, gridSize];
            if (!LoadArrayFromDisk(voxelColorFilePath, voxelColors))
                voxelColors = null;

            double[] normalsAsDoubleArray = new double[gridSize * gridSize * gridSize * 3];
            if (LoadArrayFromDisk(voxelNormalFilePath, normalsAsDoubleArray))
            {
                // Copy voxel normals from array of doubles. LoadArrayFromDisk requires an array of primitives.
                voxelNormals = new Vector[gridSize, gridSize, gridSize];
                Vector normal;
                int i = 0;
                for (var x = 0; x < gridSize; x++)
                {
                    for (var y = 0; y < gridSize; y++)
                    {
                        for (var z = 0; z < gridSize; z++)
                        {
                            normal.x = normalsAsDoubleArray[i++];
                            normal.y = normalsAsDoubleArray[i++];
                            normal.z = normalsAsDoubleArray[i++];
                            voxelNormals[x, y, z] = normal;
                        }
                    }
                }
            }
            else
            {
                voxelNormals = null;
            }
#endif
        }

        public bool HasData
        {
            get
            {
                return voxelColors != null && voxelNormals != null;
            }
        }

        /// <param name="voxelColors">3D grid of voxel colors</param>
        /// <param name="voxelNormals">3D grid of voxel normals</param>
        public void SetData(uint[, ,] voxelColors, Vector[, ,] voxelNormals)
        {
            Contract.Requires(voxelColors != null);
            Contract.Requires(voxelColors.Length == GridSize * GridSize * GridSize);
            Contract.Requires(voxelNormals != null);
            Contract.Requires(voxelNormals.Length == GridSize * GridSize * GridSize);

            this.voxelColors = voxelColors;
            this.voxelNormals = voxelNormals;

#if USE_FILES
            // Copy voxel normals to array of doubles. SaveArrayToDisk requires an array of primitives.
            double[] normalsAsDoubleArray = new double[GridSize * GridSize * GridSize * 3];
            int i = 0;
            for (var x = 0; x < GridSize; x++)
            {
                for (var y = 0; y < GridSize; y++)
                {
                    for (var z = 0; z < GridSize; z++)
                    {
                        var normal = voxelNormals[x, y, z];
                        normalsAsDoubleArray[i++] = normal.x;
                        normalsAsDoubleArray[i++] = normal.y;
                        normalsAsDoubleArray[i++] = normal.z;
                    }
                }
            }

            SaveArrayToDisk(voxelColorFilePath, voxelColors);
            SaveArrayToDisk(voxelNormalFilePath, normalsAsDoubleArray);
#endif
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
            start = (start * 0.5 + half) * ((double)GridSize - 0.001);
            end = (end * 0.5 + half) * ((double)GridSize - 0.001);

            int oldX = -1, oldY = -1, oldZ = -1;

            // TODO: a larger minStep (e.g. 1) makes the voxels look like thin squares facing a single direction. Why?
            foreach (var pos in LineWalker3D.WalkLine(start, end, 0.1))
            {
                int x = (int)pos.x;
                var y = (int)pos.y;
                var z = (int)pos.z;
                Contract.Assert(x >= 0 && x < GridSize && y >= 0 && y < GridSize && z >= 0 && z < GridSize);

                // TODO: Contracts analyser says pos.z is not -1 here, and similarly for x and y
                if (x != oldX && y != oldY && z != oldZ)
                {
                    uint colorSample = voxelColors[x,y,z];

                    // Treat black voxels as transparent
                    if (colorSample != 0)
                    {
                        //var normal = new Vector(oldX - x, oldY - y, oldZ - z);
                        //normal.Normalise();

                        var normal = voxelNormals[x, y, z];
                        Contract.Assume(normal.IsUnitVector);

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

        // Array must be of a primitive type
        private static bool LoadArrayFromDisk(string cacheFilePath, Array data)
        {
#if USE_ISOLATED_STORAGE
            // Isolated storage works in Silverlight but not ASP.NET
            // TODO: not enough free space in isolated storage for such large files - file is corrupted!
            using (IsolatedStorageFile isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!isolatedStorage.FileExists(cacheFilePath))
                    return false;

                using (var fileStream = isolatedStorage.OpenFile(cacheFilePath, System.IO.FileMode.Open))
                {
                    ReadArrayFromStream(fileStream, data);
                }
            }
#elif USE_FILES
            // Cache to normal file for ASP.NET
            // TODO: use memory-mapped files to allow files to be much larger than free memory
            if (!File.Exists(cacheFilePath))
                return false;

            using (var fileStream = File.OpenRead(cacheFilePath))
            {
                ReadArrayFromStream(fileStream, data);
            }
#endif

            return true;
        }

        // Array must be of a primitive type
        private static void SaveArrayToDisk(string cacheFilePath, Array data)
        {
#if USE_ISOLATED_STORAGE
            // Isolated storage works in Silverlight but not ASP.NET
            // TODO: not enough free space in isolated storage for such large files!
            using (IsolatedStorageFile isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (var fileStream = isolatedStorage.CreateFile(cacheFilePath))
                {
                    WriteArrayToStream(data, fileStream);
                }
            }
#else
            // Cache to normal file for ASP.NET
            // TODO: use memory-mapped files to allow files to be much larger than free memory
            using (var fileStream = File.OpenWrite(cacheFilePath))
            {
                WriteArrayToStream(data, fileStream);
            }
#endif
        }

        // TODO: could replace this with Stream.Read(array, offset, count)
        private static void ReadArrayFromStream(Stream stream, Array data)
        {
            byte[] buffer = new byte[4096];
            int destBytePos = 0;
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                Buffer.BlockCopy(buffer, 0, data, destBytePos, bytesRead);
                destBytePos += bytesRead;
            }
        }

        // TODO: could replace this with Stream.Write(array, offset, count)
        private static void WriteArrayToStream(Array data, Stream stream)
        {
            byte[] buffer = new byte[4096];
            int srcBytePos = 0;
            int bytesLeft = Buffer.ByteLength(data); // TODO: this throws if Array does not contain primitives
            while (bytesLeft > 0)
            {
                int bytesThisBlock = Math.Min(buffer.Length, bytesLeft);
                Buffer.BlockCopy(data, srcBytePos, buffer, 0, bytesThisBlock);
                stream.Write(buffer, 0, bytesThisBlock);
                srcBytePos += bytesThisBlock;
                bytesLeft -= bytesThisBlock;
            }
        }
    }
}