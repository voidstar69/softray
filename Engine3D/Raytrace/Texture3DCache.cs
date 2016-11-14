// Only one of these USE_* must be defined. If none are defined, data is not persisted to/from disk.
#if SILVERLIGHT // Silverlight supports isolated storage, but not normal files or memory-mapped files
//#define USE_ISOLATED_STORAGE
#else // ASP.NET (or standard C#) supports normal files or memory-mapped files, but not isolated storage
#define USE_FILES
#endif

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.IsolatedStorage;

namespace Engine3D.Raytrace
{
    // TODO: finish this class!
    // TODO: has this class been debugged? Probably not! Copy proven code from AmbientOcclusion class.
    class Texture3DCache<T> : Texture3D<T>
        where T : IComparable
    {
        // Value indicating an empty cache entry
        public readonly T EmptyCacheEntry;

        // Value to use instead, if attempt is made to write the empty cache value to the cache
        public readonly T EmptyCacheEntryReplacement;

        // TODO: this wastes loads of memory - this should be a sparse 3D array
        // TODO: cache is not reused across website renders - persist it to a file for reuse
        // TODO: turn into properties or separate class
        // TODO: Power-of-two size might make 3D array indexing quicker
        private readonly int cacheSize;
        private readonly T[] cacheData;
        private readonly string cacheFilePath;
        private readonly GenerateTextureSample sampleGenerator;
        private readonly object calcLock = new object();


        private readonly int numCalcsBetweenPersists;
        private int numCalcsUntilNextPersist;

        // Texture coordinates should probably fall within the unit cube centred at the origin
        // Must not return default(T). This is used as a sentinel value.
        public delegate T GenerateTextureSample(Vector pos);

        public bool EnableCache { get; set; }

        public bool EnableFileCache
        {
            get { return enableFileCache; }
            set
            {
#if SILVERLIGHT // Silverlight supports isolated storage, but not normal files or memory-mapped files
                if(value)
                    throw new NotSupportedException("Silverlight does not support reading/writing to normal files");
#endif
                enableFileCache = value; 
            }
        }
        private bool enableFileCache;

        public Texture3DCache(int cubicCacheSize, string instanceKey, T emptyEntryValue, T emptyEntryReplacementValue, GenerateTextureSample sampleGenerator)
        {
            Contract.Requires(cubicCacheSize > 0);
            cacheSize = cubicCacheSize;
            this.sampleGenerator = sampleGenerator;
            this.EmptyCacheEntry = emptyEntryValue;
            this.EmptyCacheEntryReplacement = emptyEntryReplacementValue;
            cacheData = new T[cacheSize * cacheSize * cacheSize];
            numCalcsBetweenPersists = Math.Min(5000, cacheData.Length / 4);
            numCalcsUntilNextPersist = numCalcsBetweenPersists;
            EnableCache = true;

#if USE_FILES
            EnableFileCache = true;
#else
            EnableFileCache = false; // TODO: a Silverlight app can be told to cache to file at runtime, but Silverlight cannot access files, so this will cause a failure
#endif

            cacheFilePath = string.Format("{0}_res{1}.cache", instanceKey, cacheSize);

            if (EnableFileCache)
            {
                // TODO: temporary hack to store shadow cache in single global location
                // TODO: store cached shadows on external HDD not internal SSD, to avoid shortening life of my SSD!!!
                cacheFilePath = @"C:\Temp\" + this.cacheFilePath;

                LoadCacheFromDisk(cacheFilePath, cacheData);
            }
        }

        // Multithread safe.
        public T Sample(Vector pos)
        {
            // TODO: sometimes a surface point coordinate is very slightly outside the unit cube (i.e. Z coordinate of 0.50000000001)
            // TODO: clamp 'close' coordinates to the unit cube?
            Assert.IsTrue(-0.5 <= pos.x && pos.x <= 0.5, "Texture3D coordinate is outside unit cube");
            Assert.IsTrue(-0.5 <= pos.y && pos.y <= 0.5, "Texture3D coordinate is outside unit cube");
            Assert.IsTrue(-0.5 <= pos.z && pos.z <= 0.5, "Texture3D coordinate is outside unit cube");
            var cacheIndex = (int)((pos.x + 0.5) * (cacheSize - 1)) * cacheSize * cacheSize +
                                (int)((pos.y + 0.5) * (cacheSize - 1)) * cacheSize +
                                (int)((pos.z + 0.5) * (cacheSize - 1));

            // Fetch texture cache entry, but if it is missing then generate the texture sample via a delegate function

            if (!EnableCache)
            {
                return sampleGenerator(pos);
            }

            // Double-check locking pattern
            T sample = cacheData[cacheIndex];
            if (sample.Equals(EmptyCacheEntry))
            {
                lock (calcLock)
                {
                    if (cacheData[cacheIndex].Equals(EmptyCacheEntry))
                    {
                        sample = sampleGenerator(pos);

                        // Do not allow the 'empty cache entry' value to be written to the cache. Replace this with a nearby value.
                        if (sample.CompareTo(EmptyCacheEntry) == 0)
                            sample = EmptyCacheEntryReplacement;

                        cacheData[cacheIndex] = sample;

                        // Time to persist cache data to file in isolated storage?
                        numCalcsUntilNextPersist--;
                        if (numCalcsUntilNextPersist <= 0 && EnableFileCache)
                        {
                            // TODO: lock may be held for long time!
                            SaveCacheToDisk(cacheFilePath, cacheData);
                            numCalcsUntilNextPersist = numCalcsBetweenPersists;
                        }
                    }
                }
            }

            return sample;
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
#else
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
#else
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