using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace Engine3D.Raytrace
{
    public sealed class MemMappedArray<T> : IDisposable
        where T : struct
    {
        private readonly int elementSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T)); // in bytes
        private readonly MemoryMappedFile memoryMappedFile;
        private MemoryMappedViewAccessor memMapViewAccessor; // use the View property rather than using this field directly
        private bool alreadyDisposed = false;

        /// <summary>
        /// Create a memory-mapped array backed by the system page-file.
        /// Works up to a size of 1GB on my laptop. After that a "not enough storage" IOException is thrown.
        /// </summary>
        /// <param name="arrayLength"></param>
        /// <param name="mapName"></param>
        public MemMappedArray(long arrayLength, string mapName)
        {
            memoryMappedFile = MemoryMappedFile.CreateOrOpen(mapName, arrayLength * elementSize, MemoryMappedFileAccess.ReadWrite);
        }

        /// <summary>
        /// Create a memory-mapped array backed by a file on disk.
        /// If the file does not exist, it is immediately created, with a size to match the array size.
        /// If the file already exists, it must be no smaller than the size of the array (in bytes).
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="arrayLength">The length of the array (in elements).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the array is smaller than the file on disk (measured in bytes)</exception>
        public MemMappedArray(string filePath, long arrayLength)
        {
            Contract.Requires(filePath != null);

            // map names must not contain backslashes
            var mapName = filePath.Replace('\\', '/');

            try
            {
                memoryMappedFile = MemoryMappedFile.CreateFromFile(filePath, System.IO.FileMode.OpenOrCreate, mapName, arrayLength * elementSize);
            }
            catch (DirectoryNotFoundException ex)
            {
                // There is an issue with the map name
                throw new ArgumentException("Map name is invalid", "mapName", ex);
            }
            catch (System.IO.IOException)
            {
                // assume that memory-mapped object already exists in this process
                // TODO: once in a while, this throws saying "Unable to find the specified file". Is the mem-map object automatically reclaimed?
                memoryMappedFile = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.ReadWrite);
            }
        }

        private MemoryMappedViewAccessor View
        {
            get
            {
                if (null == memMapViewAccessor)
                {
                    try
                    {
                        memMapViewAccessor = memoryMappedFile.CreateViewAccessor();
                    }
                    catch (IOException ex)
                    {
                        // assume that we have run out of physical or virtual memory in this process
                        // TODO: this seems to occur often (for a 800MB file), even if we dispose of the view and mmap file! Too much memory usage in a 32-bit .NET process?
                        throw new ArgumentException("Not enough physical or virtual memory to create view of memory-mapped file", "arrayLength", ex);
                    }
                }

                return memMapViewAccessor;
            }
        }

        public void Dispose()
        {
            FreeVirtualMemory();
            if (!alreadyDisposed)
            {
                memoryMappedFile.Dispose();
                alreadyDisposed = true;
            }
        }

        public void FreeVirtualMemory()
        {
            if (null != memMapViewAccessor)
            {
                memMapViewAccessor.Flush();
                memMapViewAccessor.Dispose();
            }
            memMapViewAccessor = null;
        }

        public T this[int index] {
            get
            {
                T element;
                View.Read(index * elementSize, out element);
                return element;
            }

            set
            {
                View.Write(index * elementSize, ref value);
            }
        }
    }
}