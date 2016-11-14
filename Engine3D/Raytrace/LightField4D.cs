// Only one of these USE_* must be defined. If none are defined, data is not persisted to/from disk.
#if SILVERLIGHT // Silverlight supports isolated storage, but not normal files or memory-mapped files
//#define USE_ISOLATED_STORAGE
#else // ASP.NET (or standard C#) supports normal files or memory-mapped files, but not isolated storage
//#define USE_FILES
//#define USE_MEMORY_MAPPED_FILES
#endif

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Collections.Generic;

#if USE_ISOLATED_STORAGE
using System.IO.IsolatedStorage;
#endif

namespace Engine3D.Raytrace
{
    using Coord4D = Tuple<byte, byte, byte, byte>;
    using Float4D = Tuple<double, double, double, double>;

    // TODO: disposing is only strictly neccessary when USE_MEMORY_MAPPED_FILES is defined

    public sealed class LightField4D<T> : IDisposable
        where T : struct, IComparable
    {
        // Value indicating an empty cache entry
        public readonly T EmptyCacheEntry;

        // Value to use instead, if attempt is made to write the empty cache value to the cache
        public readonly T EmptyCacheEntryReplacement;

        public readonly int cacheRes;
        private readonly string cacheFilePath;

#if USE_MEMORY_MAPPED_FILES
        private readonly MemMappedArray<T> cacheData;
#else
        // TODO: this wastes loads of memory - this should be a sparse 4D array
        // TODO: cache is not reused across website renders - persist it to a file for reuse
        // TODO: turn into properties or separate class
        // TODO: Power-of-two size might make 4D array indexing quicker
        private readonly T[] cacheData;

        private readonly object calcLock = new object();
        private readonly int numCalcsBetweenPersists; // was 10000, now calculated in constructor
        private int numCalcsUntilNextPersist;
#endif

        private readonly Sphere boundingSphere;

        private static int numLightFields = 0; // only for debugging

        /// <summary>
        /// Create structure to generate and store 4D lightfield data for a single 3D model (that fits within the unit cube).
        /// </summary>
        /// <param name="cacheRes">The resolution of the 4D cache. Cache has size 2N x N x 2N x N (yaw covers 360 degrees; pitch covers 180 degrees).
        /// A power-of-two size might make indexing into the 4D array quicker.</param>
        /// <param name="instanceKey">A text value unique to the current 3D model.</param>
        public LightField4D(byte cacheRes, string instanceKey, T emptyEntryValue, T emptyEntryReplacementValue, string cachePath)
        {
            Contract.Requires(cacheRes > 0);
            // cacheRes is a byte because we use bytes to represent the 4D coordinates (anyway cacheRes of 256 would mean a cache size of 16 giga-entries!)

            numLightFields++;

            // TODO: get the actual sphere bounding the geometry. This would improve light field resolution.
            this.boundingSphere = new Sphere(new Vector(0, 0, 0), 0.866); // the sphere bounding the unit cube centred at the origin

            this.EmptyCacheEntry = emptyEntryValue;
            this.EmptyCacheEntryReplacement = emptyEntryReplacementValue;
            this.cacheRes = cacheRes;
            this.cacheFilePath = string.Format("{0}_res{1}.cache", instanceKey, cacheRes);

#if USE_MEMORY_MAPPED_FILES

            // TODO: store cached lightfields on external HDD not internal SSD, to avoid shortening life of my SSD!!!
            this.cacheFilePath = Path.Combine(cachePath, this.cacheFilePath);
            //this.cacheFilePath = @"C:\Temp\" + this.cacheFilePath;
            // TODO: path too long for memory-mapped files? Probably not.
            //this.cacheFilePath = @"C:\Src\SVN\ModelLibrary\database\3dRenders\v4\lightfields\" + this.cacheFilePath;

            this.cacheData = new MemMappedArray<T>(this.cacheFilePath, CacheResToBufferSize(cacheRes));

#else // files, isolated storage, or no persistence to disk

            this.cacheData = AllocMemBuffer4D(this.cacheRes, out this.cacheRes);
            this.cacheFilePath = string.Format("{0}_res{1}.cache", instanceKey, this.cacheRes);
#if USE_FILES
            // TODO: store cached lightfields on external HDD not internal SSD, to avoid shortening life of my SSD!!!
            this.cacheFilePath = Path.Combine(cachePath, this.cacheFilePath);
            //this.cacheFilePath = @"C:\Temp\" + this.cacheFilePath;
#endif
            this.numCalcsBetweenPersists = this.cacheRes * this.cacheRes * 7 / 10;
            this.numCalcsUntilNextPersist = this.numCalcsBetweenPersists;

            // TODO: not enough free space in isolated storage for such large files - file is corrupted!
            LoadCacheFromDisk(cacheFilePath, cacheData);

#endif

#if USE_ISOLATED_STORAGE
            using (IsolatedStorageFile isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                // This invariably fails, as isolated storage is currently far too small!
                Contract.Assert(CacheResToBufferSize(cacheRes) < isolatedStorage.AvailableFreeSpace, "Not enough free space in isolated storage for lightfield");

                // TODO: figure out how to increase size of isolated storage. Does user need to interact with UI for size increase to be allowed?

                // TODO: always silently fails and returns false
                //bool foo = isolatedStorage.IncreaseQuotaTo(100 * 1024 * 1024);
            }
#endif
        }

        public void Dispose()
        {
#if USE_MEMORY_MAPPED_FILES
            cacheData.Dispose();
#else
            // if cache is dirty, write it out to disk
            if (numCalcsUntilNextPersist < numCalcsBetweenPersists)
            {
                SaveCacheToDisk(cacheFilePath, cacheData);
            }
#endif

            numLightFields--;
        }

        public void FreeVirtualMemory()
        {
#if USE_MEMORY_MAPPED_FILES
            cacheData.FreeVirtualMemory();
#endif
        }

        /// <returns>Size of cache buffer (in T elements)</returns>
        private static uint CacheResToBufferSize(uint cacheRes)
        {
            return cacheRes * cacheRes * cacheRes * cacheRes * 4;
        }

        private static T[] AllocMemBuffer4D(int desiredCacheRes, out int actualCacheRes)
        {
            // create 4D data structure. Reduce its size if insufficient memory!
            T[] buffer = null;
            int resolution = desiredCacheRes;
            while (null == buffer)
            {
                try
                {
                    // u and s coordinates have double the resolution of v and t
                    buffer = new T[CacheResToBufferSize((uint)resolution)];
                }
                catch (OutOfMemoryException)
                {
                    buffer = null; // to avoid confusing the Contracts analyser
                    //resolution /= 2;
                    resolution -= 16;
                }
            }

            actualCacheRes = resolution;
            return buffer;
        }

        /// <summary>
        /// Convert a ray to a 4D coordinate within the light field (if a coordinate exists for the ray).
        /// </summary>
        /// <param name="start">The start position of the ray, in object space. Must be outside the lightfield bounding sphere! (Passed by ref for efficiency - it is not modified).</param>
        /// <param name="dir">The direction of the ray, in object space (Not a unit vector. Passed by ref for efficiency - it is not modified).</param>
        /// <returns>A 4D coordinate within the light field, or null if no corresponding coordinate exists.</returns>
        public Coord4D RayToCoord4D(ref Vector start, ref Vector dir)
        {
            Contract.Requires(!dir.IsZeroVector);
            Contract.Ensures(Contract.Result<Coord4D>() == null || Contract.Result<Coord4D>().Item1 < cacheRes * 2);
            Contract.Ensures(Contract.Result<Coord4D>() == null || Contract.Result<Coord4D>().Item2 < cacheRes);
            Contract.Ensures(Contract.Result<Coord4D>() == null || Contract.Result<Coord4D>().Item3 < cacheRes * 2);
            Contract.Ensures(Contract.Result<Coord4D>() == null || Contract.Result<Coord4D>().Item4 < cacheRes);
            Contract.Ensures(Contract.ValueAtReturn(out start) == Contract.OldValue(start));
            Contract.Ensures(Contract.ValueAtReturn(out dir) == Contract.OldValue(dir));
            Contract.Assert(!boundingSphere.ContainsPoint(start));

            // Intersect ray with lightfield bounding sphere, to produce two spherical coordinates
            Sphere.LatLong spherePt1, spherePt2;
            boundingSphere.IntersectLine(start, start + dir, out spherePt1, out spherePt2);

            if (spherePt1 == null)
                // line does not intersect sphere
                return null;

            var u = spherePt1.horizAngle / Math.PI * 0.5 + 0.5; // [0, 1]
            var v = spherePt1.vertAngle / Math.PI + 0.5;        // [0, 1]
            var s = spherePt2.horizAngle / Math.PI * 0.5 + 0.5; // [0, 1]
            var t = spherePt2.vertAngle / Math.PI + 0.5;        // [0, 1]

            // TODO: calc cacheIndex here and return it instead of 4D coordinate? Makes it difficult to find cache entry canonical ray, and to peform quad-linear interpolation!

            return Tuple.Create(
                (byte)(u * (cacheRes * 2 - 1)), // [0, cacheRes * 2 - 1]
                (byte)(v * (cacheRes - 1)),     // [0, cacheRes - 1]
                (byte)(s * (cacheRes * 2 - 1)), // [0, cacheRes * 2 - 1]
                (byte)(t * (cacheRes - 1)));    // [0, cacheRes - 1]
        }

        /// <summary>
        /// Convert a ray to a 4D floating-point coordinate within the light field (if a coordinate exists for the ray).
        /// </summary>
        /// <param name="start">The start position of the ray, in object space. Must be outside the lightfield bounding sphere! (Passed by ref for efficiency - it is not modified).</param>
        /// <param name="dir">The direction of the ray, in object space (Not a unit vector. Passed by ref for efficiency - it is not modified).</param>
        /// <returns>A 4D coordinate within the light field, or null if no corresponding coordinate exists.</returns>
        public Float4D RayToFloat4D(ref Vector start, ref Vector dir)
        {
            Contract.Requires(!dir.IsZeroVector);
            Contract.Ensures(Contract.Result<Float4D>() == null || Contract.Result<Float4D>().Item1 < cacheRes * 2);
            Contract.Ensures(Contract.Result<Float4D>() == null || Contract.Result<Float4D>().Item2 < cacheRes);
            Contract.Ensures(Contract.Result<Float4D>() == null || Contract.Result<Float4D>().Item3 < cacheRes * 2);
            Contract.Ensures(Contract.Result<Float4D>() == null || Contract.Result<Float4D>().Item4 < cacheRes);
            Contract.Ensures(Contract.ValueAtReturn(out start) == Contract.OldValue(start));
            Contract.Ensures(Contract.ValueAtReturn(out dir) == Contract.OldValue(dir));
            Contract.Assert(!boundingSphere.ContainsPoint(start));

            // Intersect ray with lightfield bounding sphere, to produce two spherical coordinates
            Sphere.LatLong spherePt1, spherePt2;
            boundingSphere.IntersectLine(start, start + dir, out spherePt1, out spherePt2);

            if (spherePt1 == null)
                // line does not intersect sphere
                return null;

            var u = spherePt1.horizAngle / Math.PI * 0.5 + 0.5; // [0, 1]
            var v = spherePt1.vertAngle / Math.PI + 0.5;        // [0, 1]
            var s = spherePt2.horizAngle / Math.PI * 0.5 + 0.5; // [0, 1]
            var t = spherePt2.vertAngle / Math.PI + 0.5;        // [0, 1]

            // TODO: calc cacheIndex here and return it instead of 4D coordinate? Makes it difficult to find cache entry canonical ray, and to peform quad-linear interpolation!

            return Tuple.Create(
                (u * (cacheRes * 2 /* - 1*/)), // [0, cacheRes * 2 - 1]
                (v * (cacheRes /*- 1*/)),     // [0, cacheRes - 1]
                (s * (cacheRes * 2 /*- 1*/)), // [0, cacheRes * 2 - 1]
                (t * (cacheRes /*- 1*/)));    // [0, cacheRes - 1]
        }

        /// <summary>
        /// Convert a 4D coordinate within the light field, to a ray travelling from first spherical point to second spherical point.
        /// </summary>
        /// <param name="coord4D">4D coordinate within the light field</param>
        /// <param name="start">The first point on the sphere</param>
        /// <param name="dir">The vector from the first to second points on the sphere</param>
        public void Coord4DToRay(Coord4D coord4D, out Vector start, out Vector dir)
        {
            Contract.Requires(coord4D != null);
            Contract.Requires(coord4D.Item1 < cacheRes * 2);
            Contract.Requires(coord4D.Item2 < cacheRes);
            Contract.Requires(coord4D.Item3 < cacheRes * 2);
            Contract.Requires(coord4D.Item4 < cacheRes);
            Contract.Ensures(!Contract.ValueAtReturn(out dir).IsZeroVector);

            // generate the canonical ray associated with this lightfield entry
            double maxValueUandS = cacheRes * 2 - 1;
            double maxValueVandT = cacheRes - 1;
            var u = (((double)coord4D.Item1 + 0.5) / maxValueUandS - 0.5) * Math.PI * 2;    // [-π, π]
            var v = (((double)coord4D.Item2 + 0.5) / maxValueVandT - 0.5) * Math.PI;        // [-π/2, π/2]
            var s = (((double)coord4D.Item3 + 0.5) / maxValueUandS - 0.5) * Math.PI * 2;    // [-π, π]
            var t = (((double)coord4D.Item4 + 0.5) / maxValueVandT - 0.5) * Math.PI;        // [-π/2, π/2]
            Sphere.LatLong spherePt1 = new Sphere.LatLong(u, v);
            Sphere.LatLong spherePt2 = new Sphere.LatLong(s, t);
            boundingSphere.ConvertLine(spherePt1, spherePt2, out start, out dir);
            dir = dir - start;
        }

        /// <summary>
        /// Convert a 4D coordinate within the light field, to a ray travelling from first spherical point to second spherical point.
        /// </summary>
        /// <param name="coord4D">4D coordinate within the light field</param>
        /// <param name="start">The first point on the sphere</param>
        /// <param name="dir">The vector from the first to second points on the sphere</param>
        public void Float4DToRay(Float4D coord4D, out Vector start, out Vector dir)
        {
            Contract.Requires(coord4D != null);
            Contract.Requires(coord4D.Item1 < cacheRes * 2);
            Contract.Requires(coord4D.Item2 < cacheRes);
            Contract.Requires(coord4D.Item3 < cacheRes * 2);
            Contract.Requires(coord4D.Item4 < cacheRes);
            Contract.Ensures(!Contract.ValueAtReturn(out dir).IsZeroVector);

            // generate the canonical ray associated with this lightfield entry
            double maxValueUandS = cacheRes * 2 /*- 1*/;
            double maxValueVandT = cacheRes /*- 1*/;
            var u = ((coord4D.Item1 /*+ 0.5*/) / maxValueUandS - 0.5) * Math.PI * 2;    // [-π, π]
            var v = ((coord4D.Item2 /*+ 0.5*/) / maxValueVandT - 0.5) * Math.PI;        // [-π/2, π/2]
            var s = ((coord4D.Item3 /*+ 0.5*/) / maxValueUandS - 0.5) * Math.PI * 2;    // [-π, π]
            var t = ((coord4D.Item4 /*+ 0.5*/) / maxValueVandT - 0.5) * Math.PI;        // [-π/2, π/2]
            Sphere.LatLong spherePt1 = new Sphere.LatLong(u, v);
            Sphere.LatLong spherePt2 = new Sphere.LatLong(s, t);
            boundingSphere.ConvertLine(spherePt1, spherePt2, out start, out dir);
            dir = dir - start;
        }

        // Thread safe
        public T ReadCache(int u, int v, int s, int t)
        {
            Contract.Requires(0 <= u && u < cacheRes * 2);
            Contract.Requires(0 <= v && v < cacheRes);
            Contract.Requires(0 <= s && s < cacheRes * 2);
            Contract.Requires(0 <= t && t < cacheRes);

            // TODO: covert these into explicit left shifts? cacheRes needs to be replaced by a power of two
            // TODO: calc this only once outside this function?
            var cacheIndex = (int)(u * cacheRes * cacheRes * cacheRes * 2) +
                             (int)(v * cacheRes * cacheRes * 2) +
                             (int)(s * cacheRes) +
                             (int)(t);

            // Fetch lightfield cache entry (which might be missing)
            return cacheData[cacheIndex];
        }

        // Thread safe
        public void WriteCache(int u, int v, int s, int t, T value)
        {
            Contract.Requires(0 <= u && u < cacheRes * 2);
            Contract.Requires(0 <= v && v < cacheRes);
            Contract.Requires(0 <= s && s < cacheRes * 2);
            Contract.Requires(0 <= t && t < cacheRes);

            // TODO: covert these into explicit left shifts? cacheRes needs to be replaced by a power of two
            // TODO: calc this only once outside this function?
            var cacheIndex = (int)(u * cacheRes * cacheRes * cacheRes * 2) +
                             (int)(v * cacheRes * cacheRes * 2) +
                             (int)(s * cacheRes) +
                             (int)(t);

            // Do not allow the 'empty cache entry' value to be written to the cache. Replace this with a nearby value.
            if (value.CompareTo(EmptyCacheEntry) == 0)
                value = EmptyCacheEntryReplacement;

            // Create/overwrite lightfield cache entry
            // TODO: do we need locking to ensure another thread does not overwrite this lightfield cache entry?
            cacheData[cacheIndex] = value;

            // Cheap thread lock - prevent other threads recalculating this value, while it is calculated by this thread
            // TODO: cannot be used with byte values, but could be used with ints
            //if(0 == Interlocked.CompareExchange<byte>(ref cacheData[cacheIndex], (byte)1, (byte)0))
              
#if !USE_MEMORY_MAPPED_FILES // no need to explicitly persist data to disk when using memory-mapped files
            lock (calcLock)
            {
                // Time to persist cache data to file in isolated storage?
                numCalcsUntilNextPersist--;
                if (numCalcsUntilNextPersist <= 0)
                {
                    // TODO: lock may be held for long time!
                    SaveCacheToDisk(cacheFilePath, cacheData);
                    numCalcsUntilNextPersist = numCalcsBetweenPersists;
                }
            }
#endif
        }

        private static void LoadCacheFromDisk(string cacheFilePath, T[] cacheData)
        {
#if USE_ISOLATED_STORAGE
            // Isolated storage works in Silverlight but not ASP.NET
            // TODO: not enough free space in isolated storage for such large files - file is corrupted!
            using (IsolatedStorageFile isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (isolatedStorage.FileExists(cacheFilePath))
                {
                    using (var fileStream = isolatedStorage.OpenFile(cacheFilePath, System.IO.FileMode.Open))
                    {
                        ReadArrayFromStream(fileStream, cacheData);
                    }
                }
            }
#elif USE_FILES
            // Cache to normal file for ASP.NET
            // TODO: use memory-mapped files to allow files to be much larger than free memory
            if (File.Exists(cacheFilePath))
            {
                using (var fileStream = File.OpenRead(cacheFilePath))
                {
                    ReadArrayFromStream(fileStream, cacheData);
                }
            }
#endif
        }

        private static void SaveCacheToDisk(string cacheFilePath, T[] cacheData)
        {
#if USE_ISOLATED_STORAGE
            // Isolated storage works in Silverlight but not ASP.NET
            // TODO: not enough free space in isolated storage for such large files!
            using (IsolatedStorageFile isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (var fileStream = isolatedStorage.CreateFile(cacheFilePath))
                {
                    WriteArrayToStream(cacheData, fileStream);
                }
            }
#elif USE_FILES
            // Cache to normal file for ASP.NET
            // TODO: use memory-mapped files to allow files to be much larger than free memory
            using (var fileStream = File.OpenWrite(cacheFilePath))
            {
                WriteArrayToStream(cacheData, fileStream);
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
            int bytesLeft = Buffer.ByteLength(data);
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