using System;
using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    public class ShadowMethod : IRayIntersectable
    {
        // TODO: 400x400 image test at quality 10 took 12s per image; quality 100 took 1 min per image.
        private const int softShadowQuality = 100; // affects performance linearly
        private const double shadowProbeOffset = 0.001; // prevents self-shadowing of surface

        // The underlying geometry to raytrace
        private readonly IRayIntersectable geometry;

        // Scene lighting parameters
        private readonly Scene scene;

        // Cache for static soft shadows, to calculate and store results from soft shadow calculations.
        // Will be null if shadows are dynamic instead of static.
        private readonly Texture3DCache<byte> softShadowCache;

        // Random offsets from light position, used to control the range of area lighting for soft shadows
        private readonly Vector[] areaLightOffsets;

        // this is thread safe, but still a hack
        [ThreadStatic] private static Vector currSurfaceNormal;

        public bool Enabled { get; set; }

        public bool CacheStaticShadowsToFile
        {
            get
            {
                if (null != softShadowCache)
                    return softShadowCache.EnableFileCache;
                else
                    return false;
            }
            set
            {
                if (null != softShadowCache)
                    softShadowCache.EnableFileCache = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="geometry"></param>
        /// <param name="staticShadows"></param>
        /// <param name="resolution">The resolution of the 3D texture for static shadows.
        /// A power-of-two size might make indexing into the 3D texture quicker.</param>
        /// <param name="instanceKey">A text value unique to the current 3D model.</param>
        /// <remarks>Not thread safe</remarks>
        public ShadowMethod(IRayIntersectable geometry, Scene scene, bool staticShadows, byte resolution, string instanceKey, int randomSeed)
        {
            Contract.Requires(resolution > 0);
            Enabled = true;
            this.geometry = geometry;
            this.scene = scene;

            // generate random offsets to area light, for soft shadows
            var random = new Random(randomSeed);
            areaLightOffsets = new Vector[softShadowQuality];
            for (int i = 0; i < softShadowQuality; i++)
            {
                // generate points on the surface of a sphere, representing the area light source
                var offset = new Vector(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1);
                offset.Normalise();
                offset *= 0.2;
                areaLightOffsets[i] = offset;
            }

            if (staticShadows)
            {
                // Create static soft shadow cache, and optionally load from disk.
                // TODO: power-of-two size/resolution might make 3D array indexing quicker
                // TODO: does this capture a fixed or varying currSurfaceNormal? If fixed, this could cause the cross-image-test pollution!
                softShadowCache = new Raytrace.Texture3DCache<byte>(resolution, instanceKey, default(byte), default(byte) + 1,
                    (pos) => { return (byte)(TraceRaysForSoftShadows(pos, currSurfaceNormal, geometry) * 254 + 1); }
                );
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

            // if shadowing is disabled, pass-through the ray intersection
            if (!Enabled)
                return info;

            // did ray not hit any geometry?
            if (info == null)
                return null;

            byte lightIntensityByte = 255;
            if (softShadowCache != null)
            {
                // Static soft shadows, cached in a 3D texture
                currSurfaceNormal = info.normal; // a hack to pass this value to TraceRaysForSoftShadows
                lightIntensityByte = softShadowCache.Sample(info.pos);
            }
            else
            {
                // Dynamic soft shadows, not cached
                lightIntensityByte = (byte)(TraceRaysForSoftShadows(info.pos, info.normal, geometry) * 255);
            }

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

        /// <summary>
        /// Calculates soft shadows at a point on a surface.
        /// Multithread safe?
        /// </summary>
        /// <param name="surfacePos">Position of surface point to be shadowed</param>
        /// <param name="surfaceNormal">Normal to surface at surface point</param>
        /// <param name="geometry">The geometry to be raytraced (both shadow caster and shadow receiver)</param>
        /// <returns>Fraction of light reaching the surface point (between 0 and 1): 0 = surface point fully shadowed; 1 = surface point not shadowed at all</returns>
        private double TraceRaysForSoftShadows(Vector surfacePos, Vector surfaceNormal, Raytrace.IRayIntersectable geometry)
        {
            int rayEscapeCount = 0;
            for (int i = 0; i < softShadowQuality; i++)
            {
                // Check for shadow. Calculate direction and distance from light to surface point.
                Vector dirLightToSurface;
                Vector shadowRayStart;
                Vector shadowRayEnd = surfacePos + surfaceNormal * shadowProbeOffset;
                if (scene.pointLighting)
                {
                    // generate points on the surface of a unit sphere (representing the light source)
                    var lightSource = scene.positionalLightPos_Model + areaLightOffsets[i];
                    dirLightToSurface = shadowRayEnd - lightSource;
                    shadowRayStart = lightSource;
                }
                else
                {
                    // Directional lighting.
                    // TODO: soft shadows might make no sense for a directional light!
                    dirLightToSurface = scene.directionalLightDir_Model;
                    shadowRayStart = shadowRayEnd + dirLightToSurface * 1000.0 + areaLightOffsets[i];
                }

                // Fire off shadow ray through underlying geometry
                IntersectionInfo shadowInfo = geometry.IntersectRay(shadowRayStart, dirLightToSurface);

                // Did the shadow ray hit an occluder before reaching the light?
                if (shadowInfo == null || shadowInfo.rayFrac > 1.0)
                {
                    // No, so this light ray reaches the surface point
                    rayEscapeCount++;
                }
            }

            return (double)rayEscapeCount / (double)softShadowQuality;
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