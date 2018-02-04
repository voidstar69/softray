//#define LOAD_REMOTE_MODEL
#define USE_MODEL_MEMORY_CACHE
#define USE_RENDER_MEMORY_CACHE
#define USE_RENDER_DISK_CACHE
//#define USE_AMBIENT_OCCLUSION_MEMORY_CACHE
#define SHOW_LOG
#define SUPPRESS_EXCEPTION

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Caching;
using Engine3D;

public partial class RenderModel : System.Web.UI.Page
{
    #region Constants

    const int maxRenderFileCacheImageWidth = int.MaxValue;
    //const int maxRenderFileCacheImageWidth = 320; // to conserve disk space on AspSpider websites
    const int renderFileCacheVersion = 4;
    const int renderMemoryCacheVersion = 1;
    const int ambientOcclusionMemoryCacheVersion = 1;

    readonly TimeSpan memoryCacheTime = new TimeSpan(30, 0, 0, 0); // 30 days

    const string bitmapFileExtension = ".jpg";
    const string bitmapContentType = "image/jpeg";
    readonly ImageFormat bitmapFormat = ImageFormat.Jpeg;

    // TODO: rendered image has transparent alpha channel, so PNG files always end up fully transparent!
    //const string bitmapFileExtension = ".png";
    //const string bitmapContentType = "image/png";
    //readonly ImageFormat bitmapFormat = ImageFormat.Png;

    #endregion

    #region Main page-rendering method

    protected void Page_Load(object sender, EventArgs e)
    {
        #region Variables

        // Options which can be overridden with URL parameters
        string modelFileName = "cube.3ds";
        string modelName = null;
        int imageWidth = 1024;
        int imageHeight = 768;
        double yawDegrees = 0.0;
        double pitchDegrees = 0.0;
        double rollDegrees = 0.0;
        double objectDepth = 3.0; //  1.0; // depth of object (in view space)
        bool rayTrace = false;
        bool rayTraceLightField = false;
        int antiAliasResolution = 3; // TODO: anti-aliasing often causes OutOfMemoryException
        int cacheLevel = 1; // 0 -> invalidate all caches. 1 -> read/write all caches.

        // Working variables.
        string renderImageKey = null;
        string renderCacheDir = null;
        string renderCacheFile = null;
        Bitmap bitmap = null;
        StringBuilder logText = null;
        bool exceptionCaught = false;

        #endregion

#if SUPPRESS_EXCEPTION

        try
        {

#endif

#if SHOW_LOG

        logText = new StringBuilder();
        Logger.LogLineEvent += delegate(string text) { logText.AppendLine(text); if (logText.Length > 10000) logText.Length = 10000; };

#endif

        // Extract parameters
        GetParam("model", ref modelFileName);
        GetParam("width", ref imageWidth);
        GetParam("height", ref imageHeight);
        GetParam("yaw", ref yawDegrees);
        GetParam("pitch", ref pitchDegrees);
        GetParam("roll", ref rollDegrees);
        GetParam("depth", ref objectDepth);
        GetParam("antiAlias", ref antiAliasResolution);
        GetParam("cache", ref cacheLevel);
        GetParam("raytrace", ref rayTrace);
        GetParam("lightfield", ref rayTraceLightField);
        if (rayTraceLightField)
            rayTrace = true;

        // Extract the name of the model from the 3DS file name.
        modelName = RemoveFileExtension(modelFileName);

        // Wrap all angles to lie within [0, 360) degrees.
        yawDegrees = WrapAngleDegrees(yawDegrees);
        pitchDegrees = WrapAngleDegrees(pitchDegrees);
        rollDegrees = WrapAngleDegrees(rollDegrees);

#if USE_RENDER_MEMORY_CACHE

        // Determine the unique key for this rendered image.
        renderImageKey = modelName + '_' + renderMemoryCacheVersion + '_' + (rayTrace ? (rayTraceLightField ? "rl" : "r") : "s") + '_' +
            imageWidth + '_' + imageHeight + '_' + yawDegrees + '_' + pitchDegrees + '_' + rollDegrees + '_' + objectDepth + "_pathTracing";

        // If image is in memory cache, and not invalidating the memory cache, return the cached image.
        if (cacheLevel > 0 && SendImageFromMemCache(renderImageKey, Cache, Response))
        {
            return;
        }

#endif

#if USE_RENDER_DISK_CACHE

        // Generate the path to the directory and file used to cache this render.
        string partitionName = modelName.Substring(0, 1).ToLower();
        renderCacheDir = string.Format(@"{0}3dRenders\v{1}\{2}\{3}",
            Database.GetDataRoot(Server), renderFileCacheVersion, partitionName, modelName);

        renderCacheFile = string.Format(@"{0}\{1}_{2}_{3}_{4}_{5}_{6}_{7}{8}",
            renderCacheDir, (rayTrace ? (rayTraceLightField ? "rl" : "r") : "s"), imageWidth, imageHeight, yawDegrees, pitchDegrees,
            rollDegrees, objectDepth, bitmapFileExtension);

        // If image is in disk cache, and not invalidating the disk cache, return the cached image.
        if (cacheLevel > 0 && SendImageFromDiskCache(renderCacheFile, Response))
        {
            return;
        }

#endif

        // Reuse cached renderer object if possible.
        // TODO: we do this to try avoid out-of-memory exceptions when anti-aliasing, probably caused by leaking memory somehow.
        // Unfortunately this seems to often cause thumbnails to be of the wrong model, i.e. concurrent
        // rendering screws up! Find a way to not leak memory, or reuse only 2D surfaces.
        //Renderer renderer = Cache["RenderModel-Renderer"] as Renderer;
        //if (renderer == null)
        //{
        //    renderer = new Renderer();
        //    // Insert renderer into cache with a 1-day sliding expiration.
        //    Cache.Insert("RenderModel-Renderer", renderer, null, Cache.NoAbsoluteExpiration, memoryCacheTime);
        //}

        using (var renderer = new Renderer())
        {
            renderer.CachePath = Path.Combine(Database.GetDataRoot(Server), "cache");
            Directory.CreateDirectory(renderer.CachePath);

            /*
                    // Lighting parameters (in view space).
                    renderer.ambientLight_intensity = 0.1;
                    // We pick a lighting direction similar to sunlight: coming from the right, above and behind the camera.
                    renderer.directionalLight_dir = new Vector(-1.0, -1.0, -1.0); // direction of light (in view space)
                    renderer.directionalLight_dir.Normalise();
                    renderer.positionalLight_pos = renderer.objectPos - renderer.directionalLight_dir * 2; // position of light (in view space)
                    renderer.specularLight_shininess = 100.0;
            */

            // Pick rendering parameters.
            renderer.BackgroundColor = 0x00000000; // transparent black
            //renderer.BackgroundColor = 0xff1f1f1f; // nearly black
            //renderer.BackgroundColor = 0xffff0000; // red

            renderer.rayTrace = rayTrace;
            renderer.rayTraceLightField = rayTraceLightField;
            renderer.LightFieldStoresTriangles = true; // TODO: was false
            renderer.rayTraceShading = true;
            renderer.rayTraceAmbientOcclusion = true;
            renderer.rayTraceFocalBlur = true;
            renderer.rayTraceSubPixelRes = 4;

            //renderer.rayTracePathTracing = true;
            //renderer.rayTraceShading = false;
            //renderer.rayTraceAmbientOcclusion = false;
            //renderer.rayTraceSubPixelRes = 1;

            // TODO: turn off most raytracing features when using voxels, just to make it faster to test and debug
            renderer.rayTraceVoxels = true;
            renderer.rayTraceLightField = false;
            renderer.rayTraceAmbientOcclusion = false;
            renderer.rayTraceFocalBlur = false;
            renderer.rayTraceSubPixelRes = 1;

/*
            // TODO: make these into parameters?
            // TODO: using these features makes raytracing much, much slower!
            if (rayTraceLightField)
            {
                // TODO: these may break when used together with lightfields. Shadows option displayed a weird spherical point-cloud!
                //renderer.rayTraceShadows = true;
                //renderer.rayTraceAmbientOcclusion = true;

                // TODO: focal blur only works if rayTraceSubPixelRes > 1. Give focal blur an independant sub-resolution!
                renderer.rayTraceFocalBlur = true;
                renderer.rayTraceSubPixelRes = 4;
            }
*/

            renderer.sortFacesByAxis = false;
            renderer.depthBuffer = true;
            renderer.depthBufferHires = true;
            renderer.perPixelShading = true;
            renderer.shading = true;
            renderer.pointLighting = true;
            renderer.specularLighting = true;

            // Fetch 3D model from memory cache, local server or remote server.
            Model cachedModel = null;

#if USE_MODEL_MEMORY_CACHE

            cachedModel = Cache[modelName] as Model;

#endif

            if (cachedModel != null && cacheLevel > 0)
            {
                // 3D model is in cache, so reuse it instead of reloading the model from disk.
                renderer.Model = cachedModel;
            }
            else
            {
                // TODO: this is async, which doesn't seem to work for an ASP.NET webpage. Make it sync?
                //renderer.Load3dsModelFromURI(modelName, new Uri("http://gavinm.brinkster.net/modellibrary/getmodel.aspx?model=" + modelName));

#if LOAD_REMOTE_MODEL

            // 3D model is not in the cache, so load the model from the website where the models are stored.
            // TODO: use flag to load models locally for development, for speed and to avoid using up hosting bandwidth limits.
            string address = "http://voidstar.xtreemhost.com/3dmodels/" + modelFileName;
            using (var webClient = new WebClient())
            {
                // Add a user agent header in case the requested URI contains a query.
                webClient.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
                //webClient.Headers.Add("user-agent", "Googlebot/2.1 (+http://www.google.com/bot.html)");
                
                //webClient.UseDefaultCredentials = true;
                //webClient.Headers.Add("Content-Type","application/x-www-form-urlencoded");

                //using (Stream stream = webClient.OpenRead(address))
                //{
                //    renderer.Load3dsModelFromStream(stream);
                //}

                // Load3dsModelFromStream requires the stream to be seekable, which OpenRead does not guarentee,
                // so download all the model data and pass a (seekable) memory stream.
                // TODO: how to tell if the model is missing? We seem to download the 'missing page' data.
                byte[] data = webClient.DownloadData(address);
                using (var stream = new MemoryStream(data))
                {
                    renderer.Load3dsModelFromStream(stream);
                }
            }

#else

                // 3D model is not in the cache, so load the model from disk.
                string modelFilePath = string.Format("{0}3dModels\\{1}", Database.GetDataRoot(Server), modelFileName);
                using (Stream stream = new FileStream(modelFilePath, FileMode.Open, FileAccess.Read))
                {
                    renderer.Load3dsModelFromStream(stream);
                }

#endif

                /*
//            renderer.Load3dsModelFromURI(modelName, new Uri("http://aspspider.ws/voidstar69/modellibrary/GetModel.aspx?model=" + modelName + ".3DS"));
            renderer.Load3dsModelFromURI(modelName, new Uri("http://voidstar.xtreemhost.com/" + modelName + ".3DS"));
            while (!renderer.HasModelCompletedLoading())
            {
                if (renderer.HasModelLoadFailed())
                {
                    throw new ApplicationException("Error loading model: " + modelName);
                }
            }
*/

                //         Response.Redirect("http://voidstar.xtreemhost.com/" + modelName + ".3DS", true);
                //         Response.Redirect("http://aspspider.ws/voidstar69/modellibrary/GetModel.aspx?model=" + modelName + ".3DS");
            }

#if USE_MODEL_MEMORY_CACHE

            // Store the 3D model in the memory cache.
            // If the 3D model is already in the memory cache, overwrite it in case the existing model is corrupt.
            if (/* Cache[modelName] == null && */ renderer.Model != null)
            {
                // Insert 3D model into cache with a 1-day sliding expiration.
                Cache.Insert(modelName, renderer.Model, null, Cache.NoAbsoluteExpiration, memoryCacheTime);
                // TODO: can't have a cache dependency on a file on a remote server
                //Cache.Insert(modelName, renderer.Model, new System.Web.Caching.CacheDependency(modelFilePath));
            }

#endif

            // Create an instance of this model in the scene.
            renderer.Instances.Add(new Instance(renderer.Model)
                {
                    Position = new Vector(0.0, 0.0, objectDepth), // position of object (in view space)
                    Yaw = yawDegrees / 180.0 * Math.PI,
                    Pitch = pitchDegrees / 180.0 * Math.PI,
                    Roll = rollDegrees / 180.0 * Math.PI
                });

            // Render the 3D model to an in-memory 2D surface.
            var pixels = new int[imageWidth * imageHeight];
            renderer.SetRenderingSurface(imageWidth, imageHeight, pixels);
            if (renderer.rayTrace)
            {
                renderer.AntiAliasResolution = 1;
                // TODO: this is far, far slower than raytracing with one ray per pixel! Optimise this! Adaptive antialiasing?
                //renderer.rayTraceSubPixelRes = antiAliasResolution;
            }
            else
            {
                renderer.AntiAliasResolution = antiAliasResolution;
                renderer.rayTraceSubPixelRes = 1;
            }

            // TODO: buggy!
#if USE_AMBIENT_OCCLUSION_MEMORY_CACHE

        // Retrieve/store Ambient Occlusion data into memory cache
        // TODO: ambient occlusion currently only generated and used by raytracing
        if (rayTrace)
        {
            // Determine the unique key for the ambient occlusion data for this model.
            var aoDataKey = modelName + '_' + ambientOcclusionMemoryCacheVersion + '_' + Renderer.aoCacheSize;


            // TODO: debug why using the cache breaks the AO calcs after a few frames!
            // TODO: verify that when the AO data structure is modified, the cache entry is also modified!
            //var perc = Cache.EffectivePercentagePhysicalMemoryLimit;
            //var priv = Cache.EffectivePrivateBytesLimit;


            // If ambient occlusion data is in memory cache (and we are not invalidating the memory cache), retrieve the cached data.
            if (Cache[aoDataKey] != null && cacheLevel > 0) 
            {
                var data = Cache[aoDataKey] as byte[];
                Assert.IsTrue(data != null, "Ambient occlusion data in memory cache is null");
                renderer.aoCache = data;
            }
            else
            {
                // Store the ambient occlusion data structure into the memory cache. The data structure
                // will initially be empty, but the cache should continue pointing at it as it gets updated.
                // TODO: should store and retrieve data structure size along with raw data.
                Cache.Insert(aoDataKey, renderer.aoCache, null, Cache.NoAbsoluteExpiration, memoryCacheTime);
            }
        }

#endif

            renderer.Render();

            // Copy the 2D surface into a bitmap.
            unsafe
            {
                fixed (void* pointer = pixels)
                {
                    // TODO: use a PixelFormat.Format32bppRgb to discard alpha channel (which is always transparent)?
                    bitmap = new Bitmap(imageWidth, imageHeight, imageWidth * 4, PixelFormat.Format32bppArgb, (IntPtr)pointer);
                };
            };

        } // dispose of the renderer (to free up virtual memory used by memory-mapped files)

#if SUPPRESS_EXCEPTION

        }
        catch(System.Threading.ThreadAbortException)
        {
            // HttpResponse.End throws this exception on success!
            throw;
        }
        catch (Exception ex)
        {
            exceptionCaught = true;
            Logger.Log("{0}", ex);

            // Send back a blank image to indicate an error
            bitmap = new Bitmap(imageWidth, imageHeight);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.FillRectangle(Brushes.Red, 0, 0, imageWidth, imageHeight);
            }
        }

#endif

        using(bitmap)
//        using (Bitmap bitmap = new Bitmap(imageWidth, imageHeight))
        {
/*
            // Copy raw pixel colors from surface to a bitmap.
            for (int y = 0; y < imageHeight; y++)
            {
                for (int x = 0; x < imageWidth; x++)
                {
                    int packedColor = pixels[y * imageWidth + x];
                    byte r = (byte)(packedColor >> 16);
                    byte g = (byte)(packedColor >> 8);
                    byte b = (byte)(packedColor);
                    Color color = Color.FromArgb(255, r, g, b);
                    bitmap.SetPixel(x, y, color);
                }
            }
*/

#if USE_RENDER_MEMORY_CACHE

            // Store the render image in the memory cache.
            // If the render image is already in the memory cache, overwrite it in case the existing image is corrupt.
            // TODO: this is inefficient, because the image has to be converted to a JPEG.
            // TODO: can we get here if the image is already in the cache? Probably with cache=0
            // TODO: note that we do cache the error image here!
            //if (Cache[renderImageKey] == null)
            {
                // Convert the render image to a JPEG image, and get hold of the raw bytes of the JPEG.
                byte[] jpegImageBytes;
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, bitmapFormat);
                    jpegImageBytes = stream.GetBuffer();
                }

                // Insert the render image JPEG into the cache with a 1-day sliding expiration.
                Cache.Insert(renderImageKey, jpegImageBytes, null, Cache.NoAbsoluteExpiration, memoryCacheTime);
            }

#endif

#if USE_RENDER_DISK_CACHE

            // Cache the rendered image on disk as a bitmap.
            // Don't cache error image to disk
            if (imageWidth <= maxRenderFileCacheImageWidth && !exceptionCaught)
            {
                // Ensure that the directory for cached renders for this model exists.
                if (!Directory.Exists(renderCacheDir))
                {
                    Directory.CreateDirectory(renderCacheDir);
                }

                // Cache this render onto disk.
                bitmap.Save(renderCacheFile, bitmapFormat);
            }

#endif

#if SHOW_LOG

            if (exceptionCaught)
            {
                // Write the log onto the image for diagnostics. Note that the cached image does not include the logging.
                using (var graphics = Graphics.FromImage(bitmap))
                using (var font = new Font(FontFamily.GenericSansSerif, 8))
                {
                    graphics.DrawString(logText.ToString(), font, Brushes.White, 0, 0);
                }
            }

#endif

            // Send the bitmap back to the browser

/*
            using (FileStream stream = new FileStream(dir + filename, FileMode.Create))
            {
                bitmap.Save(Response.OutputStream, bitmapFormat);
            }
*/

            // TODO: these don't seem to have any effect when testing locally.
            Response.Cache.SetCacheability(System.Web.HttpCacheability.Public);
            Response.Cache.SetMaxAge(new TimeSpan(1, 0, 0, 0)); // 1 day
            Response.Cache.VaryByParams.IgnoreParams = false;

            // change Response Type to an image and stream to the browser
            Response.ContentType = bitmapContentType;
            // TODO: an error occurs here for PNG files. Does using a pixel format of Format32bppRgb fix this error?
            bitmap.Save(Response.OutputStream, bitmapFormat);
            Response.End();

            // TODO: the web server should dispose of Renderer every frame, to free up memory-mapped file views
        }
    }

    #endregion

    #region Private methods

    private static bool SendImageFromMemCache(string renderImageKey, Cache cache, HttpResponse response)
    {
        // Check the memory cache in case this image has already been rendered.
        var renderJpegBytes = cache[renderImageKey] as byte[];
        if (renderJpegBytes != null)
        {
            // Render bitmap is in cache, so reuse it instead of touching the file cache or rendering.

            // TODO: these don't seem to have any effect when testing locally.
            response.Cache.SetCacheability(System.Web.HttpCacheability.Public);
            response.Cache.SetMaxAge(new TimeSpan(1, 0, 0, 0)); // 1 day
            response.Cache.VaryByParams.IgnoreParams = false;

            // Yes so send the cached render JPEG to the browser instead of re-rendering.
            response.ContentType = bitmapContentType;
            response.BinaryWrite(renderJpegBytes);

            //using (var stream = new MemoryStream())
            //{
            //    renderBitmap.Save(stream, bitmapFormat);
            //    Response.BinaryWrite(stream.GetBuffer());
            //}

            response.End();
            return true;
        }
        return false;
    }

    private static bool SendImageFromDiskCache(string renderCacheFile, HttpResponse response)
    {
        // Does the cached render exist?
        if (File.Exists(renderCacheFile))
        {
            // TODO: these don't seem to have any effect when testing locally.
            response.Cache.SetCacheability(System.Web.HttpCacheability.Public);
            response.Cache.SetMaxAge(new TimeSpan(1, 0, 0, 0)); // 1 day
            response.Cache.VaryByParams.IgnoreParams = false;

            // TODO: load render file from file cache into in-memory cache? Which is faster?

            // Yes so send the cached render to the browser instead of re-rendering.
            response.ContentType = bitmapContentType;
            response.WriteFile(renderCacheFile);
            response.End();
            return true;
        }
        return false;
    }

    private void GetParam(string paramName, ref string paramVar)
    {
        if (Request[paramName] != null)
        {
            paramVar = Request[paramName];
        }
    }

    private void GetParam(string paramName, ref int paramVar)
    {
        if (Request[paramName] != null)
        {
            int.TryParse(Request[paramName], out paramVar);
        }
    }

    private void GetParam(string paramName, ref double paramVar)
    {
        if (Request[paramName] != null)
        {
            double.TryParse(Request[paramName], out paramVar);
        }
    }

    private void GetParam(string paramName, ref bool paramVar)
    {
        if (Request[paramName] != null)
        {
            bool.TryParse(Request[paramName], out paramVar);
        }
    }

    private static double WrapAngleDegrees(double angle)
    {
        angle = angle % 360.0;
        if (angle < 0)
        {
            angle += 360.0;
        }
        return angle;
    }

    private static string RemoveFileExtension(string filePath)
    {
        int index = filePath.LastIndexOf('.');
        if(index == -1)
        {
            return filePath;
        }
        else
        {
            return filePath.Substring(0, index);
        }
    }

    #endregion
}
