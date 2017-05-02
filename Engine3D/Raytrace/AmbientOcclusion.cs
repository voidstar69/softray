// Only one of these USE_* must be defined. If none are defined, data is not persisted to/from disk.
#if SILVERLIGHT // Silverlight supports isolated storage, but not normal files or memory-mapped files
//#define USE_ISOLATED_STORAGE
#else // ASP.NET (or standard C#) supports normal files or memory-mapped files, but not isolated storage
#define USE_FILES
#endif

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using Engine3D;

#if USE_ISOLATED_STORAGE
using System.IO.IsolatedStorage;
#endif

// TODO: class is not multithread safe. This breaks IsolatedStorage (especially when stepping through code in the debugger)! Puts locks around this usage.
// TODO: class should be generic like LightField4D, but this requires client code to convert to/from generic type. Extract CalcAmbientOcclusion method to client code?
// TODO: this should use Texture3DCache, to avoid duplicate code. Has that class been debugged?

namespace Engine3D.Raytrace
{
    using T = System.Byte;

    /// <summary>
    /// Calculates and caches ambient occlusion.
    /// Ambient occlusion is calculated via raytracing.
    /// </summary>
    public sealed class AmbientOcclusion : IDisposable
    {
        // TODO: raising ambientOcclusionQuality from 10 to 100 introduces glaring artifacts due to race conditions!
        public int ambientOcclusionQuality = 100;           // affects performance linearly
        public double ambientOcclusionProbeOffset = 0.001;  // prevents self-shadowing of surface
        public double ambientOcclusionProbeDist = 2;        // distance to search for neighboring surfaces

        // TODO: hacky attempt to cache nearly ambient occlusion results, to reuse on nearby surfaces
        // TODO: this wastes loads of memory - this should be a sparse 3D array
        // TODO: cache is not reused across website renders - persist it to a file for reuse
        // TODO: turn into properties or separate class
        // TODO: Power-of-two size might make 3D array indexing quicker
        private readonly int cacheSize;
        private readonly T[] cacheData;
        private readonly string cacheFilePath;

        private const int numCalcsBetweenPersists = 10000;
        private int numCalcsUntilNextPersist = numCalcsBetweenPersists;
        private readonly object calcLock = new object();

        // Value indicating an empty cache entry
        private readonly T EmptyCacheEntry = 0;

        public bool EnableCache { get; set; }

        //private IsolatedStorageFile isolatedStorage;

        /// <summary>
        /// Create structure to generate and store Ambient Occlusion for a single 3D model (that fits within the unit cube).
        /// </summary>
        /// <param name="cubicCacheSize">The resolution of the cache of values within the space of the unit cube.
        /// Cache has size N x N x N. A power-of-two size might make indexing into the 3D array quicker.</param>
        /// <param name="modelKey">A text value unique to the current model.</param>
        public AmbientOcclusion(int cubicCacheSize, string modelKey, string cachePath)
        {
            Contract.Requires(cubicCacheSize > 0);
            Contract.Requires(!string.IsNullOrEmpty(modelKey));
            Contract.Requires(!string.IsNullOrEmpty(cachePath));
            this.cacheSize = cubicCacheSize;
            this.cacheData = new T[cacheSize * cacheSize * cacheSize];
            this.cacheFilePath = string.Format("{0}_res{1}.ao", modelKey, cacheSize);
            //this.isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication();
            EnableCache = true;

#if USE_FILES
            // TODO: temporary hack to store AO cache in single global location
            // TODO: store cached AO on external HDD not internal SSD, to avoid shortening life of my SSD!!!
            this.cacheFilePath = Path.Combine(cachePath, this.cacheFilePath);
#endif

            LoadCacheFromDisk(cacheFilePath, cacheData);
        }

        public void Dispose()
        {
            SaveCacheToDisk(cacheFilePath, cacheData);
        }

        //public void CalcAllAmbientOcclusion()
        //{
        //    // TODO: write brute force algorithm: for every cell in 3D cube, find intersecting triangles and calc AO for these? Or just find nearest triangle to each cell center?
        //}

        /// <summary>
        /// Calculates and caches ambient occlusion at a point on a surface.
        /// Multithread safe.
        /// </summary>
        /// <param name="surface">Information about surface point</param>
        /// <param name="geometry">The geometry to be raytraced (both occlusion caster and occlusion receiver)</param>
        /// <param name="random">Random number generator to use. Set to <value>NULL</value> to use the default random number generator</param>
        /// <returns>Fraction of non-occlusion at surface point (between 1 and 255): 1 = surface point fully occluded by neighbour surfaces; 255 = surface point not occluded at all</returns>
        public T CacheAmbientOcclusion(IntersectionInfo surface, Raytrace.IRayIntersectable geometry, RenderContext context)
        {
            Contract.Requires(surface != null);

            // TODO: Sometimes a surface point coordinate is very slightly outside the unit cube (i.e. Z coordinate of 0.50000000001)
            const double maxExtent = 0.5; // TODO: extent seems too large on Couch model...
            const double scale = 0.5 / maxExtent;

            // So we clamp coordinates to the unit cube
            surface.pos.x = Math.Min(Math.Max(-0.5, surface.pos.x), 0.5);
            surface.pos.y = Math.Min(Math.Max(-0.5, surface.pos.y), 0.5);
            surface.pos.z = Math.Min(Math.Max(-0.5, surface.pos.z), 0.5);
/*
            Assert.IsTrue(-maxExtent <= surface.pos.x && surface.pos.x <= maxExtent, "Surface point is outside unit cube");
            Assert.IsTrue(-maxExtent <= surface.pos.y && surface.pos.y <= maxExtent, "Surface point is outside unit cube");
            Assert.IsTrue(-maxExtent <= surface.pos.z && surface.pos.z <= maxExtent, "Surface point is outside unit cube");
*/ 
            var cacheIndex = (int)((surface.pos.x * scale + 0.5) * (cacheSize - 1)) * cacheSize * cacheSize +
                            (int)((surface.pos.y * scale + 0.5) * (cacheSize - 1)) * cacheSize +
                            (int)((surface.pos.z * scale + 0.5) * (cacheSize - 1));

            // fetch cache entry, but if missing then calc AO factor
            // TODO: probably not multithread safe!
            // TODO: tri-linearly interpolate AO factor from eight neighbouring cache entries for smoother results!

            // Cheap thread lock - prevent other threads recalculating this value, while it is calculated by this thread
            // TODO: cannot be used with byte values, but could be used with ints
            //if(0 == Interlocked.CompareExchange<byte>(ref cacheData[cacheIndex], (byte)1, (byte)0))

            if (!EnableCache)
            {
                // calc shading intensity based on percentage AO shadowing
                double unoccludedFactor = CalcAmbientOcclusion(surface, geometry, context);
                // scale to range [1, 255]. We avoid zero as this is a sentinel value indicating empty cache entry.
                return (T)(unoccludedFactor * 254 + 1);
            }

            // Double-check locking pattern
            // TODO: rare IndexOutOfRangeException here
            T lightByte = cacheData[cacheIndex];
            if (EmptyCacheEntry == lightByte)
            {
                lock (calcLock)
                {
                    // another thread may have got the lock first, and already calculated this value
                    if (EmptyCacheEntry == cacheData[cacheIndex])
                    {
                        // calc shading intensity based on percentage AO shadowing
                        double unoccludedFactor = CalcAmbientOcclusion(surface, geometry, context);
                        // scale to range [1, 255]. We avoid zero as this is a sentinel value indicating empty cache entry.
                        lightByte = (T)(unoccludedFactor * 254 + 1);
                        cacheData[cacheIndex] = lightByte;

                        // Time to persist cache data to file in isolated storage?
                        numCalcsUntilNextPersist--;
                        if (numCalcsUntilNextPersist <= 0)
                        {
                            // TODO: lock may be held for long time!
                            SaveCacheToDisk(cacheFilePath, cacheData);
                            numCalcsUntilNextPersist = numCalcsBetweenPersists;
                        }
                    }
                }
            }

            return lightByte;
        }

        /// <summary>
        /// Calculates ambient occlusion at a point on a surface.
        /// </summary>
        /// <param name="surface">Information about surface point</param>
        /// <param name="geometry">The geometry to be raytraced (both occlusion caster and occlusion receiver)</param>
        /// <param name="random">Random number generator to use</param>
        /// <returns>Fraction of non-occlusion at surface point (between 0 and 1): 0 = surface point fully occluded by neighbour surfaces; 1 = surface point not occluded at all</returns>
        private double CalcAmbientOcclusion(IntersectionInfo surface, Raytrace.IRayIntersectable geometry, RenderContext context)
        {
            Contract.Ensures(0 <= Contract.Result<double>() && Contract.Result<double>() <= 1);

            var random = context.RNG;
            var rayStart = surface.pos + surface.normal * ambientOcclusionProbeOffset;

            Vector avgEscapedRayDir = new Vector();
            int rayEscapeCount = 0;
            for (int i = 0; i < ambientOcclusionQuality; i++)
            {
                // Pick a random direction within the hemisphere around the surface normal
                // TODO: works for external surfaces, but some self-intersection on interior surfaces
                var rayDir = new Vector(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1);
                //rayDir.Normalise();
                if (rayDir.DotProduct(surface.normal) < 0)
                    rayDir = -rayDir;

                // Pick random directions until we find one roughly in the same direction as the surface normal
                //Vector rayDir;
                //double cosOfAngle;
                //do
                //{
                //    rayDir = new Vector(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1);
                //    rayDir.Normalise();
                //    cosOfAngle = rayDir.DotProduct(surfaceInfo.normal);

                //    // force ray to be in the hemisphere around the surface normal
                //} while (cosOfAngle < 0.0);

                // Fire off ray to check for nearby surface in chosen direction
                // TODO: might be more efficient if ray tracing stopped after a short distance from ray origin
                Raytrace.IntersectionInfo shadowInfo = geometry.IntersectRay(rayStart, rayDir, context);
                if (shadowInfo == null || shadowInfo.rayFrac > ambientOcclusionProbeDist)
                {
                    // This ray did not hit a nearby surface
                    rayEscapeCount++;
                    avgEscapedRayDir += rayDir;
                }
            }


            // visualise direction of unobstructed space around surface point
            //avgEscapedRayDir.Normalise();
            //var v = new Vector(avgEscapedRayDir.x + 1, avgEscapedRayDir.y + 1, avgEscapedRayDir.z + 1);
            //v *= 128;
            //return Surface.PackRgb((byte)v.x, (byte)v.y, (byte)v.z);

            //avgEscapedRayDir.Normalise();
            //avgEscapedRayDir *= 255;
            //return Surface.PackRgb((byte)avgEscapedRayDir.x, (byte)avgEscapedRayDir.y, (byte)avgEscapedRayDir.z);


            return (double)rayEscapeCount / (double)ambientOcclusionQuality;
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