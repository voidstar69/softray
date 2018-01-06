using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Engine3D;

namespace Raytracer
{
    public partial class Page : UserControl
    {
        #region Data Members

        private const double sceneDepth = 4.0;

        // Object rotation speed.
        private const double rotationSpeed = 0.5;

        // Raytracing parameters.
        private static bool rayTrace = true;
        private static bool rayTraceSubdivision = true;
        //private static bool rayTraceShadows = false;
        private static bool rayTraceLightField = false;
        // Raytrace each frame progressively, to provide interactive feedback of very slow raytracing?
        private static bool progressiveRayTrace = false;
        private const int progressiveRayTraceNumRows = 25;

        // Misc
        private const int margin = 0;
        private const int tickPeriodMs = 10; // frame rate = 1000 / tick period in milliseconds

        // The rendering surface, and the associated image that displays it.
        private WriteableBitmap renderSurface;
        private Image imageThatShowsRenderSurface;

        // The viewport dimensions. This may be smaller than the visible area of the page.
        private int viewportWidth;
        private int viewportHeight;

        // The renderer.
        private readonly Renderer _renderer = new Renderer();

        // The instance of a model in the scene.
        private Instance _instance;

        #endregion

        #region Initialization

        internal Page()
        {
            InitializeComponent();
            InitLogger();
            InitLinks();
            InitRenderer();
            InitViewport();
        }

        private void InitRenderer()
        {
            _renderer.showModelLoadProgressBar = true;
            _renderer.BackgroundColor = 0xff1f1f1f; // quarter grey
            //_renderer.BackgroundColor = 0xffff00ff; // purple
            _renderer.rayTrace = rayTrace;
            //_renderer.rayTraceShadows = rayTraceShadows;
            _renderer.rayTraceShadowsStatic = false;
            _renderer.rayTraceSubdivision = rayTraceSubdivision;
            _renderer.rayTraceLightField = rayTraceLightField;
            _renderer.LightFieldStoresTriangles = false;

            // TODO: test this
            _renderer.rayTraceShading = false;
            _renderer.rayTracePathTracing = true;
            _renderer.rayTraceVoxels = true;
            _renderer.rayTraceSubPixelRes = 1;
            _renderer.rayTraceFocalBlur = true; //  true;
            _renderer.rayTraceFocalDepth = sceneDepth;
            _renderer.rayTraceFocalBlurStrength = 10.0;

            // TODO: for debugging
            //renderer.objectPos = new Vector(0.0, 0.0, 5.0);
            //renderer.directionalLight_dir = new Vector(0.5, -0.5, 1.0); // direction of light (in view space)
            //renderer.directionalLight_dir.Normalise();
            //renderer.positionalLight_pos = renderer.objectPos - renderer.directionalLight_dir * 2; // position of light (in view space)
        }

        private void InitViewport()
        {
            // Get the actual size of the Silverlight content area. This may be zero size.
            viewportWidth = (int)Application.Current.Host.Content.ActualWidth;
            viewportHeight = (int)Application.Current.Host.Content.ActualHeight;

            // Choose the size of the viewport.
            if (viewportWidth < 100)
            {
                viewportWidth = 1024;
            }
            if (viewportHeight < 100)
            {
                viewportHeight = 768;
            }
            viewportWidth -= margin * 2;
            viewportHeight -= margin * 2;

            canvas.Width = viewportWidth;
            canvas.Height = viewportHeight;
        }

        private void InitLogger()
        {
            if (GetParamAsBool("debug", false))
            {
                Logger.LogLineEvent += LogLineEventHandler;
            }
            else
            {
                baseCanvas.Children.Remove(FrameInfoTextBox);
                baseCanvas.Children.Remove(LoggerTextBox);
            }
        }

        private void InitLinks()
        {
            string modelName = GetParamAsString("modelName", "cube");
            previewButton.NavigateUri = new Uri("/ModelPreview.aspx?model=" + modelName, UriKind.RelativeOrAbsolute);
        }

        private void InitInstance()
        {
            // TODO: position the camera to lie on the lightfield bounding sphere, for better performance?
            _instance = new Instance(_renderer.Model) { Position = new Vector(0.0, 0.0, sceneDepth), Pitch = -Math.PI / 8.0 };
            _renderer.Instances.Add(_instance);
        }

        #endregion 

        #region Event handlers

        private void Canvas_Loaded(object sender, EventArgs e)
        {
            imageThatShowsRenderSurface = new Image();
            imageThatShowsRenderSurface.Width = viewportWidth;
            imageThatShowsRenderSurface.Height = viewportHeight;
            imageThatShowsRenderSurface.Margin = new Thickness(margin);
            canvas.Children.Add(imageThatShowsRenderSurface);

            if (rayTrace)
            {
                if (rayTraceLightField)
                    resolutionComboBox.SelectedIndex = 5; // resolution 10x
                else
                    resolutionComboBox.SelectedIndex = 4; // was 3; // resolution 4x
            }
            else
            {
                resolutionComboBox.SelectedIndex = 0; // resolution 1x
            }

            StartTimer();
        }

        #endregion

        #region Combo box handlers

        private void LightingComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (lightingComboBox != null)
            {
                _renderer.pointLighting = false;
                _renderer.specularLighting = false;
                switch (lightingComboBox.SelectedIndex)
                {
                    case 0: break;
                    case 1: _renderer.pointLighting = true; break;
                    case 2: _renderer.pointLighting = true; _renderer.specularLighting = true; break;
                    default: Logger.Log("LightingComboBox: item index {0} not expected", lightingComboBox.SelectedIndex); break;
                }
            }
        }

        private void RenderMethodComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (renderMethodComboBox != null)
            {
                rayTrace = true;
                rayTraceLightField = false;

                switch (renderMethodComboBox.SelectedIndex)
                {
                    case 0: rayTrace = false; break;
                    case 1: rayTraceLightField = true; break;
                    case 2: rayTraceSubdivision = true; break;
                    case 3: rayTraceSubdivision = false; break;
                    default: Logger.Log("renderMethodComboBox: item index {0} not expected", renderMethodComboBox.SelectedIndex); break;
                }

                _renderer.rayTrace = rayTrace;
                _renderer.rayTraceSubdivision = rayTraceSubdivision;
                _renderer.rayTraceLightField = rayTraceLightField;
            }
        }

        private void ShadowComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (shadowComboBox != null)
            {
                _renderer.rayTraceShading = true;
                _renderer.rayTraceShadows = false;
                _renderer.rayTraceAmbientOcclusion = false;

                switch (shadowComboBox.SelectedIndex)
                {
                    case 0: _renderer.rayTraceShading = false; break; // no shading
                    case 1: break; // only shading
                    case 2: _renderer.rayTraceShadows = true; _renderer.rayTraceShading = false; break; // only shadows
                    case 3: _renderer.rayTraceShadows = true;  break; // shadows + shading
                    case 4: _renderer.rayTraceAmbientOcclusion = true; _renderer.rayTraceShading = false; break; // ambient occlusion
                    case 5: _renderer.rayTraceAmbientOcclusion = true; _renderer.rayTraceShadows = true; _renderer.rayTraceShading = false; break; // ambient occlusion + shadows
                    case 6: _renderer.rayTraceAmbientOcclusion = true; break; // ambient occlusion + shading
                    case 7: _renderer.rayTraceAmbientOcclusion = true; _renderer.rayTraceShadows = true; break; // ambient occlusion + shadows + shading
                    default: Logger.Log("ShadowComboBox: item index {0} not expected", shadowComboBox.SelectedIndex); break;
                }
            }
        }
        
        private void ResolutionComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (resolutionComboBox != null)
            {
                switch (resolutionComboBox.SelectedIndex)
                {
                    case 0: SetResolution(1); break;
                    case 1: SetResolution(2); break;
                    case 2: SetResolution(3); break;
                    case 3: SetResolution(4); break;
                    case 4: SetResolution(5); break;
                    case 5: SetResolution(10); break;
                    case 6: SetResolution(25); break;
                    case 7: SetResolution(50); break;
                    case 8: SetResolution(100); break;
                    case 9: SetResolution(200); break;
                    default: Logger.Log("ResolutionComboBox: item index {0} not expected", resolutionComboBox.SelectedIndex); break;
                }
            }
        }

        private void SubPixelComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (subPixelComboBox != null)
            {
                switch (subPixelComboBox.SelectedIndex)
                {
                    case 0: _renderer.rayTraceSubPixelRes = 1; break;
                    case 1: _renderer.rayTraceSubPixelRes = 2; break;
                    case 2: _renderer.rayTraceSubPixelRes = 3; break;
                    case 3: _renderer.rayTraceSubPixelRes = 4; break;
                    default: Logger.Log("SubPixelComboBox: item index {0} not expected", subPixelComboBox.SelectedIndex); break;
                }
            }
        }

        #endregion

        #region Private Methods

        private void SetResolution(int resolutionFactor)
        {
            // Divide the viewport into a grid of the desired resolution (rounding up the grid dimensions)
            int pixelWidth = (viewportWidth - 1) / resolutionFactor + 1;
            int pixelHeight = (viewportHeight - 1) / resolutionFactor + 1;
            //MessageBox.Show(string.Format("Viewport: {0}x{1}   Surface: {2}x{3} ({4} pixels)",
            //    viewportWidth, viewportHeight, pixelWidth, pixelHeight, pixelWidth * pixelHeight));
            renderSurface = new WriteableBitmap(pixelWidth, pixelHeight);
            _renderer.SetRenderingSurface(pixelWidth, pixelHeight, renderSurface.Pixels);
            imageThatShowsRenderSurface.Source = renderSurface;
            // Prevent zooming of rendered image.
            //imageThatShowsRenderSurface.Width = pixelWidth;
            //imageThatShowsRenderSurface.Height = pixelHeight;
            progressiveCurrRow = 0;
        }

        private void LogLineEventHandler(string text)
        {
            if (LoggerTextBox.Text.Length > 1500)
            {
                int firstNewLinePos = LoggerTextBox.Text.IndexOf('\n');
                LoggerTextBox.Text = LoggerTextBox.Text.Remove(0, firstNewLinePos + 1);
            }
            LoggerTextBox.Text += text + '\n';
//            MessageBox.Show(text);

//            LoggerTextBox.Width = 700;

//            depthOrderComboBox.SelectedIndex = 3;

//            ComboBoxItem item = (ComboBoxItem)depthOrderComboBox.Items[0];
//            item.Width = 300;
        }

        private string GetParamAsString(string paramName, string defaultValue)
        {
            string paramValue;
            if (Application.Current.Host.InitParams.TryGetValue(paramName, out paramValue))
            {
                return paramValue;
            }
            else
            {
                return defaultValue;
            }
        }

        private bool GetParamAsBool(string paramName, bool defaultValue)
        {
            bool value;
            if (bool.TryParse(GetParamAsString(paramName, null), out value))
            {
                return value;
            }
            else
            {
                return defaultValue;
            }
        }

        private void LoadModel(bool loadModelFromResource)
        {
            if (loadModelFromResource)
            {
                // Load the default 3D model embedded in a resource.
                Logger.Log("Loading model embedded as a resource");
                _renderer.Load3dsModelFromStream(Application.GetResourceStream(new Uri("obj.3ds", UriKind.Relative)).Stream);
            }
            else
            {
                // Load the requested 3D model from a URI.
                string modelName = GetParamAsString("modelName", "cube");
                Logger.Log("Loading model {0} from a URI", modelName);
                _renderer.Load3dsModelFromURI(modelName, new Uri("getmodel.aspx?model=" + modelName, UriKind.Relative));
            }
        }

        private void StartTimer()
        {
            DispatcherTimer myDispatcherTimer = new DispatcherTimer();
            myDispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, tickPeriodMs); // milliseconds 
            myDispatcherTimer.Tick += Each_Tick;
            myDispatcherTimer.Start();
        }

        #endregion

        #region Render loop

        private DateTime prevTickTime = DateTime.Now;
        private bool fatalError = false;
        private int progressiveCurrRow = 0;
        private bool firstTime = true;

        private void Each_Tick(object sender, EventArgs e)
        {
            if (fatalError)
            {
                return;
            }

            if (firstTime)
            {
                LoadModel(GetParamAsBool("loadModelFromResource", false));
                firstTime = false;
            }

            // Has the attempt to load the 3D model from a URL failed?
            if (_renderer.HasModelLoadFailed())
            {
                try
                {
                    // Yes, so attempt to load the 3D model from a resource.
                    LoadModel(true);
                }
                catch (Exception)
                {
                    MessageBox.Show("Error: Could not load requested 3D model or default 3D model");
                    fatalError = true;
                }
            }

            if (_instance == null && _renderer.HasModelCompletedLoading())
            {
                InitInstance();
            }

            // Pick the range of pixels rows to raytrace (either all rows or a single row).
            if (progressiveRayTrace)
            {
                _renderer.rayTraceStartRow = progressiveCurrRow;
                progressiveCurrRow += progressiveRayTraceNumRows;
                _renderer.rayTraceEndRow = progressiveCurrRow - 1;

                if (progressiveCurrRow > _renderer.RenderingSurfaceHeight)
                {
                    progressiveCurrRow = 0;
                }
            }

            _renderer.Render();

            // Only change the scene between frames, not within a single (progressive) frame.
            if (!rayTrace || !progressiveRayTrace || progressiveCurrRow == 0)
            {
                // Calculate the length of time since the last frame.
                DateTime currTickTime = DateTime.Now;
                TimeSpan diff = currTickTime - prevTickTime;
                prevTickTime = currTickTime;
                double deltaTime = diff.TotalSeconds;

                // Change the scene.
                if (_instance != null)
                {
                    _instance.Yaw += rotationSpeed * deltaTime;
                    //_instance.Pitch += rotationSpeed * 0.5 * deltaTime;
                }

                // TODO: these totals are skewed by the many rays that miss the bounding box of the binary tree. Count number of rays that traverse the binary tree seperately?
                double frameRate = (deltaTime == 0.0 ? 99 : 1.0 / deltaTime);
                double raysFired = _renderer.TracedRayCount;
                double avgGeomTestsPerRay = (raysFired == 0 ? 0 : _renderer.NumGeometryTests / raysFired);
                double avgNodeVisitsPerRay = (raysFired == 0 ? 0 : _renderer.NumNodeVisits / raysFired);
                double avgLeafNodeVisitsPerRay = (raysFired == 0 ? 0 : _renderer.NumLeafNodeVisits / raysFired);
                FrameInfoTextBox.Text = string.Format("ray-tri tests: {1}, num rays: {2} ({6} clipped, {7} tree traced), avg tri tests per ray: {3:F1}, avg node visits per ray: {4:F1}, avg leaf node visits per ray: {5:F1}, framerate: {0}",
                    frameRate, _renderer.RayGeometryTestCount, _renderer.NumRaysFired, avgGeomTestsPerRay, avgNodeVisitsPerRay, avgLeafNodeVisitsPerRay, _renderer.ClippedRayCount, _renderer.TracedRayCount);
                _renderer.RayGeometryTestCount = 0;
                _renderer.ClippedRayCount = 0;
                _renderer.TracedRayCount = 0;
                //                Logger.Log("Framerate: {0}", frameRate);
                //                Logger.Log("Render time: {0} seconds per frame", deltaTime);
            }

            renderSurface.Invalidate();
        }

        #endregion
    }
}