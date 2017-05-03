using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Color = Engine3D.Color;
using GeometryCollection = Engine3D.Raytrace.GeometryCollection;
using Renderer = Engine3D.Renderer;
using Sphere = Engine3D.Raytrace.Sphere;
using Vector = Engine3D.Vector;

namespace Engine3D_Tests
{
    [TestClass]
    public class RendererTests
    {
        private const int maxImageWidth = 100;
        private const int maxImageHeight = 100;

        // Faster regression test
        private const int defaultResolution = 100;
        //private const int imageWidth = 100;
        //private const int imageHeight = 100;

        // Slower regression test
        //private const int defaultResolution = 400;
        //private const int imageWidth = 400;
        //private const int imageHeight = 400;

        private const string bitmapFileExtension = ".bmp";
        private readonly ImageFormat bitmapFormat = ImageFormat.Bmp;
        //private const string bitmapFileExtension = ".jpg";
        //private readonly ImageFormat bitmapFormat = ImageFormat.Jpeg;
        // TODO: rendered image has transparent alpha channel, so PNG files always end up fully transparent!
        //private const string bitmapFileExtension = ".png";
        //private readonly ImageFormat bitmapFormat = ImageFormat.Png;

        private string modelFileName = "../../obj.3ds";
        private double objectDepth = 1.0; // depth of object (in view space)

        //private const double yawDegrees = 45.0;
        //private const double pitchDegrees = 45.0;
        //private const double rollDegrees = 45.0;

        private const double defaultYawDegrees = 135.0;
        private const double defaultPitchDegrees = -22.0;
        private const double defaultRollDegrees = 0.0;

        //Vector initAngles = new Vector(-Math.PI / 8.0, 0.0, 0.0)

        private const string candidatePath = "../../candidate images/";
        private const string baselinePath = "../../baseline images/";

        private int numMissingBaselines = 0;

        private static int[] pixels = new int[maxImageWidth * maxImageHeight];

        [TestInitialize]
        public void Init()
        {
        }

        private static void RendererSetup(Renderer renderer, string modelFileName,
            double pitchDegrees, double yawDegrees, double rollDegrees, double objectDepth, int resolution)
        {
            renderer.BackgroundColor = 0xff00ff; // pink
            //renderer.BackgroundColor = 0xff0000; // red
            //renderer.BackgroundColor = 0x0000ff; // blue

            renderer.SetRenderingSurface(resolution, resolution, pixels);

            // Load 3D model from disk
            using (Stream stream = new FileStream(modelFileName, FileMode.Open, FileAccess.Read))
            {
                renderer.Load3dsModelFromStream(stream);
            }

            // Create an instance of this model in the scene.
            renderer.Instances.Add(new Engine3D.Instance(renderer.Model)
            {
                Position = new Engine3D.Vector(0.0, 0.0, objectDepth), // position of object (in view space)
                Yaw = yawDegrees / 180.0 * Math.PI,
                Pitch = pitchDegrees / 180.0 * Math.PI,
                Roll = rollDegrees / 180.0 * Math.PI
            });

            //renderer.directionalLight_dir = new Engine3D.Vector(1.0, 1.0, 1.0);
            //renderer.directionalLight_dir.Normalise();
            //renderer.positionalLight_pos = renderer.Instances[0].Position - renderer.directionalLight_dir * 2;
            //renderer.specularLight_shininess = 100.0;

            // Prevent Assert failure from displaying UI
            //Debug.Listeners.Clear();
        }

        private void MakeBooleanPermutation(int num, bool[] flags)
        {
            for(int i = 0; i < flags.Length; i++)
            {
                flags[i] = (num % 2 == 1);
                num /= 2;
            }
        }

        // Release mode: 6 mins @ res 100
        // Release mode: (5 flags) 54 seconds @ res 400
        // TODO: very, very slow! Make this much faster!
        [TestMethod]
        public void RaytraceTest()
        {
            var flags = new bool[6];
            var numPermutations = 1 << flags.Length;
            for (int i = 0; i < numPermutations; i++)
            {
                MakeBooleanPermutation(i, flags);

                // TODO: add AO flag
                var shading = flags[0];
                var shadows = flags[1];
                var lfColors = flags[2];
                var focalBlur = flags[3];
                var subPixelRes = (focalBlur ? (flags[4] ? 4 : 2) : (flags[4] ? 4 : 1)); // focal-blur or anti-aliasing sub-pixel resolution
                var staticShadows = flags[5];

                // TODO: add flag for quad-filtering on color lightfield
                // TODO: add flag for path tracing?
                RaytraceScenario(shading: shading, focalBlur: focalBlur, shadows: shadows, staticShadows: staticShadows,
                    lightField: lfColors, lightFieldWithTris: !lfColors, subPixelRes: subPixelRes);
            }

            if (numMissingBaselines > 0)
                Assert.Fail("{0} missing baseline images were recreated", numMissingBaselines);
        }

        // Release mode: 9 seconds @ res 100
        // Debug mode with contracts: 23 seconds @ res 100
        [TestMethod]
        public void RaytraceAntialised()
        {
            // TODO: compare performance to using Renderer.antiAliasResolution (result should be exactly the same, as long as focal blur not enabled).
            RaytraceScenario(subPixelRes: 2);
            RaytraceScenario(subPixelRes: 4);
            RaytraceScenario(subPixelRes: 8);

            if (numMissingBaselines > 0)
                Assert.Fail("{0} missing baseline images were recreated", numMissingBaselines);
        }

        // Release mode: 8 seconds @ res 100
        // Debug mode with contracts: 26 seconds @ res 100
        [TestMethod]
        public void RaytraceDynamicShadow()
        {
            RaytraceScenario(shadows: true);

            if (numMissingBaselines > 0)
                Assert.Fail("{0} missing baseline images were recreated", numMissingBaselines);
        }

        // Release mode: 15 sec @ res 100
        // Debug mode with contracts: >55 sec @ res 100 (test failed part way through)
        // TODO: number of threads affects resulting image, slightly changing some pixels
        // Most likely the threads are racing each other to update the shadow cache, causing non-deterministic behaviour!
        [TestMethod]
        public void RaytraceStaticShadow()
        {
            RaytraceScenario(shadows: true, staticShadows: true);
            RaytraceScenario(shadows: true, staticShadows: true, shading: false);

            if (numMissingBaselines > 0)
                Assert.Fail("{0} missing baseline images were recreated", numMissingBaselines);
        }

        // Release mode: 1 min @ res 100
        // Debug mode with contracts: 14 mins @ res 100
        [TestMethod]
        public void RaytraceShadowAndFocalBlur()
        {
            RaytraceScenario(focalBlur: true, shadows: true, subPixelRes: 4, resolution: defaultResolution / 2);

            // TODO: test higher quality focal blur, and different focal depths. Also test it without shadows?
//            RaytraceScenario(focalBlur: true, shadows: true, subPixelRes: 8, focalDepth: objectDepth + 0.5);

            if (numMissingBaselines > 0)
                Assert.Fail("{0} missing baseline images were recreated", numMissingBaselines);
        }

        // Release mode: 20 sec @ res 100
        // Debug mode with contracts: 3 min @ res 100
        // TODO: produced image is slightly different if this test is run in isolation vs run together with test RaytraceStaticShadow. Why?
        // TODO: number of threads affects resulting image, slighty changing quite a few pixels
        // Most likely the threads are racing each other to update the shadow cache, causing non-deterministic behaviour!
        [TestMethod]
        public void RaytraceStaticShadowAndFocalBlur()
        {
            RaytraceScenario(focalBlur: true, shadows: true, staticShadows: true, subPixelRes: 4);

            if (numMissingBaselines > 0)
                Assert.Fail("{0} missing baseline images were recreated", numMissingBaselines);
        }

        // Release mode: 1 min @ res 100
        // Debug mode with contracts: 7 min @ res 100
        [TestMethod]
        public void RaytraceShadowAndAntiAlias()
        {
            RaytraceScenario(shadows: true, subPixelRes: 4, resolution: defaultResolution / 2);

            if (numMissingBaselines > 0)
                Assert.Fail("{0} missing baseline images were recreated", numMissingBaselines);
        }

        // Release mode: 22 seconds @ res 100 (slowed down to 2 mins after firing 100 rays per lightfield cell!)
        // Debug mode with contracts: 2 min @ res 100
        [TestMethod]
        public void RaytraceLightField_Tris()
        {
            RaytraceScenario(lightField: true, lightFieldWithTris: true, focalBlur: false, shadows: true, subPixelRes: 4);
            RaytraceScenario(lightField: true, lightFieldWithTris: true, subPixelRes: 4);
            RaytraceScenario(lightField: true, lightFieldWithTris: true, yawDegrees: -125);

            if (numMissingBaselines > 0)
                Assert.Fail("{0} missing baseline images were recreated", numMissingBaselines);
        }

        // Release mode: 6 seconds @ res 100
        // Debug mode with contracts: 39 sec @ res 100
        [TestMethod]
        public void RaytraceLightField_Colors()
        {
            RaytraceScenario(lightField: true, lightFieldWithTris: false);

            // TODO: number of threads affects resulting image, slighty changing quite a few pixels
            // Most likely the threads are racing each other to update the lightfield, causing non-deterministic behaviour!
            RaytraceScenario(lightField: true, lightFieldWithTris: false, focalBlur: true, shadows: true, subPixelRes: 4);

            // TODO: add render with quad-filtering on color lightfield

            if (numMissingBaselines > 0)
                Assert.Fail("{0} missing baseline images were recreated", numMissingBaselines);
        }

        // skip all tests up until a specific test, then run it and all the rest
/*
        private const string startFromTest = "raytrace/100x100/noShading_staticShadows_lightField";
        private bool skipTest = true;
*/

        // TODO: add flag for quad-filtering on color lightfield
        private void RaytraceScenario(bool shading = true, bool focalBlur = false, bool shadows = false, bool staticShadows = false,
            bool lightField = false, bool lightFieldWithTris = false, bool pathTracing = false, int subPixelRes = 1, int resolution = 100,
            double pitchDegrees = defaultPitchDegrees, double yawDegrees = defaultYawDegrees, double rollDegrees = defaultRollDegrees,
            double focalDepth = -1, GeometryCollection extraGeometry = null)
        {
            using (var renderer = new Renderer())
            {
                RendererSetup(renderer, modelFileName, pitchDegrees, yawDegrees, rollDegrees, objectDepth, resolution);

                // TODO: raytracer has slight difference on cube edges between Release and Debug mode. Rounding error?
                renderer.rayTrace = true;
                renderer.rayTraceSubdivision = true;
                renderer.rayTraceShading = shading;
                renderer.rayTraceFocalBlur = focalBlur;
                renderer.rayTraceFocalDepth = (Math.Abs(focalDepth + 1) < 0.001 ? objectDepth + 0.5 : focalDepth);
                renderer.rayTraceSubPixelRes = subPixelRes;
                renderer.rayTracePathTracing = pathTracing;

                renderer.rayTraceShadows = shadows; // TODO: adding this flag seems to increase test runtime massively! (during focal blur?)
                renderer.rayTraceShadowsStatic = staticShadows;
                // TODO: ambient occlusion has some instability, sometimes causes banding, and fails regression testing
                //renderer.rayTraceAmbientOcclusion = true; // = flags[2];
                // TODO: non-cached AO has many differences to cached AO
                //renderer.ambientOcclusionEnableCache = false;

                // avoid caching static shadows to disk, as this can produce a slightly different image on each test execution
                renderer.CacheStaticShadowsToFile = false;

                // lightfields must be used in x64 mode to avoid 'cannot create MM view' errors
                // TODO: unit testing lightfields with memory-mapped files seems to cause an error for later tests
                renderer.rayTraceLightField = lightField;
                renderer.LightFieldStoresTriangles = lightFieldWithTris;

                // raytrace any extra geometry beyond the triangle model?
                if (extraGeometry != null)
                    renderer.ExtraGeometryToRaytrace = extraGeometry;

                // when shadows are disabled, static shadows flag has no effect
                if (renderer.rayTraceShadowsStatic && !renderer.rayTraceShadows)
                    return;

                var dirPath = "raytrace/" + resolution + 'x' + resolution;
                var testName = dirPath + '/' +
                    (renderer.rayTracePathTracing ? "pathTracing_" : "") +
                    (renderer.rayTraceShading ? "shading" : "noShading") +
                    (renderer.rayTraceShadows ? (renderer.rayTraceShadowsStatic ? "_staticShadows" : "_shadows") : "") +
                    (renderer.rayTraceAmbientOcclusion ? "_AO" : "") +
                    (lightField && lightFieldWithTris ? "_lightFieldTri" : "") + (lightField && !lightFieldWithTris ? "_lightFieldColor" : "") +
                    (renderer.rayTraceFocalBlur ? "_focalBlur" : "") +
                    (focalBlur ? "x" + subPixelRes : (subPixelRes > 1 ? "_" + subPixelRes + "xAA" : "")) +
                    (extraGeometry != null ? ("_" + (extraGeometry.Count + 1) + "_geometry") : "");

/*
                // skip all tests up until a specific test, then run it and all the rest
                if (testName == startFromTest)
                    skipTest = false;
                if (skipTest)
                    return;
*/

                Directory.CreateDirectory(candidatePath + dirPath);
                Directory.CreateDirectory(baselinePath + dirPath);

                try
                {
                    RenderAndTest(testName, renderer, resolution, resolution);
                }
                catch(Exception)
                {
                    // TODO: reports test name, but breaks stack trace links.
                    //Assert.Fail("{0}: threw exception", testName);
                    throw;
                }
            }
        }

        [TestMethod]
        public void PathTracePrimitivesTest()
        {
            var geometryList = new GeometryCollection();
            //geometryList.Add(new Plane(new Vector(0, -0.5, 0), new Vector(0, 1, 0)) { Color = Color.White });
            geometryList.Add(new Sphere(new Vector(0, -10000, 0), 9999.5) { Color = Color.White });
            geometryList.Add(new Sphere(new Vector(-0.5, 0, -0.5), 0.5) { Color = Color.Red });
            geometryList.Add(new Sphere(new Vector(+0.5, 0, +0.5), 0.5) { Color = Color.Green });
            geometryList.Add(new Sphere(new Vector(+0.5, 0, -0.5), 0.5) { Color = Color.Blue });
            geometryList.Add(new Sphere(new Vector(-0.5, 0, +0.5), 0.5) { Color = Color.Yellow });

            objectDepth = 3.0;
            double depthOfFocus = objectDepth - 0.5;
            RaytraceScenario(pathTracing: true, shading: false, extraGeometry: geometryList);
            // TODO: in this render, the triangle geometry ends up added twice!
            RaytraceScenario(pathTracing: true, shading: false, extraGeometry: geometryList, focalBlur: true, focalDepth: depthOfFocus, subPixelRes: 4);
            // TODO: in this render, the triangle geometry ends up added 3x!
            RaytraceScenario(pathTracing: true, shading: false, extraGeometry: geometryList, focalBlur: true, focalDepth: depthOfFocus, subPixelRes: 8);
        }

        [TestMethod]
        public void PathTraceTrianglesTest()
        {
            double depthOfFocus = objectDepth;
            modelFileName = "../../obj2.3ds";

            RaytraceScenario(pathTracing: true, shading: false);
            RaytraceScenario(pathTracing: true, shading: false, subPixelRes: 2);
            RaytraceScenario(pathTracing: true, shading: false, subPixelRes: 4);
            RaytraceScenario(pathTracing: true, shading: false, subPixelRes: 8);
            RaytraceScenario(pathTracing: true, shading: false, focalBlur: true, focalDepth: depthOfFocus, subPixelRes: 2);
            RaytraceScenario(pathTracing: true, shading: false, focalBlur: true, focalDepth: depthOfFocus, subPixelRes: 4);
            RaytraceScenario(pathTracing: true, shading: false, focalBlur: true, focalDepth: depthOfFocus, subPixelRes: 8);
        }
        
        // Release mode: 16-23 seconds @ res 400; 1 sec @ res 100
        // Debug mode with contracts: 2 sec @ res 100
        [TestMethod]
        public void RasteriseTest()
        {
            const int resolution = defaultResolution;
            using (var renderer = new Renderer())
            {
                RendererSetup(renderer, modelFileName, defaultPitchDegrees, defaultYawDegrees, defaultRollDegrees, objectDepth, resolution);
                renderer.rayTrace = false;

                var flags = new bool[5];
                var numPermutations = 1 << flags.Length;
                for (int i = 0; i < numPermutations; i++)
                {
                    MakeBooleanPermutation(i, flags);

                    renderer.shading = flags[0];
                    renderer.perPixelShading = flags[1];
                    renderer.pointLighting = flags[2];
                    renderer.specularLighting = flags[3];
                    renderer.depthBuffer = flags[4]; // TODO: adding this flag increases test runtime from ~10s to ~20s in Debug mode

                    // when shading is disabled, per-pixel-shading flag has no effect
                    if (renderer.perPixelShading && !renderer.shading)
                        continue;

                    var dirPath = "rasterise/" + resolution + 'x' + resolution;
                    var testName = dirPath + '/' +
                        (renderer.perPixelShading ? "perPixelShading" : (renderer.shading ? "shading" : "noShading")) +
                        (renderer.pointLighting ? "_pointLit" : "") +
                        (renderer.specularLighting ? "_specular" : "") +
                        (renderer.depthBuffer ? "_depth" : "") +
                        ("x" + renderer.rayTraceSubPixelRes);

                    Directory.CreateDirectory(candidatePath + dirPath);
                    Directory.CreateDirectory(baselinePath + dirPath);
                    RenderAndTest(testName, renderer, resolution, resolution);
                }

                if (numMissingBaselines > 0)
                    Assert.Fail("{0} missing baseline images were recreated", numMissingBaselines);
            }
        }

        /// <summary>
        /// Render image using current renderer settings, and compare image against a baseline image.
        /// </summary>
        /// <param name="testName">Unique name for the test. Can contain slashes.</param>
        private void RenderAndTest(string testName, Renderer renderer, int imageWidth, int imageHeight)
        {
            renderer.Render();

            // Copy the 2D surface into a bitmap.
            Bitmap bitmap = null;
            unsafe
            {
                fixed (void* pointer = pixels)
                {
                    bitmap = new Bitmap(imageWidth, imageHeight, imageWidth * 4, PixelFormat.Format32bppRgb, (IntPtr)pointer);
                };
            };

            bitmap.Save(candidatePath + testName + bitmapFileExtension, bitmapFormat);

            Bitmap baseBitmap = null;
            try
            {
                baseBitmap = new Bitmap(baselinePath + testName + bitmapFileExtension);
            }
            catch(ArgumentException)
            {
                // Baseline image file is missing. We recreate it.
                bitmap.Save(baselinePath + testName + bitmapFileExtension, bitmapFormat);
                numMissingBaselines++;
                return;
            }

            int numPixelsDiff = CompareBitmaps(bitmap, baseBitmap);
            Assert.AreNotEqual(-1, numPixelsDiff, "Different bitmap dimensions");
            var percentDiff = numPixelsDiff * 100.0 / imageWidth / imageHeight;
            Assert.AreEqual(0.0, percentDiff, "{0}: % different pixel colours between bitmaps", testName);
        }

        /// <summary>
        /// Compares two bitmaps, pixel by pixel.
        /// </summary>
        /// <returns>Number of corresponding pixels in bitmaps with different colour.
        /// Returns -1 if bitmaps have different dimensions.</returns>
        private static int CompareBitmaps(Bitmap bitmap1, Bitmap bitmap2)
        {
            if (bitmap1.Width != bitmap1.Width || bitmap1.Height != bitmap2.Height)
                return -1;

            int numPixelsDiff = 0;
            for (int y = 0; y < bitmap1.Height; y++)
            {
                for (int x = 0; x < bitmap1.Width; x++)
                {
                    int color1 = bitmap1.GetPixel(x, y).ToArgb();
                    int color2 = bitmap2.GetPixel(x, y).ToArgb();
                    if (color1 != color2)
                        numPixelsDiff++;
                }
            }
            return numPixelsDiff;
        }
    }
}
