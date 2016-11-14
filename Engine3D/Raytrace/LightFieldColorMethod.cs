using System;
using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    using Coord4D = Tuple<byte, byte, byte, byte>;
    using Float4D = Tuple<double, double, double, double>;

    /// <summary>
    /// A method for rendering images of 4D lightfields, and caching 4D lightfields from underlying raytracable geometry
    /// </summary>
    public class LightFieldColorMethod : IRayIntersectable, IDisposable
    {
        // The underlying geometry to render on-demand into the 4D lightfield cache
        private readonly IRayIntersectable geometry;

        private readonly byte resolution;
        private readonly string instanceKey;
        private readonly string cachePath;

        // TODO: we store background color in lightfield. Might be better to store sentinel value, detect this, and return a 'missed ray'. This would allow for any type of background.
        private readonly uint backgroundColor;

        // Light field cache, to cache final light color for every incoming ray
        private LightField4D<uint> lightFieldCache;
        private readonly object cacheLock = new object(); // only used for locking major operations, i.e. creation and freeing.

        // Resolution / limit for each dimension of lightfield
        private int uRes;
        private int vRes;
        private int sRes;
        private int tRes;

        public bool Enabled { get; set; }

        // quad-linear interpolation
        public bool Interpolate { get; set; }

        /// <summary>
        /// Create structure to generate and store 4D lightfield data for a single 3D model (that fits within the unit cube).
        /// </summary>
        /// <param name="resolution">The resolution of the 4D lightfield. Lightfield has size 2N x N x 2N x N (yaw covers 360 degrees; pitch covers 180 degrees).
        /// A power-of-two size might make indexing into the 4D array quicker.</param>
        /// <param name="instanceKey">A text value unique to the current 3D model.</param>
        /// <param name="backgroundColor"></param>
        public LightFieldColorMethod(IRayIntersectable geometry, byte resolution, string instanceKey, uint backgroundColor, string cachePath)
        {
            Contract.Requires(resolution > 0);
            Enabled = true;
            Interpolate = false; // TODO: was true
            this.geometry = geometry;
            this.backgroundColor = backgroundColor;
            this.resolution = resolution;
            this.instanceKey = instanceKey;
            this.cachePath = cachePath;
        }

        public void EndRender()
        {
            // Free up any virtual memory used by any memory-mapped files
            lock (cacheLock)
            {
                if (lightFieldCache != null)
                {
                    lightFieldCache.FreeVirtualMemory();
                }
            }
        }

        public void Dispose()
        {
            lock (cacheLock)
            {
                if (lightFieldCache != null)
                {
                    lightFieldCache.Dispose();
                    lightFieldCache = null;
                }
            }
        }

        // TODO: store depth-fraction-within-sphere into lightfield alpha channel, to be able to return an intersection point/rayfrac? This would allow shadow rays to use lightfield!

        // TODO: is this entire method thread safe?
        /// <summary>
        /// Intersect a ray against this object.
        /// </summary>
        /// <param name="start">The start position of the ray, in object space.</param>
        /// <param name="dir">The direction of the ray, in object space (not a unit vector).</param>
        /// <returns>Information about the nearest intersection, or null if no intersection.</returns>
        /// <remarks>Construction of underlying <typeparamref name="LightField4D"/> is thread-safe</remarks>
        public IntersectionInfo IntersectRay(Vector start, Vector dir)
        {
            Contract.Ensures(!Enabled || Contract.Result<IntersectionInfo>() != null);

            // if lightfield is disabled, pass-through the ray intersection
            if (!Enabled)
                return geometry.IntersectRay(start, dir);

            // double-check locking pattern
            if (lightFieldCache == null)
            {
                lock (cacheLock)
                {
                    // another thread may have got the lock first, and already constructed the cache
                    if (lightFieldCache == null)
                    {
                        this.lightFieldCache = new LightField4D<uint>(resolution, instanceKey, default(uint), default(uint) + 1, cachePath);
                        this.uRes = lightFieldCache.cacheRes * 2;
                        this.vRes = lightFieldCache.cacheRes;
                        this.sRes = uRes;
                        this.tRes = vRes;
                    }
                }
            }

            // TODO: if ray start is within lightfield sphere, throw an exception? Might occur for shadow/AO rays.

            uint finalColor = CalcColorForRay(start, dir);
            return new IntersectionInfo{ color = finalColor, normal = Vector.Forward };
        }

        // TODO: is this method multi-thread safe?
        // cache ray colors in a 4D light field
        private uint CalcColorForRay(Vector rayStart, Vector rayDir)
        {
            // TODO: do we need locking to ensure another thread does not overwrite lightfield cache entry(s)?

            if (!Interpolate)
            {
                // no interpolation
                Coord4D lfCoord = null;

                // Convert ray to 4D spherical coordinates
                // TODO: do we need locking to ensure another thread does not overwrite this lightfield cache entry?
                lfCoord = lightFieldCache.RayToCoord4D(ref rayStart, ref rayDir);
                if (lfCoord == null)
                    return backgroundColor;

                return CalcColorForCoord(lfCoord);
            }
            else
            {
                // quad-linear interpolation

                // Convert ray to 4D spherical coordinates
                Float4D lfFloat4D = lightFieldCache.RayToFloat4D(ref rayStart, ref rayDir);
                if (lfFloat4D == null)
                    return backgroundColor;

                // this linearly interpolates lightfield colours along all four axes
                Coord4D coord = new Coord4D((byte)lfFloat4D.Item1, (byte)lfFloat4D.Item2, (byte)lfFloat4D.Item3, (byte)lfFloat4D.Item4);
                double uFrac = lfFloat4D.Item1 - coord.Item1;
                double vFrac = lfFloat4D.Item2 - coord.Item2;
                double sFrac = lfFloat4D.Item3 - coord.Item3;
                double tFrac = lfFloat4D.Item4 - coord.Item4;
                Color finalColor = new Color();
                for (byte u = 0; u <= 1; u++)
                {
                    var uFactor = (u == 1 ? uFrac : 1 - uFrac);
                    for (byte v = 0; v <= 1; v++)
                    {
                        var vFactor = (v == 1 ? vFrac : 1 - vFrac);
                        for (byte s = 0; s <= 1; s++)
                        {
                            var sFactor = (s == 1 ? sFrac : 1 - sFrac);
                            for (byte t = 0; t <= 1; t++)
                            {
                                Coord4D newCoord = new Coord4D(
                                    (byte)((coord.Item1 + u) % uRes),
                                    (byte)((coord.Item2 + v) % vRes),
                                    (byte)((coord.Item3 + s) % sRes),
                                    (byte)((coord.Item4 + t) % tRes));
                                var color = new Color(CalcColorForCoord(newCoord));
                                finalColor += color * uFactor * vFactor * sFactor * (t == 1 ? tFrac : 1 - tFrac);
                            }
                        }
                    }
                }
                return finalColor.ToARGB();
            }
        }

        private uint CalcColorForCoord(Coord4D lfCoord)
        {
            // Index into light field cache using 4D spherical coordinates
            uint lfCacheEntry = lightFieldCache.ReadCache(lfCoord.Item1, lfCoord.Item2, lfCoord.Item3, lfCoord.Item4);
            if (lfCacheEntry != lightFieldCache.EmptyCacheEntry)
            {
                // lightfield stores colors, and we found a color entry to return
                return lfCacheEntry;
            }

            // switch to tracing the ray associated with this lightfield entry, instead of the original ray (to avoid biasing values in lightfield)
            Vector rayStart;
            Vector rayDir;
            lightFieldCache.Coord4DToRay(lfCoord, out rayStart, out rayDir);

            // TODO: once the light field cache 'fills up', do we cease tracing rays? Prove/measure this!

            // we did not find a color in the light field corresponding to this ray, so trace this ray against the geometry
            // TODO: this line is the major performance bottleneck!
            IntersectionInfo info = geometry.IntersectRay(rayStart, rayDir);
            if (info == null)
            {
                // ray did not hit the geometry, so cache background color into the 4D light field
                lightFieldCache.WriteCache(lfCoord.Item1, lfCoord.Item2, lfCoord.Item3, lfCoord.Item4, backgroundColor);
                return backgroundColor;
            }

            // TODO: this is where lots of other AO, shadow code etc used to execute

            // cache ray colors in a 4D light field?
            lightFieldCache.WriteCache(lfCoord.Item1, lfCoord.Item2, lfCoord.Item3, lfCoord.Item4, info.color);

            return info.color;
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
    }
}