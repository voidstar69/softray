//#define VISUALISATION

using System;
using System.Diagnostics.Contracts;
using System.Collections.Generic;
using System.Linq;

namespace Engine3D.Raytrace
{
    using Coord4D = Tuple<byte, byte, byte, byte>;
    using Float4D = Tuple<double, double, double, double>;

    /// <summary>
    /// A method for rendering images of 4D lightfields, and caching 4D lightfields from underlying raytracable geometry
    /// </summary>
    public class LightFieldTriMethod : IRayIntersectable, IDisposable
    {
        // Three different representations of the same geometry
        private readonly IRayIntersectable geometry;                // abstract interface to collection of triangles
        private readonly GeometryCollection geometry_simple;        // collection of triangles
        private readonly SpatialSubdivision geometry_subdivided;    // spatial subdivision of collection of triangles

        private readonly byte resolution;
        private readonly string instanceKey;
        private readonly string cachePath;
        private readonly Random random;

        // 4D light field cache, to cache results from all lighting calculations. This stores a triangle index per cache cell.
        // TODO: should this use locking like LightFieldColorMethod?
        private LightField4D<ushort> lightFieldCache;

        public bool Enabled { get; set; }

        /// <summary>
        /// Create structure to generate and store 4D lightfield data for a single 3D model (that fits within the unit cube).
        /// </summary>
        /// <param name="geometry">abstract interface to collection of triangles</param>
        /// <param name="triList">collection of triangles</param>
        /// <param name="subdividedTris">spatial subdivision of collection of triangles</param>
        /// <param name="resolution">The resolution of the 4D lightfield. Lightfield has size 2N x N x 2N x N (yaw covers 360 degrees; pitch covers 180 degrees).
        /// A power-of-two size might make indexing into the 4D array quicker.</param>
        /// <param name="instanceKey">A text value unique to the current 3D model.</param>
        public LightFieldTriMethod(IRayIntersectable geometry, GeometryCollection triList, SpatialSubdivision subdividedTris,
            byte resolution, string instanceKey, string cachePath, int randomSeed)
        {
            Contract.Requires(triList != null);
            Contract.Requires(subdividedTris != null);
            Contract.Requires(resolution > 0);
            Enabled = true;
            this.geometry = geometry;
            this.geometry_simple = triList;
            this.geometry_subdivided = subdividedTris;
            this.resolution = resolution;
            this.instanceKey = instanceKey;
            this.cachePath = cachePath;
            this.random = new Random(randomSeed);
        }

        public void EndRender()
        {
            // Free up any virtual memory used by any memory-mapped files
            // TODO: should this use locking like LightFieldColorMethod?
            if (lightFieldCache != null)
                lightFieldCache.FreeVirtualMemory();
        }

        public void Dispose()
        {
            if (lightFieldCache != null)
            {
                lightFieldCache.Dispose();
                lightFieldCache = null;
            }
        }

        /// <summary>
        /// Intersect a ray against this object.
        /// </summary>
        /// <param name="start">The start position of the ray, in object space.</param>
        /// <param name="dir">The direction of the ray, in object space (not a unit vector).</param>
        /// <returns>Information about the nearest intersection, or null if no intersection.</returns>
        public IntersectionInfo IntersectRay(Vector start, Vector dir, RenderContext context)
        {
            // if lightfield is disabled, pass-through the ray intersection
            if (!Enabled)
                return geometry.IntersectRay(start, dir, context);

            // lazily create the lightfield cache
            if (lightFieldCache == null)
                lightFieldCache = new LightField4D<ushort>(resolution, instanceKey, default(ushort), default(ushort) + 1, cachePath);

            // cache triangle indices in a 4D light field
            Coord4D lfCoord = null;
            Vector rayStart = start;
            Vector rayDir = dir;

            // Convert ray to 4D spherical coordinates
            // TODO: do we need locking to ensure another thread does not overwrite this lightfield cache entry?
            lfCoord = lightFieldCache.RayToCoord4D(ref rayStart, ref rayDir);
            if (lfCoord == null)
                return null;

            // TODO: debugging. Very slow! Also broken as of Aug 2016.
            //Vector tmpStart;
            //Vector tmpDir;
            //lightFieldCache.Coord4DToRay(lfCoord, out tmpStart, out tmpDir);
            //var tmpCoord = lightFieldCache.RayToCoord4D(ref tmpStart, ref tmpDir);
            //Contract.Assert(tmpCoord.Equals(lfCoord));

            // Index into light field cache using 4D spherical coordinates
            ushort lfCacheEntry = lightFieldCache.ReadCache(lfCoord.Item1, lfCoord.Item2, lfCoord.Item3, lfCoord.Item4);
            if (lfCacheEntry == lightFieldCache.EmptyCacheEntryReplacement)
                // cache entry indicates 'missing triangle', so nothing to intersect against
                return null;

            // if lightfield cache entry is empty, populate it with a triangle from the cell's beam
            int triIndex;
            if (lightFieldCache.EmptyCacheEntry == lfCacheEntry)
            {
                // lightfield does not yet contain a triangle entry to intersect against
                // TODO: once the light field cache 'fills up', do we cease tracing rays? Prove/measure this!

                // Intersect random rays along beam of lightfield cell, against all triangles
                // TODO: this *should* improve quality, but seems to decrease it, and adds a little randomness to the regression tests
                //triIndex = BeamTriangleComplexIntersect(lfCoord, context);

                // Intersect central axis of lightfield cell against all triangles
                triIndex = BeamTriangleSimpleIntersect(lfCoord, context);

                if (triIndex == -1)
                {
                    // Cache 'missing triangle' value into the light field cache
                    lightFieldCache.WriteCache(lfCoord.Item1, lfCoord.Item2, lfCoord.Item3, lfCoord.Item4, lightFieldCache.EmptyCacheEntryReplacement);
                    return null;
                }
                else
                {
                    // Take triangle that we intersected, and store its index into the light field cache
                    // TODO: triIndex could overflow a ushort and wrap around to an incorrect triangle index!
                    lfCacheEntry = (ushort)(triIndex + 2); // account for empty cache value and replacement cache value (i.e. avoid storing 0 or 1 into lightfield)
                    lightFieldCache.WriteCache(lfCoord.Item1, lfCoord.Item2, lfCoord.Item3, lfCoord.Item4, lfCacheEntry);
                }
            }

            Contract.Assert(lightFieldCache.EmptyCacheEntry != lfCacheEntry);

            // lightfield contains a triangle entry to intersect against

            // store triangle indices in light field, and raytrace the triangle that we get from the lightfield

            // TODO: Store index of bucket of triangles into lightfield? Tradeoff between small buckets and many buckets. Optimise for 8-bit or 16-bit bucket indices?

            // Intersect primary ray against closest/likely/best triangle (from lightfield cache)
            triIndex = (int)lfCacheEntry - 2; // discount empty and replacement cache values
            // TODO: Contracts analyser says assert unproven
            Contract.Assert(triIndex >= 0);
            var tri = geometry_simple[triIndex] as Raytrace.Triangle;
            IntersectionInfo intersection = tri.IntersectRay(rayStart, rayDir, context); // intersect ray against triangle

            // intersect ray against plane of triangle, to avoid holes by covering full extent of lightfield cell. Creates spatial tearing around triangle silhouettes.
            //info = tri.Plane.IntersectRay(rayStart, rayDir);
            //info.color = tri.Color; // all planes are white, so use color of triangle

            if (intersection == null)
            {
                // Intersect ray against triangles 'near' to the triangle from the lightfield (i.e. in the same subdivision node).
                // This is slower than intersecting the single triangle from the lightfield, but much faster than intersecting the entire subdivision structure.
                // TODO: need to walk the subdivision tree to find triangles in nearby tree nodes
                intersection = geometry_subdivided.IntersectRayWithLeafNode(rayStart, rayDir, tri, context);

#if VISUALISATION
                if (intersection != null)
                    return new IntersectionInfo { color = 0x000000FF, normal = new Vector(0, 1, 0) }; // visualise hitting nearby triangle (blue)
#endif
            }

            if (intersection == null)
            {
                // Intersect ray against all triangles in the geometry.
                // TODO: this is much slower than intersecting the single triangle from the lightfield, or the triangles in the same node.
                // Performance will be reasonable if not many pixels reach this code path.
                intersection = geometry_subdivided.IntersectRay(rayStart, rayDir, context);

#if VISUALISATION
                if (intersection != null)
                    return new IntersectionInfo { color = 0x00FF0000, normal = new Vector(0, 1, 0) }; // visualise hitting triangle in distant node (red)
#endif
            }

            //if (info == null)
            //{
            //    // intersect ray against plane of triangle, to avoid holes by covering full extent of lightfield cell
            //    // TODO: this creates spatial tearing around triangle silhouettes, as all background pixels in this cell are covered by this plane.
            //    info = tri.Plane.IntersectRay(rayStart, rayDir);
            //    if (info != null)
            //        info.color = tri.Color; // all planes are white, so use color of triangle
            //}

#if VISUALISATION
            if (intersection == null)
                return new IntersectionInfo { color = 0x0000FF00, normal = new Vector(0, 1, 0) }; // visualise missing nearby triangles (green)
#endif

            return intersection;
        }

        // Returns index of triangle intersected by central axis of lightfield cell's beam
        // Returns -1 if no triangle is intersected
        private int BeamTriangleSimpleIntersect(Coord4D lfCoord, RenderContext context)
        {
            // switch to tracing the canonical ray associated with this lightfield entry, instead of the original ray (to avoid biasing values in lightfield)
            Vector cellRayStart;
            Vector cellRayDir;
            lightFieldCache.Coord4DToRay(lfCoord, out cellRayStart, out cellRayDir);

            // TODO: once the light field cache 'fills up', do we cease tracing rays? Prove/measure this!

            // trace a primary ray against all triangles in scene
            // TODO: trace multiple rays to find the triangle with largest cross-section in the beam corresponding to this lightfield cell?
            // This may also avoid incorrectly storing 'missing triangle' value when primary ray misses because there are no triangles along central axis of cell's beam.
            IntersectionInfo intersection = geometry_subdivided.IntersectRay(cellRayStart, cellRayDir, context);
            if (intersection == null)
            {
                return -1;
            }
            else
            {
                Contract.Assert(intersection.triIndex >= 0);
                return intersection.triIndex;
            }
        }

        // Returns index of triangle most frequently intersected by random rays along lightfield cell's beam
        // Returns -1 if no triangle is intersected
        private int BeamTriangleComplexIntersect(Coord4D lfCoord, RenderContext context)
        {
            var triCount = new Dictionary<int, short>();
            for (int i = 0; i < 100; i++)
            {
                const double bias = +0.5;
                Float4D randCoord = new Float4D(
                    lfCoord.Item1 + random.NextDouble() + bias,
                    lfCoord.Item2 + random.NextDouble() + bias,
                    lfCoord.Item3 + random.NextDouble() + bias,
                    lfCoord.Item4 + random.NextDouble() + bias);

                // switch to tracing the canonical ray associated with this lightfield entry, instead of the original ray (to avoid biasing values in lightfield)
                Vector cellRayStart;
                Vector cellRayDir;
                lightFieldCache.Float4DToRay(randCoord, out cellRayStart, out cellRayDir);

                // TODO: once the light field cache 'fills up', do we cease tracing rays? Prove/measure this!

                // trace a primary ray against all triangles in scene
                // TODO: trace multiple rays to find the triangle with largest cross-section in the beam corresponding to this lightfield cell?
                // This may also avoid incorrectly storing 'missing triangle' value when primary ray misses because there are no triangles along central axis of cell's beam.
                IntersectionInfo intersection = geometry_subdivided.IntersectRay(cellRayStart, cellRayDir, context);
                if (null != intersection)
                {
                    var triIndex = intersection.triIndex;
                    Contract.Assert(triIndex >= 0);
                    if(triCount.ContainsKey(triIndex))
                        triCount[triIndex]++;
                    else
                        triCount[triIndex] = 1;
                }
            }

            if (triCount.Count == 0)
                return -1;
            else
            {
                // find tri index that occurs most often
                var maxCount = triCount.Max(x => x.Value);
                return triCount.First(x => x.Value == maxCount).Key;
            }
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
                return geometry_subdivided.NumRayTests;
            }
        }
    }
}