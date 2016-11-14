using System;
using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    public class AmbientOcclusionMethod : IRayIntersectable, IDisposable
    {
        // The underlying geometry to raytrace
        private readonly IRayIntersectable geometry;

        private readonly int resolution;
        private readonly string instanceKey;
        private readonly string cachePath;

        // Ambient occlusion cache, to calculate and store results from ambient occlusion calculations
        private AmbientOcclusion ambientOcclusionCache;

        private bool EnableAoCache = true;

        public bool Enabled { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="geometry"></param>
        /// <param name="resolution">The resolution of the 3D texture for caching ambient occlusion.
        /// A power-of-two size might make indexing into the 3D texture quicker.</param>
        /// <param name="instanceKey">A text value unique to the current 3D model.</param>
        /// <remarks>Not thread safe</remarks>
        public AmbientOcclusionMethod(IRayIntersectable geometry, int resolution, string instanceKey, string cachePath)
        {
            Contract.Requires(resolution > 0);
            Enabled = true;
            this.geometry = geometry;
            this.resolution = resolution;
            this.instanceKey = instanceKey;
            this.cachePath = cachePath;
        }

        public void Dispose()
        {
            if (ambientOcclusionCache != null)
                ambientOcclusionCache.Dispose();
        }

        public bool EnableCache
        {
            get
            {
                return EnableAoCache;
            }
            set
            {
                EnableAoCache = value;
            }
        }

        /// <summary>
        /// Intersect a ray against this object.
        /// </summary>
        /// <param name="start">The start position of the ray, in object space.</param>
        /// <param name="dir">The direction of the ray, in object space (not a unit vector).</param>
        /// <returns>Information about the nearest intersection, or null if no intersection.</returns>
        /// <remarks>Thread safe</remarks>
        public IntersectionInfo IntersectRay(Vector start, Vector dir)
        {
            // trace ray through underlying geometry
            IntersectionInfo info = geometry.IntersectRay(start, dir);

            // if AO is disabled, pass-through the ray intersection
            if (!Enabled)
                return info;

            // did ray not hit any geometry?
            if (info == null)
                return null;

            if (ambientOcclusionCache == null)
            {
                // Create ambient occlusion cache, and optionally load cache data from disk

                // Extract the name of the model from the 3DS file name.
                // TODO: this is missing most/all of the time!
                //var modelFileName = model.GetFileName();
                //Contract.Assert(modelFileName != null && modelFileName.Length > 0);
                //var modelName = RemoveFileExtension(modelFileName);

                // TODO: power-of-two size/resolution might make 3D array indexing quicker
                ambientOcclusionCache = new AmbientOcclusion(resolution, instanceKey, cachePath);
            }

            // Should the AO cache be enabled, or pass-through?
            ambientOcclusionCache.EnableCache = EnableAoCache;

            // shade surface point based on nearby geometry blocking ambient light from reaching surface
            var lightIntensityByte = ambientOcclusionCache.CacheAmbientOcclusion(info, geometry);
            info.color = Modulate(info.color, lightIntensityByte);
            return info;
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
                return geometry.NumRayTests;
            }
        }

        private uint Modulate(uint color, byte amount)
        {
            byte r = (byte)(color >> 16);
            byte g = (byte)(color >> 8);
            byte b = (byte)color;
            r = (byte)((r * amount) >> 8);
            g = (byte)((g * amount) >> 8);
            b = (byte)((b * amount) >> 8);
            return (uint)((r << 16) + (g << 8) + b);
            //            return (uint)(((ulong)color * amount) >> 8);
        }
    }
}