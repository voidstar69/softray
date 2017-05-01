using System;
using System.Diagnostics.Contracts;

// TODO: appears to not be thread-safe - path tracing with 8xAA causes banding!

namespace Engine3D.Raytrace
{
    public class PathTracingMethod : IRayIntersectable
    {
        public double raySurfaceOffset = 0.001;  // prevents self-shadowing of surface

        // The underlying geometry to raytrace
        private readonly IRayIntersectable geometry;

        // Random number generator used for picking new ray directions
        private readonly Random random;

        public bool Enabled { get; set; }

        public PathTracingMethod(IRayIntersectable geometry, Scene scene, Instance instance, int randomSeed)
        {
            Enabled = true;
            this.geometry = geometry;
            random = new Random(randomSeed);
        }

        /// <summary>
        /// Intersect a ray against this object.
        /// </summary>
        /// <param name="start">The start position of the ray, in object space.</param>
        /// <param name="dir">The direction of the ray, in object space (not a unit vector).</param>
        /// <returns>Information about the nearest intersection, or null if no intersection.</returns>
        public IntersectionInfo IntersectRay(Vector start, Vector dir)
        {
            Contract.Ensures(Contract.Result<IntersectionInfo>() == null || (Contract.Result<IntersectionInfo>().color & 0xff000000) == 0xff000000);

            // trace ray through underlying geometry
            IntersectionInfo info = geometry.IntersectRay(start, dir);

            // if we are disabled, pass-through the ray intersection
            if (!Enabled)
                return info;

            // did ray not hit any geometry?
            if (info == null)
                return null;

            // shade the surface point
            Vector surfaceNormal = info.normal;
            Vector newRayStart = info.pos + surfaceNormal * raySurfaceOffset;
            Vector newRayDir = RandomRayInHemisphere(surfaceNormal, random);
            newRayDir.Normalise(); // only needed for calling BRDF function

            // Fire off ray to check for another surface in the chosen direction
            Raytrace.IntersectionInfo newRayInfo = geometry.IntersectRay(newRayStart, newRayDir);
            // Did this ray did hit another surface?
            Color incomingLight = Color.Black; // TODO: use background color from Renderer
            if (newRayInfo != null)
                incomingLight = new Color(newRayInfo.color);

            // TODO: color conversions are probably very slow
            Color surfaceEmission = new Color(info.color); // TODO: info.color might be shaded surface color! We want the raw material color here!
            double reflectedLightFrac = BRDF(newRayDir, surfaceNormal /*, dir */);
            Color outgoingLight = incomingLight * reflectedLightFrac + surfaceEmission;

            // only normalise colour if R, G or B are greater than 1.0
            if (outgoingLight.r > 1.0 || outgoingLight.g > 1.0 || outgoingLight.b > 1.0)
                outgoingLight.Normalise();

            Contract.Assert(0.0 <= outgoingLight.r && outgoingLight.r <= 1.0);
            Contract.Assert(0.0 <= outgoingLight.g && outgoingLight.g <= 1.0);
            Contract.Assert(0.0 <= outgoingLight.b && outgoingLight.b <= 1.0);
            info.color = outgoingLight.ToARGB();

            return info;
        }

        // Bidirectional Reflectabce Distribution Function (see Wikipedia).
        // This BRDF implements a simple Lambertian diffuse surface (i.e. Cosine weighted)
        private static double BRDF(Vector incomingLightDirReverse, Vector surfaceNormal /*, Vector outgoingLightDirReverse */)
        {
            Contract.Requires(incomingLightDirReverse.IsUnitVector);
            Contract.Requires(surfaceNormal.IsUnitVector);
//            Contract.Requires(outgoingLightDirReverse.IsUnitVector);
            return surfaceNormal.DotProduct(incomingLightDirReverse); // *2.0;
        }

        private static Vector RandomRayInHemisphere(Vector normal, Random random)
        {
            // Pick a random direction within the hemisphere around the surface normal
            // TODO: works for external surfaces, but we see some self-intersection on interior surfaces

//            return normal;

            var rayDir = new Vector(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1);
            if (rayDir.DotProduct(normal) < 0.0)
                rayDir = -rayDir;
            return rayDir;

/*
            // Pick a random direction in a smaller angle than the 180 degree hemisphere
            while (true)
            {
                var rayDir = new Vector(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1);
                if (rayDir.DotProduct(normal) < 0.5)
                    return rayDir;
            }
 */ 
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