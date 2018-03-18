// TODO: cannot display logging text while rendering with multiple threads!
#define PARALLEL_RENDER_TPL

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks; // TODO: not supported in Silverlight (even SL 5!)
using Engine3D.Raytrace;

namespace Engine3D
{
    /// <remarks>World space is a Left-Handed coordinate system with X+ve to the right, Y+ve up and Z+ve forward.</remarks>
    /// <remarks>View space is a Left-Handed coordinate system with X+ve to the right, Y+ve up and Z+ve forward.</remarks>
    /// <remarks>Screen space is a Left-Handed coordinate system with X+ve to the right, Y+ve down and Z+ve backward.</remarks>
    public sealed class Renderer : IDisposable
    {
        #region Public data members (should be properties)

        // Style of rendering (actually post-processing)
        public enum Style
        {
            Standard,
            ColorShuffle,
            Negative,
            DepthSmooth,
            DepthBanded,
            Normals,

            Count // must be last
        };

        public Style RenderStyle;

        // Lighting parameters (in view space).
        public double ambientLight_intensity;
        public Vector directionalLight_dir; // direction of light (in view space)
        public Vector positionalLight_pos; // position of light (in view space)
        public double specularLight_shininess;

        // Lighting parameters (in model space - only used when raytracing!)
        private Vector directionalLight_dir_model; // direction of light (in model space)
        private Vector positionalLight_pos_model; // position of light (in model space)

        private readonly Scene scene = new Scene(); // for now a scene just captures lighting parameters (for raytracing)

        /// <summary>
        /// Scan-line rendering options.
        /// </summary>
        public bool removeBackFaces = true;
        // Depth ordering
        public bool sortFacesByAxis = true;
        public bool sortFacesByDepth = false;
        public bool depthBuffer = false;
        public bool depthBufferHires = false;
        // Lighting
        public bool shading = true;          // Shade triangles by lighting per-vertex or per-pixel? Otherwise lighting is per-triangle.
        public bool perPixelShading = false; // Evaluate lighting per-pixel? Otherwise it is per-vertex, and colors are interpolated.
        public bool pointLighting = true;    // Shade using a point light? Otherwise a directional light is used.
        public bool specularLighting = true; // Include specular lighting? Diffuse and ambient lighting are always used.
        // Drawing style
        public bool drawVertices = false;
        public bool drawEdges = false;
        public bool drawTriangles = true;
        //public bool useMaterial = true; // if set to false, all surfaces will be pure white

        // Raytracing rendering options.
        public bool rayTrace = false;
        public bool rayTraceShading = true;
        public bool rayTraceShadows = false;
        public bool rayTraceShadowsStatic = false;
        public bool rayTraceAmbientOcclusion = false; // TODO: add flag for dynamic ambient occlusion, i.e. without using AO cache?
        public bool rayTraceLightField = false;
        public bool rayTraceSubdivision = true;
        public bool rayTracePathTracing = false;
        public bool rayTraceVoxels = false;
        public bool rayTraceFocalBlur = true;   // TODO: focal blur only works if rayTraceSubPixelRes > 1. Give focal blur an indepedant sub-resolution.
        public double rayTraceFocalDepth = 1.5; // the depth from the camera at which surfaces are exactly in focus
        public double rayTraceFocalBlurStrength = 10.0;
        public int rayTraceConcurrency = 4; // TODO: a higher number of threads results in more imperceptible noise in the image
        public int rayTraceSubPixelRes = 1; // sub-pixel raytracing resolution (NxN samples per pixel). Used for anti-aliasing (equivalent to antiAliasResolution) or focal blur (but these are mutally exclusive).

        public int rayTraceRandomSeed = 1234567890;

        // power-of-two size/resolution might make 4D array indexing quicker
        // TODO: res of up to 115 is okay in Silverlight app, but anything over ~64/70 makes the 32-bit web server run out of memory! 64-bit web server can go much higher.
        // TODO: in regression tests, anything over ~76 makes the 32-bit test runner run out of memory! Have not tried a 64-bit test runner.
#if SILVERLIGHT
        private const byte lightFieldRes = 115;
#else
        private const byte lightFieldRes = 64; // TODO: needs to be 64 for regression tests. Was 128 for website.
#endif

        // Camera Field Of View (for both rasterising and raytracing)
        private const double fieldOfViewDeg = 45.0;                                     // camera Field Of View (in degrees). 90 degrees is our default.
//        private const double fieldOfViewDeg = 53.130102354155978703144387440907;      // TODO: this is the legacy rasteriser Field Of View
//        private const double fieldOfViewDeg = 45.0;                                   // TODO: this is the legacy raytracer Field Of View
        private const double fieldOfViewRad = fieldOfViewDeg / 180.0 * Math.PI;         // camera Field Of View (in radians)
        private readonly double fieldOfViewDepth = 0.5 / Math.Tan(fieldOfViewRad / 2);  // x and y vary from -0.5 to +0.5 in view space, so a depth of 0.5 gives a 90 degree FOV

        // Technique for caching per-ray geometry or per-ray color (during raytracing)
        private LightFieldColorMethod lightFieldColorMethod;
        private LightFieldTriMethod lightFieldTriMethod;

        // Technique for shading surfaces (during raytracing)
        private ShadingMethod shadingMethod;

        // Technique for tracing rays and shading surfaces (an alternative to vanilla raytracing)
        private PathTracingMethod pathTracingMethod;

        // Technique for casting soft shadows (during raytracing)
        private const int staticShadowRes = 128;
        private ShadowMethod shadowMethod;

        // Technique for calculating ambient occlusion (during raytracing)
        private AmbientOcclusionMethod ambientOcclusionMethod;

        // 3D grid of voxels
        private VoxelGrid voxels;

//#if DEBUG

//        public int rayTraceDebug = 0;

//#endif

        //public const int pointSize = 10;

        // Miscellaneous options
        public bool showModelLoadProgressBar = false;

        // Change these in order to only raytrace a subset of all the rows of pixels.
        public int rayTraceStartRow;
        public int rayTraceEndRow;

        // Miscellaneous options
        public double modelLoadProgressPercentage; // TODO: shouldn't this be private?

        #endregion

        #region Private data members

        private readonly Color defaultColor = new Color(1, 1, 1);

        private Surface surface;

        // Dimensions of the rendering surface. This may be much smaller than the viewport size,
        // in which case the rendering surface is scaled up.
        private int width;
        private int height;

        // Anti-aliasing
        private int antiAliasResolution = 1; // Anti-aliasing will be done with a grid of NxN samples per pixel
        private Surface antiAliasedSurface;

        private double majorAxis; // width or height of view, whichever is greatest (in pixels)
        private double aspectRatio; // ratio between width and height of view (in pixels)

        // The 3D model to render.
        // This is non-null only if the 3D model has been loaded successfully.
        private Model model;

        // A 3D model that may be in the process of being loaded from a stream or URI.
        // This may be reloaded in the background asynchronously at any time,
        // or changed by a client of this class.
        private Model modelVolatile;

        //private Instance _instance;

        // Raytracing.
        private GeometryCollection geometry_simple;
        private SpatialSubdivision geometry_subdivided;
        private IRayIntersectable rootGeometry;
        //private Surface texture;
        private int totalNodesVisits;
        private int totalLeafNodesVisits;
        private int totalGeometryTests;
        private int numRaysFired;

        #endregion

        #region Contract Invariants

        [ContractInvariantMethod]
        private void ClassContract()
        {
            Contract.Invariant(antiAliasResolution >= 0);
            Contract.Invariant(width > 0);
            Contract.Invariant(height > 0);
            Contract.Invariant(Math.Max(width, height) == majorAxis);
            Contract.Invariant((double)height / (double)width == aspectRatio);
            Contract.Invariant(geometry_simple == null || geometry_simple.Count == model.Triangles.Count);

            Contract.Invariant(surface != null);
            Contract.Invariant(surface.Width == width);
            Contract.Invariant(surface.Height == height);
            Contract.Invariant(surface.Pixels != null);
            Contract.Invariant(surface.Pixels.Length == width * height);
        }

        #endregion

        #region Constructor

        public Renderer()
        {
            // Choose default lighting parameters (in view space).
            ambientLight_intensity = 0.1;
            // We pick a lighting direction similar to sunlight: coming from the right, above and behind the camera.
            //directionalLight_dir = new Vector(0.0, 0.0, 1.0); // direction of light (in view space)
            directionalLight_dir = new Vector(-1, -1, 1); // direction of light (in view space - TODO: raytracing coordinates!)
            directionalLight_dir.Normalise();
            Contract.Assume(directionalLight_dir.IsUnitVector);
            positionalLight_pos = new Vector(0.0, 0.0, 1.5) - directionalLight_dir * 2; // position of light (in view space)
            specularLight_shininess = 100.0;

            Instances = new List<Instance>();
            ExtraGeometryToRaytrace = new GeometryCollection();

            width = 1;
            height = 1;
            majorAxis = 1;
            aspectRatio = 1;
            surface = new Surface(width, height, new int[1]);

            // Default cache path
            CachePath = "./cache";
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (null != lightFieldColorMethod)
            {
                lightFieldColorMethod.Dispose();
                lightFieldColorMethod = null;
            }

            if (null != lightFieldTriMethod)
            {
                lightFieldTriMethod.Dispose();
                lightFieldTriMethod = null;
            }

            if (null != ambientOcclusionMethod)
            {
                ambientOcclusionMethod.Dispose();
                ambientOcclusionMethod = null;
            }
        }

        #endregion

        #region Properties

        public List<Instance> Instances { get; private set; }

        /// <summary>
        /// Path to disk cache to store calculated data, e.g. lightfield, ambient occlusion
        /// This is ignored for Silverlight, which uses IsolatedStorage
        /// </summary>
        public string CachePath { get; set; }

        // Legacy properties: set angles for the only object.
        // TODO: deprecate these.
        //public double Pitch
        //{
        //    get { return _instance.Pitch; }
        //    set { _instance.Pitch = value; }
        //}

        //public double Yaw
        //{
        //    get { return _instance.Yaw; }
        //    set { _instance.Yaw = value; }
        //}

        //public double Roll
        //{
        //    get { return _instance.Roll; }
        //    set { _instance.Roll = value; }
        //}

        //public Vector EulerAngles
        //{
        //    get { return _instance.EulerAngles; }
        //    set { _instance.EulerAngles = value; }
        //}

        //public Vector ObjectPos
        //{
        //    get { return _instance.Position; }
        //    set { _instance.Position = value; }
        //}

        // Background color (0xAARRGGBB = Alpha, Red, Green, Blue. Alpha will be ignored).
        // Note that setting this to non-zero slows down the rendering slightly.
        private uint backgroundColor = 0x00000000;

        /// <summary>
        /// Get or set the background colour (without alpha component)
        /// </summary>
        public uint BackgroundColor
        {
            get
            {
                return backgroundColor;
            }
            set
            {
                // Mask out Alpha/Depth component of background color (rasteriser sometimes stores 8-bit depth in Alpha component!)
                // TODO: final image is fully transparent when saved as a PNG file!
                backgroundColor = value & 0x00FFFFFF;
            }
        }

        /// <summary>
        /// Get the background colour (including fully opaque alpha component)
        /// </summary>
        public uint BackgroundColorWithAlpha
        {
            get
            {
                return backgroundColor | 0xFF000000;
            }
        }

        public int RenderingSurfaceWidth
        {
            get
            {
                return width;
            }
        }

        public int RenderingSurfaceHeight
        {
            get
            {
                return height;
            }
        }

        public Model Model
        {
            get
            {
                return modelVolatile;
            }
            set
            {
                modelVolatile = value;
                if (modelVolatile != null)
                {
                    modelVolatile.LoadingComplete = true;
                    modelVolatile.LoadingError = false;
                }
            }
        }

        public int AntiAliasResolution
        {
            get
            {
                return antiAliasResolution;
            }
            set
            {
                Contract.Requires(value > 0, "AntiAliasResolution must be greater than zero");
                Contract.Assert(surface != null, "SetRenderingSurface must be called before AntiAliasResolution is set");
                
                // Has anti-aliased resolution changed?
                if (value != antiAliasResolution)
                {
                    // Restore the original surface.
                    if (antiAliasedSurface != null)
                    {
                        surface = antiAliasedSurface;
                    }

                    int sizeX = surface.Width;
                    int sizeY = surface.Height;
                    int[] pixels = surface.Pixels;

                    antiAliasResolution = value;
                    if (antiAliasResolution == 1)
                    {
                        antiAliasedSurface = null;
                    }
                    else
                    {
                        // Create a new larger surface for pre-anti-aliased rendering.
                        antiAliasedSurface = surface;
                        //surface = new Surface(surface.Width * antiAliasResolution, surface.Height * antiAliasResolution);

                        sizeX *= antiAliasResolution;
                        sizeY *= antiAliasResolution;
                        pixels = new int[sizeX * sizeY];
                    }

                    // TODO: hack
                    int tmp = antiAliasResolution;
                    antiAliasResolution = 1;
                    SetRenderingSurface(sizeX, sizeY, pixels);
                    antiAliasResolution = tmp;

                    //width = surface.Width;
                    //height = surface.Height;
                }
            }
        }

        // does lightfield store a triangle per entry, or a color per entry?
        // Not multithread safe
        public bool LightFieldStoresTriangles
        {
            get
            {
                return lightFieldHasTris;
            }
            set
            {
                // if switching type of lightfield, remove old lightfield to avoid reinterpreting its data
                if (value != lightFieldHasTris)
                {
                    if(null != lightFieldColorMethod)
                    {
                        lightFieldColorMethod.Dispose();
                        lightFieldColorMethod = null;
                    }
                    if (null != lightFieldTriMethod)
                    {
                        lightFieldTriMethod.Dispose();
                        lightFieldTriMethod = null;
                    }
                }

                lightFieldHasTris = value;
            }
        }
        private bool lightFieldHasTris = true;

        public bool CacheStaticShadowsToFile
        {
            get { return cacheStaticShadowsToFile; }
            set
            {
                cacheStaticShadowsToFile = value;
                if(shadowMethod != null)
                    shadowMethod.CacheStaticShadowsToFile = cacheStaticShadowsToFile;
            }
        }
        private bool cacheStaticShadowsToFile = true;

        public GeometryCollection ExtraGeometryToRaytrace { get; set; }

        /// <summary>
        /// The number of rays fired during the last rendering.
        /// </summary>
        public int NumRaysFired
        {
            get
            {
                return numRaysFired;
            }
        }

        /// <summary>
        /// The number of nodes visited in the tree during the last rendering.
        /// </summary>
        public int NumNodeVisits
        {
            get
            {
                return totalNodesVisits;
            }
        }

        /// <summary>
        /// The number of leaf nodes visited in the tree during the last rendering.
        /// </summary>
        public int NumLeafNodeVisits
        {
            get
            {
                return totalLeafNodesVisits;
            }
        }

        /// <summary>
        /// The number of geometry objects tested during the last rendering.
        /// </summary>
        public int NumGeometryTests
        {
            get
            {
                return totalGeometryTests;
            }
        }

        /// <summary>
        /// A running total of the number of clipped rays, i.e. outside the global bounding box. This is never reset.
        /// </summary>
        public int ClippedRayCount
        {
            set
            {
                if (geometry_subdivided != null)
                    geometry_subdivided.ClippedRayCount = value;
            }

            get
            {
                if (geometry_subdivided != null)
                    return geometry_subdivided.ClippedRayCount;
                else
                    return 0;
            }
        }

        /// <summary>
        /// A running total of the number of rays traced through the binary tree. This is never reset.
        /// </summary>
        public int TracedRayCount
        {
            set
            {
                if (geometry_subdivided != null)
                    geometry_subdivided.TracedRayCount = value;
            }

            get
            {
                if (geometry_subdivided != null)
                    return geometry_subdivided.TracedRayCount;
                else
                    return 0;
            }
        }

        /// <summary>
        /// A running total of the number of ray-geometry tests performed. This is never reset.
        /// </summary>
        public int RayGeometryTestCount
        {
            set
            {
                if (rayTraceSubdivision)
                {
                    if (geometry_subdivided != null)
                    {
                        geometry_subdivided.RayGeometryTestCount = value;
                    }
                }
                else
                {
                    if (geometry_simple != null)
                    {
                        geometry_simple.RayGeometryTestCount = value;
                    }
                }
            }

            get
            {
                if (rayTraceSubdivision)
                {
                    if (geometry_subdivided != null)
                    {
                        return geometry_subdivided.RayGeometryTestCount;
                    }
                }
                else
                {
                    if (geometry_simple != null)
                    {
                        return geometry_simple.RayGeometryTestCount;
                    }
                }
                return 0;
            }
        }

        #endregion

        #region Public methods

        public void SetRenderingSurface(int width, int height, int[] pixels)
        {
            if (width * antiAliasResolution == this.width && height * antiAliasResolution == this.height)
            {
                // Rendering surface resolution unchanged, so don't bother recreating our surfaces.
                if (antiAliasResolution > 1)
                {
                    antiAliasedSurface.SetPixelBuffer(pixels);
                }
                else
                {
                    surface.SetPixelBuffer(pixels);
                }
                return;
            }

            if (antiAliasResolution > 1)
            {
                // TODO: hack
                antiAliasedSurface = new Surface(width, height, pixels);
                width *= antiAliasResolution;
                height *= antiAliasResolution;
                pixels = new int[width * height];
            }

            this.width = width;
            this.height = height;
            majorAxis = Math.Max(width, height);
            aspectRatio = (double)height / (double)width;
            rayTraceStartRow = 0;
            rayTraceEndRow = height - 1;

            surface = new Surface(width, height, pixels);
        }

        // Stream must be seekable!
        public void Load3dsModelFromStream(Stream stream)
        {
            // TODO: get a model file name from somewhere!
            modelVolatile = new Model();
            modelVolatile.ModelLoadProgressEvent += OnModelLoadProgressUpdate;
            modelVolatile.Load3dsModelFromStream(stream);
        }

        public void Load3dsModelFromURI(string modelFileName, Uri uri)
        {
            modelVolatile = new Model();
            modelVolatile.ModelLoadProgressEvent += OnModelLoadProgressUpdate;
            modelVolatile.Load3dsModelFromURI(modelFileName, uri);
        }

        public bool HasModelCompletedLoading()
        {
            if (modelVolatile == null)
            {
                return false;
            }
            else
            {
                return modelVolatile.LoadingComplete;
            }
        }

        public bool HasModelLoadFailed()
        {
            if (modelVolatile == null)
            {
                return false;
            }
            else
            {
                return modelVolatile.LoadingError;
            }
        }

        /// <summary>
        /// Pre-calculate where possible rather than evaluating lazily.
        /// <remarks>May be called multiple times.</remarks>
        /// <remarks>The properties rayTrace and rayTraceSubdivision should be set before calling this method.</remarks>
        /// </summary>
        public void PreCalculate()
        {
            // Attempt to pin the completely loaded model.
            while(!PinModel())
            {
                Thread.Sleep(10);
            }

            if (rayTrace)
            {
                // Pre-calculate the form of geometry required by the raytracer.
                if (rayTraceSubdivision)
                {
                    if (geometry_subdivided == null)
                    {
                        geometry_subdivided = MakeRayTracableGeometry_subdivided();
                    }
                }
                else
                {
                    if (geometry_simple == null)
                    {
                        geometry_simple = MakeRayTracableGeometry_simple();
                    }
                }
            }
        }

        public void Render()
        {
            Contract.Assert(surface != null, "SetRenderingSurface must be called before Render");

            // Need to clear color and depth buffers?
            if (!rayTrace)
            {
                if (BackgroundColor == 0x00000000)
                {
                    surface.Clear(); // faster, but background is transparent!
                }
                else
                {
                    surface.Clear(BackgroundColor); // background is opaque, but slower
                    //surface.Clear(0xFF000000); // background is opaque, but slower and breaks the depth buffer!
                }

                // Clear depth buffer.
                if (depthBufferHires)
                {
                    surface.ClearDepthBuffer();
                }
            }

            // Should we display the progress of the loading of the model?
            if (showModelLoadProgressBar && model == null && modelVolatile != null)
            {
                const int size = 20;
                int middle = height / 2;
                int progressPos = (int)(modelLoadProgressPercentage / 100.0 * (width - size * 2));
                surface.DrawRect(size - 1, middle - size - 1, width - size, middle + size, 0xffffffff);
                surface.DrawFilledRect(size, middle - size, size + progressPos, middle + size, 0xff00ff00);
            }

            // Attempt to pin the completely loaded model.
            if (!PinModel())
            {
                return;
            }

            if (!rayTrace)
            {
                surface.SetModulationColor(1.0, 1.0, 1.0);
            }

            foreach (var instance in Instances)
            {
                // Let the object instance know the camera's field of view (depth)
                instance.FieldOfViewDepth = fieldOfViewDepth;

                if (rayTrace)
                {
                    // TODO: instance is raytraced in isolation, but it should effect other instances!
                    RaytraceGeometry(instance);
                }
                else
                {
                    RenderTriangleModel(instance);
                }
            }

            // TODO
            //LineWalker3D.TestLineWalking(surface);

            PostProcessImage();

            AntiAliasImage();

            // Free up any resources used while rendering this frame
            if (null != lightFieldColorMethod)
            {
                lightFieldColorMethod.EndRender();
            }
            if (null != lightFieldTriMethod)
            {
                lightFieldTriMethod.EndRender();
            }
        }

        #endregion

        #region Private methods

        #region Miscellaneous Private methods

        /// <summary>
        /// If the model has completed loading, take a reference to the model in case it is reloaded during rendering.
        /// If the model has not completed loading, does nothing.
        /// </summary>
        /// <returns>True iff the model has completed loading, and has been pinned.</returns>
        private bool PinModel()
        {
            // Have we not yet started loading the model?
            if (modelVolatile == null)
            {
                return false;
            }

            // The model might be reloaded in the background at any point in this method, so keep a reference to the original model.
            if (modelVolatile.LoadingComplete)
            {
                Logger.Log("Render: model loading complete");
                modelVolatile.LoadingComplete = false;
                model = modelVolatile; // NOTE: this is the only line that modifies the model reference or object!
                Logger.Log("Render: done initialising model");
                return true;
            }

            return model != null;
        }

        private void OnModelLoadProgressUpdate(object sender, Model.ProgressChangedEventArgs e)
        {
            modelLoadProgressPercentage = e.ProgressPercentage;
//            Logger.Log("Progress: {0:F}", e.ProgressPercentage);
//            System.Windows.MessageBox.Show("Progress: " + e.ProgressPercentage.ToString());
        }

        private void PostProcessImage()
        {
            switch(RenderStyle)
            {
                case Style.Standard:
                    // Nothing to do
                    break;

                case Style.ColorShuffle:
                    // Shuffle ZRGB to 0GBR
                    surface.ApplyColorFunc(x => ((x & 0xffff) << 8) + ((x >> 16) & 0xff));
                    break;

                case Style.Negative:
                    surface.ApplyColorFunc(x => (x == BackgroundColor ? BackgroundColor : 0x00ffffff - x));
                    break;

                case Style.DepthSmooth:
                    if (depthBufferHires)
                    {
                        // TODO
                        //float depth = GetPixelDepth(x, y);
                        //byte intensity = (byte)((1.0f - depth) * 255);
                        //surface.DrawPixel(x, y, Surface.PackRgb(intensity, intensity, intensity));
                    }
                    else if (depthBuffer)
                    {
                        // Twizzle ZRGB to 0ZZZ
                        surface.ApplyColorFunc(x => ((x >> 8) & 0xff0000) + ((x >> 16) & 0xff00) + ((x >> 24) & 0xff));
                    }
                    break;

                case Style.DepthBanded:
                    if (depthBufferHires)
                    {
                        // TODO
                        //float depth = GetPixelDepth(x, y);
                        //byte intensity = (byte)((1.0f - depth) * 255);
                        //surface.DrawPixel(x, y, Surface.PackRgb(intensity, intensity, intensity));
                    }
                    else if (depthBuffer)
                    {
                        // Twizzle ZRGB to 0ZZZ
                        surface.ApplyColorFunc(x => ((x >> 24) & 0xff) * 111);
                    }
                    break;
            }

            if (RenderStyle == Style.Normals && (depthBuffer || depthBufferHires))
            {
                float pixelHorizDelta = 1.0f / surface.Width;
                float pixelVertDelta = 1.0f / surface.Height;

                // TODO: use lambdas and delegates to make this faster
                for (int y = 0; y < surface.Height; y++)
                {
                    for (int x = 0; x < surface.Width; x++)
                    {
                        float depth1 = GetPixelDepth(x, y);
                        float depth2 = GetPixelDepth(x + 1, y);
                        float depth3 = GetPixelDepth(x, y + 1);
                        float pixelHorizDepthDelta = depth2 - depth1;
                        float pixelVertDepthDelta = depth3 - depth1;
                        Vector xTangent = new Vector(pixelHorizDelta, 0.0, pixelHorizDepthDelta);
                        Vector yTangent = new Vector(0.0, pixelVertDelta, pixelVertDepthDelta);
                        Vector normal = xTangent.CrossProduct(yTangent);
                        normal.Normalise();

                        // normal displayed in grayscale, i.e. amount that it points out of the screen
                        //byte intensity = (byte)(normal.z * 255);
                        //surface.DrawPixel(x, y, Surface.PackRgb(intensity, intensity, intensity));

                        // normal displayed in color, in view space
                        var v = new Vector(normal.x + 1, normal.y + 1, normal.z + 1);
                        v *= 128;
                        surface.DrawPixel(x, y, Surface.PackRgb((byte)v.x, (byte)v.y, (byte)v.z));
                    }
                }
            }
        }

        /// <summary>
        /// Gets a linear depth value from the depth buffer (8-bit depth buffer or floating-point depth buffer).
        /// </summary>
        /// <param name="x">Horizontal pixel coordinate</param>
        /// <param name="y">Vertical pixel coordinate</param>
        /// <returns>Linear depth value between 0.0 (nearest) to 1.0 (furthest). Missing depth returns positive infinity.</returns>
        private float GetPixelDepth(int x, int y)
        {
            // Is the floating-point depth buffer being used?
            if (depthBufferHires)
            {
                return surface.GetPixelFloatDepth(x, y);
            }
            // Is the 8-bit depth buffer being used?
            else if (depthBuffer)
            {
                // The alpha channel stores the 8-bit depth value (actually 1 - z).
                byte r, g, b, a;
                Surface.UnpackRgba(surface.GetPixel(x, y), out r, out g, out b, out a);
                if (a == 0)
                {
                    // No depth value at this pixel, so return furthest possible depth value.
                    return float.PositiveInfinity;
                }
                else
                {
                    return 1.0f - (a / 255.0f);
                }
            }
            else
            {
                // No depth buffer is being used, so return furthest possible depth value.
                return float.PositiveInfinity;
            }
        }

        // Down-sample rendering surface into a smaller anti-aliased surface. Source pixels are averaged together.
        private void AntiAliasImage()
        {
            if(antiAliasResolution < 2)
            {
                return;
            }

            //antiAliasedSurface.Clear(0xff00ff00);

            for(int destY = 0; destY < antiAliasedSurface.Height; destY++)
            {
                for(int destX = 0; destX < antiAliasedSurface.Width; destX++)
                {
                    // Accumulate pixel color components.
                    int sumR = 0;
                    int sumG = 0;
                    int sumB = 0;
                    for (int subY = 0; subY < antiAliasResolution; subY++)
                    {
                        for (int subX = 0; subX < antiAliasResolution; subX++)
                        {
                            int srcX = destX * antiAliasResolution + subX;
                            int srcY = destY * antiAliasResolution + subY;

                            byte r, g, b;
                            Surface.UnpackRgb(surface.GetPixel(srcX, srcY), out r, out g, out b);
                            sumR += r;
                            sumG += g;
                            sumB += b;
                        }
                    }

                    // Average pixel color components.
                    sumR /= antiAliasResolution * antiAliasResolution;
                    sumG /= antiAliasResolution * antiAliasResolution;
                    sumB /= antiAliasResolution * antiAliasResolution; 

                    // Draw anti-aliased pixel.
                    antiAliasedSurface.DrawPixel(destX, destY, Surface.PackRgb((byte)sumR, (byte)sumG, (byte)sumB));
                }
            }
        }

        #endregion

        #region Private Rasteriser methods

        private void RenderTriangleModel(Instance instance)
        {
            if (instance != null)
            {
                instance.InitRender(calcLightingIntensity);
            }

            if (drawEdges || drawTriangles)
                OrderAndRenderTriangles(instance);

            if (drawVertices)
                RenderVertices(instance);
        }

        private void OrderAndRenderTriangles(Instance instance)
        {
            IEnumerable<Triangle> triangles = instance.Triangles;
            if (sortFacesByDepth)
            {
                // Perform a sort to put triangles in roughly depth order.
                triangles = instance.TrianglesInDepthOrder;
            }
            else if (sortFacesByAxis)
            {
                // Direction of view, in object space.
                Vector dir = instance.TransformDirectionReverse(new Vector(0.0, 0.0, 1.0));
                //                    Logger.Log("View dir in obj space: {0},{1},{2}", dir.x, dir.y, dir.z);
                //                    Assert.IsTrue(false, dir.x + "," + dir.y + "," + dir.z);
                double absDirX = Math.Abs(dir.x);
                double absDirY = Math.Abs(dir.y);
                double absDirZ = Math.Abs(dir.z);
                double maxAxisSize = Math.Max(Math.Max(absDirX, absDirY), absDirZ);
                var renderTrianglesInReverse = false; // This is only required for sorting faces bidirectionally by axis.
                if (maxAxisSize == absDirX)
                {
                    triangles = instance.Model.TrianglesInXOrder;
                    renderTrianglesInReverse = (dir.x > 0.0);
                    //                        Logger.Log("X-axis " + (renderTrianglesInReverse ? "forward" : "reverse"));
                }
                else if (maxAxisSize == absDirY)
                {
                    triangles = instance.Model.TrianglesInYOrder;
                    renderTrianglesInReverse = (dir.y > 0.0);
                    //                        Logger.Log("Y-axis " + (renderTrianglesInReverse ? "forward" : "reverse"));
                }
                else if (maxAxisSize == absDirZ)
                {
                    triangles = instance.Model.TrianglesInZOrder;
                    renderTrianglesInReverse = (dir.z > 0.0);
                    //                        Logger.Log("Z-axis " + (renderTrianglesInReverse ? "forward" : "reverse"));
                }

                if(renderTrianglesInReverse)
                {
                    triangles = triangles.Reverse();
                }
            }

/*            else
            {
                // Perform a single pass of a bubble sort to keep the triangles in roughly view-depth order.
                // TODO: For N triangles, if the model is rotate by 180 degrees.
                SortTriangles_SinglePass(model.Triangles, viewSpaceVertices);
            }
*/

/*
            // Set triangle intensity based on index in triangle array.
            for (int i = 0; i < model.Triangles.Count; i++)
            {
                byte intensity = (byte)((double)i / (double)(model.Triangles.Count - 1) * 255);
                model.Triangles[i].color = Surface.ColorFromArgb(255, intensity, intensity, intensity);
//                model.Triangles[i].color = Surface.ColorFromArgb(255, (byte)(i >> 10), (byte)(i >> 5), (byte)i);
            }
*/

            // Render every triangle in the model.
            foreach(var tri in triangles)
            {
/*
                if (tri.vertexIndex1 < 0 || tri.vertexIndex1 >= model.Vertices.Count)
                {
                    MessageBox.Show("Invalid vertex index: " + tri.vertexIndex1);
                    continue;
                }
                if (tri.vertexIndex2 < 0 || tri.vertexIndex2 >= model.Vertices.Count)
                {
                    MessageBox.Show("Invalid vertex index: " + tri.vertexIndex2);
                    continue;
                }
                if (tri.vertexIndex3 < 0 || tri.vertexIndex3 >= model.Vertices.Count)
                {
                    MessageBox.Show("Invalid vertex index: " + tri.vertexIndex3);
                    continue;
                }
*/

                Vector v1 = instance.ViewSpaceVertices[tri.vertexIndex1];
                Vector v2 = instance.ViewSpaceVertices[tri.vertexIndex2];
                Vector v3 = instance.ViewSpaceVertices[tri.vertexIndex3];

                // Compute this triangle's normal (not unit length!)
                Vector edge1 = v2 - v1;
                Vector edge2 = v3 - v2;
                Vector normal = edge1.CrossProduct(edge2);

                // Back-face removal - only draw front facing triangles.
                if (!removeBackFaces || normal.z < 0)
                {
                    // Normalise triangle normal (but only if triangle is visible)
                    normal.Normalise();

                    // Compute lighting.
                    double v1Light = 0.0;
                    double v2Light = 0.0;
                    double v3Light = 0.0;
                    if (shading)
                    {
                        // Retrieve per-vertex lighting intensity
                        v1Light = instance.VertexLightIntensity[tri.vertexNormalIndex1];
                        v2Light = instance.VertexLightIntensity[tri.vertexNormalIndex2];
                        v3Light = instance.VertexLightIntensity[tri.vertexNormalIndex3];

                        /*
                                                    // Get vertex normals. If any are missing, we default to the triangle normal
                                                    Vector v1Normal = (tri.vertexNormalIndex1 == -1 ? normal : viewSpaceNormals[tri.vertexNormalIndex1]);
                                                    Vector v2Normal = (tri.vertexNormalIndex2 == -1 ? normal : viewSpaceNormals[tri.vertexNormalIndex2]);
                                                    Vector v3Normal = (tri.vertexNormalIndex3 == -1 ? normal : viewSpaceNormals[tri.vertexNormalIndex3]);

                                                    // Calculate per-vertex lighting
                                                    v1Light = calcLighting(v1, v1Normal);
                                                    v2Light = calcLighting(v2, v2Normal);
                                                    v3Light = calcLighting(v3, v3Normal);
                        */

                        //if (useMaterial)
                        {
                            // TODO: make this more sophisticated
                            surface.SetModulationColor(Math.Min(1.0, tri.diffuseMaterial.r + ambientLight_intensity),
                                                       Math.Min(1.0, tri.diffuseMaterial.g + ambientLight_intensity),
                                                       Math.Min(1.0, tri.diffuseMaterial.b + ambientLight_intensity));
                        }
                    }
                    else
                    {
                        // Calculate per-triangle lighting
                        //Vector centroid = (v1 + v2 + v3) * (1.0 / 3.0); // TODO: use multiply or divide?
                        // TODO: change from intensity to color
                        Color color = calcLighting(tri.centre, normal, tri.ambientMaterial, tri.diffuseMaterial,
                                                   tri.specularMaterial, tri.specularMaterialExponent);

                        //if (useMaterial)
                        {
                            //float[] normalisedColor = tri.material.Diffuse;
                            //double r = normalisedColor[0] * intensity * 255.0;
                            //double g = normalisedColor[1] * intensity * 255.0;
                            //double b = normalisedColor[2] * intensity * 255.0;
                            //surface.Color = (int)Surface.ColorFromArgb(255, (byte)r, (byte)g, (byte)b);
                            surface.Color = (int)Surface.PackColor(color);
                        }
                        //else
                        //{
                        //    byte intensityByte = (byte)(intensity * 255);
                        //    surface.Color = (int)Surface.ColorFromArgb(255, intensityByte, intensityByte, intensityByte);
                        //}
                    }

                    // TODO: transform all vertices from view space to screen space, once per frame?
                    Vector s1 = TransformPosToScreen(v1);
                    Vector s2 = TransformPosToScreen(v2);
                    Vector s3 = TransformPosToScreen(v3);

                    if (drawTriangles)
                    {
                        RenderTriangleInterior(tri, ref v1, ref v2, ref v3, ref normal, v1Light, v2Light, v3Light, ref s1, ref s2, ref s3,
                            instance.ViewSpaceNormals);
                    }

                    if (drawEdges)
                    {
                        bool enableShading = shading && !drawTriangles;
                        surface.Color = 0x00ff0000;
                        RenderTriangleEdges(surface, enableShading, tri, v1Light, ref s1, ref s2, ref s3);
                    }
                }
            }
        }

        private void RenderVertices(Instance instance)
        {
            //                int numVertices = Math.Min(viewSpaceVertices.Length, vertexLightIntensity.Length);
            for (int i = 0; i < instance.ViewSpaceVertices.Length; i++)
            //                foreach (Vector viewPos in viewSpaceVertices)
            {
                Vector screenPos = TransformPosToScreen(instance.ViewSpaceVertices[i]);
                byte intensity = 255;
                if (i < instance.VertexLightIntensity.Length)
                {
                    intensity = (byte)(instance.VertexLightIntensity[i] * 255);
                }
                uint color = (uint)((intensity << 16) + (intensity << 8) + intensity);

                int x = (int)screenPos.x;
                int y = (int)screenPos.y;
                //                    surface.DrawRect(x, y, x + pointSize - 1, y + pointSize - 1, color);
                surface.DrawPixel(x, y, color);
            }
        }

        // Note that parameters are only refs for performance reasons. They are not modified.
        private static void RenderTriangleEdges(Surface surface, bool shading, Triangle tri, double v1Light,
                                                ref Vector s1, ref Vector s2, ref Vector s3)
        {
            if (shading)
            {
                byte intensityByte = (byte)(v1Light * 255);
                surface.Color = (int)Surface.PackArgb(255, intensityByte, intensityByte, intensityByte);
            }

            // Convert vertex position to 2D integral coordinates.
            int x1 = (int)s1.x;
            int y1 = (int)s1.y;
            int x2 = (int)s2.x;
            int y2 = (int)s2.y;
            int x3 = (int)s3.x;
            int y3 = (int)s3.y;
            uint color = (uint)surface.Color;

            // Ensure that each edge is only drawn once.
            if (tri.vertexIndex1 < tri.vertexIndex2)
            {
                surface.DrawLine(x1, y1, x2, y2, color);
            }
            if (tri.vertexIndex2 < tri.vertexIndex3)
            {
                surface.DrawLine(x2, y2, x3, y3, color);
            }
            if (tri.vertexIndex3 < tri.vertexIndex1)
            {
                surface.DrawLine(x3, y3, x1, y1, color);
            }

            //                            surface.DrawTriangle_Wireframe((int)v1.pos.x, (int)v1.pos.y, (int)v2.pos.x, (int)v2.pos.y, (int)v3.pos.x, (int)v3.pos.y, (uint)surface.Color);
        }

        private void RenderTriangleInterior(Triangle tri, ref Vector v1, ref Vector v2, ref Vector v3,
                                     ref Vector normal, double v1Light, double v2Light, double v3Light,
                                     ref Vector s1, ref Vector s2, ref Vector s3, IList<Vector> viewSpaceNormals)
        {
            if (shading && perPixelShading)
            {
                // TODO
                depthBufferHires = true;

                // Get vertex normals. If any are missing, we default to the triangle normal
                Vector v1Normal = (tri.vertexNormalIndex1 == -1 ? normal : viewSpaceNormals[tri.vertexNormalIndex1]);
                Vector v2Normal = (tri.vertexNormalIndex2 == -1 ? normal : viewSpaceNormals[tri.vertexNormalIndex2]);
                Vector v3Normal = (tri.vertexNormalIndex3 == -1 ? normal : viewSpaceNormals[tri.vertexNormalIndex3]);

                // TODO: Pass view-space depth into this method?
                double z1 = 1 - s1.z;
                double z2 = 1 - s2.z;
                double z3 = 1 - s3.z;

                Attributes s1Attr = new Attributes(1 / z1, v1.x, v1.y, v1.z, v1Normal.x / z1, v1Normal.y / z1, v1Normal.z / z1);
                Attributes s2Attr = new Attributes(1 / z2, v2.x, v2.y, v2.z, v2Normal.x / z2, v2Normal.y / z2, v2Normal.z / z2);
                Attributes s3Attr = new Attributes(1 / z3, v3.x, v3.y, v3.z, v3Normal.x / z3, v3Normal.y / z3, v3Normal.z / z3);

                // TODO: inverting vertex position seems to always break, regardless of how it is combined with invZ per pixel!
                //Attributes s1Attr = new Attributes(1 / z1, v1.x / z1, v1.y / z1, v1.z / z1, v1Normal.x / z1, v1Normal.y / z1, v1Normal.z / z1);
                //Attributes s2Attr = new Attributes(1 / z2, v2.x / z2, v2.y / z2, v2.z / z2, v2Normal.x / z2, v2Normal.y / z2, v2Normal.z / z2);
                //Attributes s3Attr = new Attributes(1 / z3, v3.x / z3, v3.y / z3, v3.z / z3, v3Normal.x / z3, v3Normal.y / z3, v3Normal.z / z3);

                surface.CalcLightingFunc = calcLightingIntensity;

                surface.InterpolateTriangle(s1.x, s1.y, s1Attr, s2.x, s2.y, s2Attr, s3.x, s3.y, s3Attr,
                    surface.DrawSpan_PerPixelLitDepthHires);
            }
            else if (shading && depthBuffer)
            {
                // TODO: use a faster, specialised rasteriser.
                if (depthBufferHires)
                {
                    Attributes s1Attr = new Attributes(v1Light, 1 / (1 - s1.z));
                    Attributes s2Attr = new Attributes(v2Light, 1 / (1 - s2.z));
                    Attributes s3Attr = new Attributes(v3Light, 1 / (1 - s3.z));
                    surface.InterpolateTriangle(s1.x, s1.y, s1Attr, s2.x, s2.y, s2Attr, s3.x, s3.y, s3Attr,
                        surface.DrawSpan_LitDepthHires);
                }
                else
                {
                    Attributes s1Attr = new Attributes(v1Light, s1.z);
                    Attributes s2Attr = new Attributes(v2Light, s2.z);
                    Attributes s3Attr = new Attributes(v3Light, s3.z);
                    surface.InterpolateTriangle(s1.x, s1.y, s1Attr, s2.x, s2.y, s2Attr, s3.x, s3.y, s3Attr,
                        surface.DrawSpan_LitDepth);
                }
            }
            else if (shading)
            {
                // 24 fps
                surface.InterpolateTriangle_1Double(s1.x, s1.y, v1Light, s2.x, s2.y, v2Light, s3.x, s3.y, v3Light,
                    surface.DrawSpan_Lit);

                //                                Attributes s1Attr = new Attributes(v1Light);
                //                                Attributes s2Attr = new Attributes(v2Light);
                //                                Attributes s3Attr = new Attributes(v3Light);

                // 15 fps
                //                                surface.InterpolateTriangle(s1.x, s1.y, s1Attr, s2.x, s2.y, s2Attr, s3.x, s3.y, s3Attr,
                //                                    surface.DrawSpan_Lit);

                // 5 fps
                //                                surface.DrawPixelFunc = surface.DrawPixel_Lit;

                // 5 fps
                //                                surface.CalcPixelColorFunc = (attr => (int)(attr[0] * 255));

                // 5 fps
                //                                surface.CalcPixelColorFunc = (attr => (int)(attr[0] * 255) * 65793);

                //                                surface.InterpolateTriangle(s1.x, s1.y, s1Attr, s2.x, s2.y, s2Attr, s3.x, s3.y, s3Attr,
                //                                    surface.InterpolateSpan);
            }
            else if (depthBuffer)
            {
                surface.InterpolateTriangle_1Double(s1.x, s1.y, s1.z, s2.x, s2.y, s2.z, s3.x, s3.y, s3.z,
                    surface.DrawSpan_Depth);
                /*
                                                Attributes s1Attr = new Attributes(s1.z);
                                                Attributes s2Attr = new Attributes(s2.z);
                                                Attributes s3Attr = new Attributes(s3.z);
                                                surface.InterpolateTriangle(s1.x, s1.y, s1Attr, s2.x, s2.y, s2Attr, s3.x, s3.y, s3Attr,
                                                    surface.DrawSpan_Depth);
                */
            }
            else
            {
                /*
                Attributes v1Attr = new Attributes(0);
                Attributes v2Attr = new Attributes(0);
                Attributes v3Attr = new Attributes(0);
                surface.InterpolateTriangle(v1.pos.x, v1.pos.y, v1Attr, v2.pos.x, v2.pos.y, v2Attr, v3.pos.x, v3.pos.y, v3Attr,
                    surface.DrawSpan_Flat);
                */

                surface.DrawTriangle_Solid(s1.x, s1.y, s2.x, s2.y, s3.x, s3.y, (uint)surface.Color);
            }
        }

        // TODO: view space is post-projection, which breaks the lighting. Redefine view space to be pre-projection!
        // TODO: optimise this method
        /// <summary>
        /// Calculate lighting at a point on a surface.
        /// </summary>
        /// <param name="pos">The position of the point on the surface (in view space).</param>
        /// <param name="normal">The normal to the surface at the point (in view space). This must be a unit vector.</param>
        /// <returns>The intensity of the light at this point on the surface, as numbers between 0 and 1.</returns>
        private double calcLightingIntensity(Vector point, Vector normal)
        {
            // TODO: calculating with colors then converting to intensity is inefficient. Optimise intensity calc?
            Color color = calcLighting(point, normal, defaultColor, defaultColor, defaultColor, specularLight_shininess);
            double intensity = Math.Max(Math.Max(color.r, color.g), color.b);

            // Ensure that lighting intensity is within range.
            Assert.IsTrue(intensity >= 0.0 && intensity <= 1.0, "intensity out of range: {0}", intensity);

            return intensity;
        }

        // TODO: view space is post-projection, which breaks the lighting. Redefine view space to be pre-projection!
        // TODO: optimise this method
        /// <summary>
        /// Calculate lighting at a point on a surface.
        /// </summary>
        /// <param name="pos">The position of the point on the surface (in view space).</param>
        /// <param name="normal">The normal to the surface at the point (in view space). This must be a unit vector.</param>
        /// <returns>The color of the light at this point on the surface, as numbers between 0 and 1.</returns>
        private Color calcLighting(Vector point, Vector normal, Color ambientMaterial, Color diffuseMaterial,
                                   Color specularMaterial, double specularMaterialExponent)
        {
            Assert.IsTrue(normal.IsUnitVector, "normal is not a unit vector. Length is {0:G}, error is {1:G}", normal.Length, Math.Abs(normal.Length - 1.0));
//          Assert.IsTrue(normal.Length - 1.0 > 1e-10, "foobar");
//          Assert.IsTrue(false, "normal:({0})", normal);

            // Calculate direction from the surface point to the light.
            Vector dirToLight;
            if (pointLighting)
            {
                dirToLight = (positionalLight_pos - point);
                // TODO: instead of normalising here, later on divide dot product by length of this vector?
                dirToLight.Normalise();
            }
            else
            {
                dirToLight = -directionalLight_dir;
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
            if (specularLighting)
            {
                Vector dirToCamera = -point; // camera is at origin of view space
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
            Color color = ambientMaterial * ambientLight_intensity +
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

        /// <summary>
        /// Transform a position vector from view space to screen space.
        /// </summary>
        /// <remarks>View space is a Left-Handed coordinate system with X+ve to the right, Y+ve up and Z+ve forward.</remarks>
        /// <remarks>Screen space is a Left-Handed coordinate system with X+ve to the right, Y+ve down and Z+ve backward.</remarks>
        private Vector TransformPosToScreen(Vector pos)
        {
            // Here we invert the Y-axis to map from Y+ve up in view space to Y+ve down in view space.
            // This causes models to appear mirrored, so we invert the X-axis too.
            // We also invert the Z-axis so that we can clear the depth buffer by setting it all to zeros.
            // Note that inverting the Z-axis here does not affect where the primitives are drawn on the screen.
            return new Vector(
                pos.x * -majorAxis + width / 2.0,
                pos.y * -majorAxis + height / 2.0,
                (1.0 - pos.z));
        }

        #endregion

        #region Private Raytracer methods

        private GeometryCollection MakeRayTracableGeometry_simple()
        {
            // Convert the 3D triangle model into a set of triangle objects that can be ray traced
            GeometryCollection geom = new GeometryCollection();
            int triIndex = 0;
            foreach (Triangle tri in model.Triangles)
            {
                Vector v1 = model.Vertices[tri.vertexIndex1].pos;
                Vector v2 = model.Vertices[tri.vertexIndex2].pos;
                Vector v3 = model.Vertices[tri.vertexIndex3].pos;
                // TODO: store material ambient and specular into raytracer triangle
                uint color = Surface.PackColorAndAlpha(tri.diffuseMaterial, 1.0);
                // let the triangle know its index within this collection of geometry
                geom.Add(new Raytrace.Triangle(v1, v2, v3, color) { TriangleIndex = triIndex });
                triIndex++;
            }
            return geom;
        }

        private SpatialSubdivision MakeRayTracableGeometry_subdivided()
        {
            if (geometry_simple == null)
            {
                geometry_simple = MakeRayTracableGeometry_simple();
            }

            // Copy the triangles from the simply geometry into a list
            var geom = new List<Raytrace.Triangle>();
            for (int i = 0; i < geometry_simple.Count; i++)
            {
                var tri = (Raytrace.Triangle)geometry_simple[i];
                geom.Add(tri);
            }

            // Create a box bounding the 3D triangle model.
            var boundingBox = new AxisAlignedBox(model.Min, model.Max);
            //Vector epsilon = new Vector(1e-5, 1e-5, 1e-5);
            //var boundingBox = new AxisAlignedBox(model.Min - epsilon, model.Max + epsilon);
            //Assert.IsTrue(false, "{0} {1}", model.Min, model.Max);

            // Create the spatial subdivison object.
            return new SpatialSubdivision(geom, boundingBox);
        }

        /// <summary>
        /// Raytrace a single instance of a 3D model.
        /// Not multithread safe! Must only be executed by a single thread at a time!
        /// </summary>
        /// <param name="instance"></param>
        private void RaytraceGeometry(Instance instance)
        {
            Contract.Requires(instance != null);

#if !SILVERLIGHT 
            Directory.CreateDirectory(CachePath);
#endif

            // TODO: this does lots of unneccessary transformation and lighting work!!!
            instance.InitRender(calcLightingIntensity);

            // Transform light position and direction from view space to model space.
            directionalLight_dir_model = instance.TransformDirectionReverse(directionalLight_dir);
            Contract.Assume(directionalLight_dir_model.IsUnitVector);
            positionalLight_pos_model = instance.TransformPosFromView(positionalLight_pos);

            // setup the scene lighting
            scene.cameraPos = instance.TransformPosFromView(Vector.Zero);
            scene.pointLighting = pointLighting;
            scene.specularLighting = specularLighting;
            scene.ambientLight_intensity = ambientLight_intensity;
            scene.specularLight_shininess = specularLight_shininess;
            // view space lighting
            scene.directionalLightDir_View = directionalLight_dir;
            scene.positionalLightPos_View = positionalLight_pos;
            // object space lighting
            scene.directionalLightDir_Model = directionalLight_dir_model;
            scene.positionalLightPos_Model = positionalLight_pos_model;

            // Ensure that the form of geometry required for the raytracer has been generated.
            PreCalculate();

            // Pick the form of geometry to use.
            if (rootGeometry == null)
            {
                if (rayTraceSubdivision)
                {
                    rootGeometry = geometry_subdivided;
                }
                else
                {
                    rootGeometry = geometry_simple;
                }

                if(ExtraGeometryToRaytrace.Count > 0)
                {
                    ExtraGeometryToRaytrace.Add(rootGeometry);
                    rootGeometry = ExtraGeometryToRaytrace;
                }

/*
                // TODO: this plane is ignored because it cannot be passed to the lightFieldTriMethod!
                // TODO: quick hack to add a ground plane to show off shadows. May break later assumptions about what geometry is set to!
                // TODO: currently breaks the AO cache as intersections points are outside the unit cube!
                // TODO: currently breaks the static soft shadow cache as intersections points are outside the unit cube!
                // TODO: slows down the raytracing a fair bit, since every screen pixel now intersects some geometry!
                if (rayTraceShadows && !rayTraceShadowsStatic && !rayTraceAmbientOcclusion)
                {
                    var geometryWithPlane = new GeometryCollection();
                    geometryWithPlane.Add(rootGeometry);
                    geometryWithPlane.Add(new Plane(new Vector(0, -0.5, 0), new Vector(0, 1, 0)));

                    rootGeometry = geometryWithPlane;
                }
*/
            }

            if (rayTraceVoxels && voxels == null)
            {
                const int voxelGridSize = 64;

                // Copy the triangles from the simply geometry into a list
                var triList = new List<Raytrace.Triangle>();
                for (int i = 0; i < geometry_simple.Count; i++)
                {
                    var tri = (Raytrace.Triangle)geometry_simple[i];
                    triList.Add(tri);
                }

                var instanceKey = string.Format("voxels_tri{0}_vert{1}", instance.Model.Triangles.Count, instance.Model.Vertices.Count);
                voxels = new VoxelGrid(voxelGridSize, instanceKey, CachePath); // TODO: this assumes only one instance globally!
                if (!voxels.HasData)
                {
                    TriMeshToVoxelGrid.Convert(triList, voxelGridSize, voxels);
                }

                rootGeometry = voxels;
            }

            if (lightFieldTriMethod == null)
            {
                // Ensure that we have a list of triangles to index into, and that every triangle has an index assigned
                if (geometry_simple == null)
                {
                    geometry_simple = MakeRayTracableGeometry_simple();
                }

                var instanceKey = string.Format("lightfieldTris_tri{0}_vert{1}", instance.Model.Triangles.Count, instance.Model.Vertices.Count);
                // TODO: power-of-two size/resolution might make 4D array indexing quicker
                // TODO: cacheRes of 100 is okay in Silverlight app, but anything over ~64/70 makes the (32-bit) web server run out of memory!!!
                lightFieldTriMethod = new LightFieldTriMethod(rootGeometry, geometry_simple, geometry_subdivided, lightFieldRes, instanceKey, CachePath, rayTraceRandomSeed);
                rootGeometry = lightFieldTriMethod;
            }
            lightFieldTriMethod.Enabled = rayTraceLightField && lightFieldHasTris;

            if (shadingMethod == null)
            {
                shadingMethod = new ShadingMethod(rootGeometry, scene, instance); // TODO: this assumes only one instance globally!
                rootGeometry = shadingMethod;
            }
            shadingMethod.Enabled = rayTraceShading;

            if (pathTracingMethod == null)
            {
                pathTracingMethod = new PathTracingMethod(rootGeometry, scene, instance, rayTraceRandomSeed);
                rootGeometry = pathTracingMethod;
            }
            pathTracingMethod.Enabled = rayTracePathTracing;

            if (shadowMethod == null)
            {
                var instanceKey = string.Format("softShadow_tri{0}_vert{1}", instance.Model.Triangles.Count, instance.Model.Vertices.Count);
                // TODO: power-of-two resolution might make 3D texture indexing quicker
                var context = new RenderContext(new Random(rayTraceRandomSeed));
                shadowMethod = new ShadowMethod(rootGeometry, scene, rayTraceShadowsStatic, staticShadowRes, instanceKey, /*rayTraceRandomSeed*/ context); // TODO: this assumes only one instance globally!
                shadowMethod.CacheStaticShadowsToFile = cacheStaticShadowsToFile;
                rootGeometry = shadowMethod;
            }
            shadowMethod.Enabled = rayTraceShadows;

            if (ambientOcclusionMethod == null)
            {
                var instanceKey = string.Format("ambientOcclusion_tri{0}_vert{1}", instance.Model.Triangles.Count, instance.Model.Vertices.Count);
                // TODO: power-of-two resolution might make 3D texture indexing quicker
                ambientOcclusionMethod = new AmbientOcclusionMethod(rootGeometry, staticShadowRes, instanceKey, CachePath); // TODO: this assumes only one instance globally!
                rootGeometry = ambientOcclusionMethod;
            }
            ambientOcclusionMethod.Enabled = rayTraceAmbientOcclusion;

            if (lightFieldColorMethod == null)
            {
                // TODO: instance name should include info about AO, shadows, shading, etc
                var instanceKey = string.Format("lightfieldColors_tri{0}_vert{1}", instance.Model.Triangles.Count, instance.Model.Vertices.Count);
                // TODO: power-of-two size/resolution might make 4D array indexing quicker
                // TODO: cacheRes of 100 is okay in Silverlight app, but anything over ~64/70 makes the (32-bit) web server run out of memory!!!
                lightFieldColorMethod = new LightFieldColorMethod(rootGeometry, lightFieldRes, instanceKey, BackgroundColorWithAlpha, CachePath);
                rootGeometry = lightFieldColorMethod;
            }
            lightFieldColorMethod.Enabled = rayTraceLightField && !lightFieldHasTris;

            // Ensure that we don't attempt to draw outside the 2D surface.
            rayTraceStartRow = Math.Min(Math.Max(0, rayTraceStartRow), height - 1);
            rayTraceEndRow = Math.Min(Math.Max(0, rayTraceEndRow), height - 1);

#if PARALLEL_RENDER_TPL

            // TODO: task-based approach might be making this run slower...
            // Divide all pixel rows into N blocks. Round up the block size.
            var numRows = rayTraceEndRow - rayTraceStartRow + 1;
            var blockHeight = (numRows - 1 + rayTraceConcurrency) / rayTraceConcurrency;    // divide and round up
            var numBlocks = (numRows - 1 + blockHeight) / blockHeight;                      // divide and round up

            // Create a fixed number of tasks, each covering a block of pixel rows
            var tasks = new Task[Math.Min(rayTraceConcurrency, numBlocks)];
            var taskCount = 0;
            for (var top = rayTraceStartRow; top <= rayTraceEndRow; top += blockHeight)
            {
                var localTop = top; // copy to loop local variable for continuation to safely capture
                tasks[taskCount++] = Task.Factory.StartNew(() => RaytraceBlock(instance, rootGeometry, 0, localTop, width, blockHeight));
            }

            // Create a task for each pixel row
            //var tasks = new Task[numRows];
            //for (var row = rayTraceStartRow; row <= rayTraceEndRow; row++)
            //{
            //    var localRow = row;
            //    tasks[taskCount++] = Task.Factory.StartNew(() => RaytraceBlock(instance, 0, localRow, width, 1));
            //}

            Task.WaitAll(tasks);

#else

            RaytraceBlock(instance, rootGeometry, 0, rayTraceStartRow, width, rayTraceEndRow - rayTraceStartRow + 1);

#endif
        }

        // Thread safe
        private void RaytraceBlock(Instance instance, IRayIntersectable geometry, int left, int top, int sizeX, int sizeY)
        {
            // Create a rendering context per thread, containing a RNG. Otherwise the RNG breaks when shared between threads (without locking).
            RenderContext context = new RenderContext(new Random(rayTraceRandomSeed));

            // TODO: fix stats for multiple blocks
            totalNodesVisits = 0;
            totalLeafNodesVisits = 0;
            totalGeometryTests = 0;
            numRaysFired = 0;

            // Create texture for raytracer.
            //if (texture == null)
            //{
            //    texture = GenerateMap();
            //}

//#if DEBUG

//            rayTraceDebug = (rayTraceDebug + 2) % 7;

//#endif

            // TODO
            //rayTraceFocalDepth = Math.Abs(instance.Position.z);

            // Raytrace a grid of pixels.
            Vector start_World = instance.TransformDirectionReverse(new Vector(0, 0, -instance.Position.z));
            for (int row = top; row < top + sizeY; row++)
            {
                for (int col = left; col < left + sizeX; col++)
                {
                    if (rayTraceSubPixelRes == 1)
                    {
                        // Fast code path

                        // Here we map the Y-axis so that Y+ve is up in view space.
                        // We also invert the X-axis (X+ve is left in view space), because otherwise models appear mirrored.
                        Vector dir_View = new Vector(-((double)col / width - 0.5), -((double)row / height - 0.5) * aspectRatio, fieldOfViewDepth);
                        Vector dir_World = instance.TransformDirectionReverse(dir_View);

                        // Fire off camera ray
                        uint color = TraceRayComplex(instance, geometry, ref start_World, ref dir_World, context);

//#if DEBUG
//                        if (sizeX - col - 1 == rayTraceDebug)
//                        {
//                            color = 0x0000ff00;
//                        }
//#endif

                        // Draw pixel.
                        surface.DrawPixel(col, row, color);
                    }
                    else
                    {
                        // Accumulate pixel color components.
                        int sumR = 0;
                        int sumG = 0;
                        int sumB = 0;

                        // Compute focal point for all sub-rays of the current pixel
                        Vector pixelFocalPt_World = new Vector(0, 0, 0);
                        if (rayTraceFocalBlur)
                        {
                            // Here we map the Y-axis so that Y+ve is up in view space.
                            // We also invert the X-axis (X+ve is left in view space), because otherwise models appear mirrored.
                            Vector dir_View = new Vector(-((double)col / width - 0.5), -((double)row / height - 0.5) * aspectRatio, fieldOfViewDepth);
                            Vector dir_World = instance.TransformDirectionReverse(dir_View);
                            pixelFocalPt_World = dir_World * rayTraceFocalDepth + start_World;
                        }

                        for (int subX = 0; subX < rayTraceSubPixelRes; subX++)
                        {
                            for (int subY = 0; subY < rayTraceSubPixelRes; subY++)
                            {
                                // Convert subX and subY to values in range [-0.5, 0.5]
                                double fracSubX = (double)subX / (rayTraceSubPixelRes - 1) - 0.5;
                                double fracSubY = (double)subY / (rayTraceSubPixelRes - 1) - 0.5;

                                // Pick starting point for sub-ray
                                Vector subStart_World;
                                if (rayTraceFocalBlur)
                                {
                                    // Jitter sub-ray start around the eye point
                                    // TODO: this effect appears to be resolution-dependant. Higher resolutions have less blur!
                                    Vector subStart_View = new Vector(fracSubX / width * rayTraceFocalBlurStrength,
                                                                      fracSubY / height *  rayTraceFocalBlurStrength,
                                                                      -instance.Position.z);
                                    subStart_World = instance.TransformDirectionReverse(subStart_View);
                                }
                                else
                                {
                                    subStart_World = start_World;
                                }

                                // Pick direction for sub-ray
                                Vector dir_World;
                                if (rayTraceFocalBlur)
                                {
                                    dir_World = pixelFocalPt_World - subStart_World;
                                }
                                else
                                {
                                    Vector dir_View = new Vector(-((col + fracSubX) / width - 0.5),
                                                                 -((row + fracSubY) / height - 0.5) * aspectRatio,
                                                                 fieldOfViewDepth);
                                    dir_World = instance.TransformDirectionReverse(dir_View);
                                }

                                // Fire off camera ray
                                uint color = TraceRayComplex(instance, geometry, ref subStart_World, ref dir_World, context);

//#if DEBUG
//                                if (sizeX - col - 1 == rayTraceDebug)
//                                {
//                                    color = 0x0000ff00;
//                                }
//#endif

                                // Accumulate pixel color components.
                                byte r, g, b;
                                Surface.UnpackRgb(color, out r, out g, out b);
                                sumR += r;
                                sumG += g;
                                sumB += b;
                            }
                        }

                        // Average pixel color components.
                        sumR /= rayTraceSubPixelRes * rayTraceSubPixelRes;
                        sumG /= rayTraceSubPixelRes * rayTraceSubPixelRes;
                        sumB /= rayTraceSubPixelRes * rayTraceSubPixelRes;

                        // Draw sub-pixel-accumulated pixel.
                        surface.DrawPixel(col, row, Surface.PackRgb((byte)sumR, (byte)sumG, (byte)sumB));
                    }
                }
            }
        }

        // TODO: not used
/*
        private static string RemoveFileExtension(string filePath)
        {
            int index = filePath.LastIndexOf('.');
            if (index == -1)
            {
                return filePath;
            }
            else
            {
                return filePath.Substring(0, index);
            }
        }
*/

        // TODO: make this method static and pass in backgroundColor as a parameter
        // TODO: extract all per-thread code into a new class. The code mostly operates on parameters, not data members.
        // Thread safe
        private uint TraceRayComplex(Instance instance, IRayIntersectable geometry, ref Vector start_World, ref Vector dir_World, RenderContext context)
        {
            Vector rayStart = start_World;
            Vector rayDir = dir_World;

            // trace a primary ray against all triangles in scene
            IntersectionInfo info = TraceRaySimple(geometry, ref rayStart, ref rayDir, context);
            if (info == null)
            {
                // ray did not hit the scene - return background color
                return BackgroundColorWithAlpha; // force background colour to be fully opaque
            }

            // by this point, our ray definitely intersected some geometry
            Contract.Assert(info != null);

            // visualise surface normals
            //var v = new Vector(Math.Abs(info.normal.x), Math.Abs(info.normal.y), Math.Abs(info.normal.z));
            //v *= 255;
            //return Surface.PackRgb((byte)v.x, (byte)v.y, (byte)v.z);

            // Texture mapping. Very few models have texture coordinates!
            //int texX = (int)((info.u - Math.Floor(info.u)) * texture.Width);
            //int texY = (int)((info.v - Math.Floor(info.v)) * texture.Height);
            //color = texture.GetPixel(texX, texY);

            //color = Surface.ColorFromArgb(0, intensityByte, intensityByte, intensityByte);

            return info.color;
        }

        // TODO: this method should live on the geometry object, so that it can be used by other classes
        // TODO: make this method static, and pass the statistics in as a ref parameter
        // Thread safe
        private IntersectionInfo TraceRaySimple(IRayIntersectable geometry, ref Vector start_World, ref Vector dir_World, RenderContext context)
        {
/*
            // TODO: quickly test the sphere primitive alone
            var sphere = new Sphere(new Vector(0, 0, 0), 0.5);
            Sphere.LatLong spherePt1, spherePt2;
            sphere.IntersectLine(start_World, start_World + dir_World, out spherePt1, out spherePt2);

            if (spherePt1 == null)
                return null;

            var info = new IntersectionInfo();
            //info.rayFrac = rayFrac;
            //info.pos = start + dir * rayFrac;
            //info.normal = info.pos - center;
            //info.normal.Normalise();
            //info.color = 0x00ffffff;

            var visualNorm = spherePt1.horizAngle / Math.PI / 2.0 + 0.5;  // [0, 1]
            //var visualNorm = spherePt1.vertAngle / Math.PI + 0.5;         // [0, 1]
            //byte visualNormByte = (byte)(visualNorm * 255);     // [0, 255]
            var visualNormInt = (int)(visualNorm * 255);        // [0, 255]
            info.color = (uint)(visualNormInt << 8);
            //info.color = (uint)((visualNormByte << 16) + (visualNormByte << 8) + visualNormByte);

            return info;
*/


            // TODO: this line is the major bottleneck!
            IntersectionInfo info = geometry.IntersectRay(start_World, dir_World, context);

            Interlocked.Increment(ref numRaysFired);
            Interlocked.Add(ref totalGeometryTests, geometry.NumRayTests);
            if (geometry == geometry_subdivided)
            {
                // We are raytracing subdivided geometry
                Interlocked.Add(ref totalNodesVisits, geometry_subdivided.NumNodesVisited);
                Interlocked.Add(ref totalLeafNodesVisits, geometry_subdivided.NumLeafNodesVisited);
            }
            return info;
        }

/*
        private static Surface GenerateMap()
        {
            Surface map = new Surface(100, 100);
            uint texelIndex = 0;
            for (uint y = 0; y < map.Height; y++)
            {
                for (uint x = 0; x < map.Width; x++)
                {
                    double re = (double)x / (double)(map.Width);
                    double im = (double)y / (double)(map.Height);
                    int level = Mandelbrot(re, im) * 20;

                    byte r = (byte)(level * 2);
                    byte g = (byte)(-level * 3);
                    byte b = (byte)(level * 5);
                    byte a = 255;

                    map.Pixels[texelIndex++] = (a << 24) + (r << 16) + (g << 8) + b;
                }
            }
            return map;
        }

        private static int Mandelbrot(double re, double im)
        {
            double zr = re;
            double zi = im;
            for (int level = 0; level < 32; level++)
            {
                if (zr * zr + zi * zi > 2.0)
                {
                    return level;
                }

                // z = z^2 + c;
                // z = (zr + zi * i)
                // z^2 = (zr^2 - zi^2 + 2 * zr * zi * i)
                double oldZr = zr;
                zr = zr * zr - zi * zi + re;
                zi = 2 * oldZr * zi + im;
            }

            return 0;
        }
*/

        #endregion

        #endregion
    }
}