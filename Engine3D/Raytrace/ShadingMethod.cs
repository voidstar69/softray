using System;
using System.Diagnostics.Contracts;

namespace Engine3D.Raytrace
{
    public class ShadingMethod : IRayIntersectable
    {
        // The underlying geometry to raytrace
        private readonly IRayIntersectable geometry;

        // Scene lighting parameters
        private readonly Scene scene;

        // Only used for fudging about with view space / object space. Should not be required.
        // TODO: this assumes only one instance globally!
        private readonly Instance instance;

        public readonly Color defaultMaterialColor = new Color(1, 1, 1);

        public bool Enabled { get; set; }

        public ShadingMethod(IRayIntersectable geometry, Scene scene, Instance instance)
        {
            Enabled = true;
            this.geometry = geometry;
            this.scene = scene;
            this.instance = instance;
        }

        /// <summary>
        /// Intersect a ray against this object.
        /// </summary>
        /// <param name="start">The start position of the ray, in object space.</param>
        /// <param name="dir">The direction of the ray, in object space (not a unit vector).</param>
        /// <returns>Information about the nearest intersection, or null if no intersection.</returns>
        public IntersectionInfo IntersectRay(Vector start, Vector dir)
        {
            // trace ray through underlying geometry
            IntersectionInfo info = geometry.IntersectRay(start, dir);

            // if shading is disabled, pass-through the ray intersection
            if (!Enabled)
                return info;

            // did ray not hit any geometry?
            if (info == null)
                return null;

            // shade the surface point based on angle of lighting
            //double intensity = CalcLightingIntensity(info.pos, info.normal); // in object space
            // TODO: All lighting calcs are broken for raytracing.
            Vector pos_View = instance.TransformPosToView(info.pos);
            Vector normal_View = instance.TransformDirection(info.normal);
            double intensity = CalcLightingIntensity(pos_View, normal_View);

            byte lightIntensityByte = 255; // TODO: change this to a double in range [0, 1] so that conversion to byte occurs only once?
            lightIntensityByte = (byte)(lightIntensityByte * intensity);

            //else
            //{
            //    return Surface.PackRgb(lightIntensityByte, lightIntensityByte, lightIntensityByte);
            //}

            //color = Surface.ColorFromArgb(0, intensityByte, intensityByte, intensityByte);

            info.color = Color.ModulatePackedColor(info.color, lightIntensityByte);
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

        // TODO: optimise this method
        /// <summary>
        /// Calculate lighting at a point on a surface.
        /// </summary>
        /// <param name="pos">The position of the point on the surface (in view space).</param>
        /// <param name="normal">The normal to the surface at the point (in view space). This must be a unit vector.</param>
        /// <returns>The intensity of the light at this point on the surface, as numbers between 0 and 1.</returns>
        private double CalcLightingIntensity(Vector point, Vector normal)
        {
            // TODO: calculating with colors then converting to intensity is inefficient. Optimise intensity calc?
            // TODO: use model's per-triangle material color
            Color color = CalcLighting(point, normal, defaultMaterialColor, defaultMaterialColor, defaultMaterialColor, scene.specularLight_shininess);
            double intensity = Math.Max(Math.Max(color.r, color.g), color.b);

            // Ensure that lighting intensity is within range.
            Assert.IsTrue(intensity >= 0.0 && intensity <= 1.0, "intensity out of range: {0}", intensity);

            return intensity;
        }

        // TODO: optimise this method
        /// <summary>
        /// Calculate lighting at a point on a surface.
        /// </summary>
        /// <param name="pos">The position of the point on the surface (in view space).</param>
        /// <param name="normal">The normal to the surface at the point (in view space). This must be a unit vector.</param>
        /// <returns>The color of the light at this point on the surface, as numbers between 0 and 1.</returns>
        private Color CalcLighting(Vector point, Vector normal, Color ambientMaterial, Color diffuseMaterial,
                                   Color specularMaterial, double specularMaterialExponent)
        {
            Assert.IsTrue(normal.IsUnitVector, "normal is not a unit vector. Length is {0:G}, error is {1:G}", normal.Length, Math.Abs(normal.Length - 1.0));
            //          Assert.IsTrue(normal.Length - 1.0 > 1e-10, "foobar");
            //          Assert.IsTrue(false, "normal:({0})", normal);

            // Calculate direction from the surface point to the light.
            Vector dirToLight;
            if (scene.pointLighting)
            {
                dirToLight = scene.positionalLightPos_View - point;
                // TODO: instead of normalising here, later on divide dot product by length of this vector?
                dirToLight.Normalise();
            }
            else
            {
                dirToLight = -scene.directionalLightDir_View;
            }

            Assert.IsTrue(dirToLight.IsUnitVector, "dirToLight is not a unit vector. Length is {0:G}, error is {1:G}",
                dirToLight.Length, Math.Abs(dirToLight.Length - 1.0));
            //          if (dirToLight.Length - 1.0 > 1e-10)

            // Calculate diffuse component of lighting.
            //            Assert.IsTrue(false, "dirToLight:({0}) normal:({1})", dirToLight, normal);
            double diffuseIntensity = dirToLight.DotProduct(normal); // [-1, +1]
            diffuseIntensity = Math.Max(0.0, diffuseIntensity); // [0, +1]

            // Calculate specular component of lighting.
            double specularIntensity = 0.0;
            if (scene.specularLighting)
            {
                Vector dirToCamera = /* scene.cameraPos */ - point; // in view space, camera is at origin
                dirToCamera.Normalise();
                //                Vector reflectedLightDir = (normal - dirToLight) + normal;
                Vector reflectedLightDir = 2.0 * dirToLight.DotProduct(normal) * normal - dirToLight;

                Assert.IsTrue(reflectedLightDir.IsUnitVector, "reflectedLightDir is not a unit vector. Length is {0:G}, error is {1:G}",
                    reflectedLightDir.Length, Math.Abs(reflectedLightDir.Length - 1.0));
                //              if (reflectedLightDir.Length - 1.0 > 1e-10)

                double cosOfAngle = reflectedLightDir.DotProduct(dirToCamera); // [-1, +1]
                specularIntensity = Math.Pow(cosOfAngle, specularMaterialExponent /*specularLight_shininess*/); // [-?, +?]
                specularIntensity = Math.Max(0.0, specularIntensity); // [0, +?]
            }

            // Merge ambient, diffuse and specular contributions.
            // TODO: this assumes unit-brightness white-light. Add light color/intensity?
            Color color = ambientMaterial * scene.ambientLight_intensity +
                          diffuseMaterial * diffuseIntensity +
                          specularMaterial * specularIntensity;

            // TODO: this assumes that all materials are white.
            //double intensity = ambientLight_intensity + diffuseIntensity + specularIntensity;
            //Color color = new Color(intensity, intensity, intensity);

            color.r = Math.Min(color.r, 1.0);
            color.g = Math.Min(color.g, 1.0);
            color.b = Math.Min(color.b, 1.0);

            // Ensure that color components are within range.
            Assert.IsTrue(color.r >= 0.0 && color.r <= 1.0, "color.r out of range: {0}", color.r);
            Assert.IsTrue(color.g >= 0.0 && color.g <= 1.0, "color.g out of range: {0}", color.g);
            Assert.IsTrue(color.b >= 0.0 && color.b <= 1.0, "color.b out of range: {0}", color.b);

            return color;
        }
    }
}