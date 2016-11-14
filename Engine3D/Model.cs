using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Net;

// TODO: class Model seems to be quite messy. Use Contracts on it a lot!

namespace Engine3D
{
    // TODO: Making this a class seems to slow down the rendering.
    public struct Vertex
    {
        public Vector pos;
//            public Vector normal;

        public Vertex(double x, double y, double z)
        {
            pos.x = x;
            pos.y = y;
            pos.z = z;
//                normal = new Vector(0.0, 0.0, 1.0);
        }

        public Vertex(Vector pos)
        {
            this.pos = pos;
//                normal = new Vector(0.0, 0.0, 1.0);
        }

/*
        public void SetNormal(Vector normal)
        {
            this.normal = normal;
        }
*/
    }

    // TODO: Does making this a class instead of a struct slow down the rendering?
    public class Triangle
    {
        public int vertexIndex1;
        public int vertexIndex2;
        public int vertexIndex3;
        public int vertexNormalIndex1;
        public int vertexNormalIndex2;
        public int vertexNormalIndex3;
        public Vector centre;

        public Color ambientMaterial;
        public Color diffuseMaterial;
        public Color specularMaterial;
        public double specularMaterialExponent;
        //public uint color;
        //public SalmonViewer.Material material;

        /// <summary>
        /// Construct with sentinel normal indices.
        /// </summary>
        /// <param name="index1"></param>
        /// <param name="index2"></param>
        /// <param name="index3"></param>
        /// <param name="material"></param>
        public Triangle(int index1, int index2, int index3, SalmonViewer.Material material)
            : this(index1, index2, index3, -1, -1, -1, material)
        {
            Contract.Requires(material != null);
        }

        /// <summary>
        /// Construct with valid normal indices.
        /// </summary>
        /// <param name="index1"></param>
        /// <param name="index2"></param>
        /// <param name="index3"></param>
        /// <param name="normalIndex1"></param>
        /// <param name="normalIndex2"></param>
        /// <param name="normalIndex3"></param>
        /// <param name="material"></param>
        public Triangle(int index1, int index2, int index3, int normalIndex1, int normalIndex2, int normalIndex3, SalmonViewer.Material material)
        {
            Contract.Requires(material != null);
            Contract.Requires(material.Ambient.Length == 3);
            Contract.Requires(material.Diffuse.Length == 3);
            Contract.Requires(material.Specular.Length == 3);

            vertexIndex1 = index1;
            vertexIndex2 = index2;
            vertexIndex3 = index3;
            vertexNormalIndex1 = normalIndex1;
            vertexNormalIndex2 = normalIndex2;
            vertexNormalIndex3 = normalIndex3;
            centre = new Vector();

            // Copy colors from SalmonViewer.Material object.
            ambientMaterial = new Color(material.Ambient[0], material.Ambient[1], material.Ambient[2]);
            diffuseMaterial = new Color(material.Diffuse[0], material.Diffuse[1], material.Diffuse[2]);
            specularMaterial = new Color(material.Specular[0], material.Specular[1], material.Specular[2]);
            specularMaterialExponent = material.Shininess;
            //this.material = material;
        }
    }

    // TODO: make this class immutable. The loading should be triggered in the constructor,
    // and there should be no methods that modify state.
    public class Model
    {
        #region Types

        public class ProgressChangedEventArgs : EventArgs
        {
            private readonly double progressPercentage;

            public ProgressChangedEventArgs(double progressPercentage)
            {
                this.progressPercentage = progressPercentage;
            }

            public double ProgressPercentage
            {
                get
                {
                    return progressPercentage;
                }
            }
        }

        public event EventHandler<ProgressChangedEventArgs> ModelLoadProgressEvent = delegate { };

        #endregion

        #region Properties

        public Vector Min { get; private set; }
        public Vector Max { get; private set; }

        /// <summary>
        /// Triangles sorted by the X axis (in object-space), to give a fast, approximate depth ordering
        /// </summary>
        public IList<Triangle> TrianglesInXOrder
        {
            get
            {
                if(_trianglesInXOrder == null)
                {
                    // Sort by centre of triangle
                    _trianglesInXOrder = Triangles.OrderBy(t => t.centre.x).ToList();
                }
                return _trianglesInXOrder;
            }
        }

        /// <summary>
        /// Triangles sorted by the Y axis (in object-space), to give a fast, approximate depth ordering
        /// </summary>
        public IList<Triangle> TrianglesInYOrder
        {
            get
            {
                if (_trianglesInYOrder == null)
                {
                    // Sort by centre of triangle
                    _trianglesInYOrder = Triangles.OrderBy(t => t.centre.y).ToList();
                }
                return _trianglesInYOrder;
            }
        }

        /// <summary>
        /// Triangles sorted by the Z axis (in object-space), to give a fast, approximate depth ordering
        /// </summary>
        public IList<Triangle> TrianglesInZOrder
        {
            get
            {
                if (_trianglesInZOrder == null)
                {
                    // Sort by centre of triangle
                    _trianglesInZOrder = Triangles.OrderBy(t => t.centre.z).ToList();
                }
                return _trianglesInZOrder;
            }
        }

        #endregion

        #region Public data

        public IList<Vertex> Vertices;
        public IList<Vector> Normals;
        public ICollection<Triangle> Triangles;

//        public bool AddGroundPlane = true; // client should set this before calling one of the Load* methods

        public bool LoadingComplete = false; // client must never modify this, only read this
        public bool LoadingError = false; // client must never modify this, only read this

        #endregion

        #region Private date

        // TODO: this is missing most/all of the time!
        private string _modelFileName;

        private const double maxCoordinateSize = 1e6;

        private const int _isolatedStorageQuotaGranularity = 100 * 1024 * 1024;
        private const int _isolatedStorageMinFreeSpace = 25 * 1024 * 1024;

        // Used to buffer downloaded data before writing it to Isolated Storage.
        private const int BufferSize = 256 * 1024;
        private static readonly byte[] Buffer = new byte[BufferSize];

        // Triangles sorted by axis, to give an approximate depth ordering
        // TODO: this wastes memory by duplicating the Triangle structs. Store triangle indices instead?
        private IList<Triangle> _trianglesInXOrder;
        private IList<Triangle> _trianglesInYOrder;
        private IList<Triangle> _trianglesInZOrder;

        #endregion

/*
        // TODO: this is missing most/all of the time!
        // TODO: did not work as a property!
        /// <summary>
        /// Get the file name of the model (with file extension).
        /// Note that this will return null until the model is loaded into memory!
        /// </summary>
        public string GetFileName()
        {
            Contract.Requires(false); // enclosing method always produces an error
            Assert.Fail("Model file name appears to be missing most/all of the time! Debug this before calling Model.GetFileName");
            return _modelFileName;
        }
*/

        public void Load3dsModelFromStream(Stream stream)
        {
            Logger.Log("Load3dsModelFromStream");
            LoadingComplete = false;
            LoadingError = false;
            try
            {
                ModelLoadProgressEvent(this, new ProgressChangedEventArgs(0));
                Load3ds(stream);
                ModelLoadProgressEvent(this, new ProgressChangedEventArgs(100));
            }
            catch (Exception)
            {
                LoadingError = true;
                throw;
            }
        }

/*
        public void LoadObjModelFromXAP(string fileName)
        {
            LoadingComplete = false;
            using (Stream stream = Application.GetResourceStream(new Uri(fileName, UriKind.Relative)).Stream)
            {
                LoadObj(stream);
            }
        }

        public void Load3dsModelFromXAP(string fileName)
        {
            Logger.Log("Load3dsModelFromXAP {0}", fileName);

            LoadingComplete = false;
            using (Stream stream = Application.GetResourceStream(new Uri(fileName, UriKind.Relative)).Stream)
            {
                Load3ds(stream);
            }
        }
  
        public void LoadObjModelFromURI(string modelName, Uri uri)
        {
            LoadingComplete = false;
            _modelFileName = modelName + ".obj";

            try
            {
                using (IsolatedStorageFile isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (isolatedStorage.Quota < _isolatedStorageQuotaGranularity)
                    {
                        isolatedStorage.IncreaseQuotaTo(_isolatedStorageQuotaGranularity);
                    }

                    if (isolatedStorage.FileExists(_modelFileName))
                    {
                        using (IsolatedStorageFileStream stream = new IsolatedStorageFileStream(_modelFileName, FileMode.Open, isolatedStorage))
                        {
                            LoadObj(stream);
                        }
                        return;
                    }
                }
            }
            catch (IsolatedStorageException ex)
            {
                Logger.Log("LoadObjModelFromURI IsolatedStorageException: " + ex.Message);
            }

            WebClient webClient = new WebClient();
            webClient.DownloadStringCompleted += DownloadObjStringCompleted;
            try
            {
                webClient.DownloadStringAsync(uri);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Logger.Log("LoadObjModelFromURI DownloadStringAsync exception: " + ex);
            }
        }
*/

        public void Load3dsModelFromURI(string modelFileName, Uri uri)
        {
            LoadingComplete = false;
            LoadingError = false;
            _modelFileName = modelFileName;

            try
            {
                using (IsolatedStorageFile isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (isolatedStorage.AvailableFreeSpace < _isolatedStorageMinFreeSpace)
                    {
                        long newQuota = isolatedStorage.Quota + _isolatedStorageQuotaGranularity;
                        Logger.Log("Load3dsModelFromURI: increasing isolated storage quota to {0} megabytes", newQuota / 1024.0 / 1024.0);
                        // TODO: this call fails unless triggered from a UI action!
                        if (isolatedStorage.IncreaseQuotaTo(newQuota))
                        {
                            Logger.Log("Load3dsModelFromURI: isolated storage quota increased successfully");
                        }
                        else
                        {
                            Logger.Log("Load3dsModelFromURI: failed to increase isolated storage quota");
                        }
                    }

                    Logger.Log("Load3dsModelFromURI: looking for 3DS file in isolated storage: {0}", _modelFileName);
                    if (isolatedStorage.FileExists(_modelFileName))
                    {
                        Logger.Log("Load3dsModelFromURI: loading 3DS file from isolated storage: {0}", _modelFileName);
                        using (IsolatedStorageFileStream stream = new IsolatedStorageFileStream(_modelFileName, FileMode.Open, isolatedStorage))
                        {
                            Load3ds(stream);
                        }
                        Logger.Log("Load3dsModelFromURI: done loading 3DS file from isolated storage");
                        return;
                    }
                    else
                    {
                        Logger.Log("Load3dsModelFromURI: file does not exist: {0}", _modelFileName);
                    }
                }
            }
            catch (IsolatedStorageException ex)
            {
                Logger.Log("Load3dsModelFromURI IsolatedStorageException: " + ex.Message);
            }

            Logger.Log("Load3dsModelFromURI: opening URI for stream read: {0}", uri.ToString());
            WebClient webClient = new WebClient();
            //webClient.AllowReadStreamBuffering = false; // TODO: only allowed on a secondary thread. Create one.
            webClient.DownloadProgressChanged += OpenRead3dsProgressChanged;
            webClient.OpenReadCompleted += OpenRead3dsCompleted;
            try
            {
                ModelLoadProgressEvent(this, new ProgressChangedEventArgs(0));
                webClient.OpenReadAsync(uri);
            }
            catch (Exception ex)
            {
                LoadingError = true;
//                MessageBox.Show(ex.Message);
                Logger.Log("Load3dsModelFromURI OpenReadAsync exception: " + ex);
            }
        }

/*
        private void DownloadObjStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            // TODO: e.Cancelled or e.Error throws a security exception on the dev box
            if (e.Cancelled)
            {
                MessageBox.Show("DownloadStringAsync OBJ Cancelled");
                Logger.Log("DownloadStringAsync OBJ Cancelled");
            }
            else if (e.Error != null)
            {
                MessageBox.Show("DownloadStringAsync OBJ Error: " + e.Error.ToString());
                Logger.Log("DownloadStringAsync OBJ Error: " + e.Error.ToString());
            }
            else
            {
                try
                {
                    using (IsolatedStorageFile isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        if (!isolatedStorage.FileExists(_modelFileName))
                        {
                            // Write data to file in isolated storage.
                            using (IsolatedStorageFileStream isfs = new IsolatedStorageFileStream(_modelFileName, FileMode.Create, isolatedStorage))
                            {
                                using (StreamWriter sw = new StreamWriter(isfs))
                                {
                                    sw.Write(e.Result);
                                }
                            }
                        }
                    }
                }
                catch (IsolatedStorageException ex)
                {
                    Logger.Log("DownloadObjStringCompleted IsolatedStorageException: " + ex.Message);
//                    isolatedStorage.DeleteFile(_modelFileName);
                }

                try
                {
                    using (StringReader reader = new StringReader(e.Result))
                    {
                        LoadObj(reader);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    Logger.Log("DownloadObjStringCompleted exception: " + ex);
                }
            }
        }
*/

        private void OpenRead3dsProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            ModelLoadProgressEvent(this, new ProgressChangedEventArgs(e.BytesReceived * 100.0 / e.TotalBytesToReceive));
        }

        private void OnModelLoadProgressUpdate(object sender, SalmonViewer.ThreeDSFile.ProgressChangedEventArgs e)
        {
            ModelLoadProgressEvent(this, new ProgressChangedEventArgs(100));
        }

        private void OpenRead3dsCompleted(object sender, OpenReadCompletedEventArgs e)
        {
            // TODO: e.Cancelled or e.Error throws a security exception on the dev box
            if (e.Cancelled)
            {
                LoadingError = true;
//                MessageBox.Show("OpenReadAsync 3DS Cancelled");
                Logger.Log("OpenReadAsync 3DS Cancelled");
                return;
            }
            else if (e.Error != null)
            {
                LoadingError = true;
//                MessageBox.Show("OpenReadAsync 3DS Error: " + e.Error.ToString());
                Logger.Log("OpenReadAsync 3DS Error: " + e.Error.ToString());
                return;
            }

            try
            {
                

                using (Stream inputStream = e.Result)
                {
                    ModelLoadProgressEvent(this, new ProgressChangedEventArgs(100));
                    Logger.Log("OpenRead3dsCompleted: about to parse 3DS network stream: {0}", _modelFileName);
//                    inputStream.Seek(0, SeekOrigin.Begin);
                    Load3ds(inputStream);
                    Logger.Log("OpenRead3dsCompleted: 3DS network stream parsed");
                    ModelLoadProgressEvent(this, new ProgressChangedEventArgs(100));

                try
                {
                    using (IsolatedStorageFile isolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        if (isolatedStorage.AvailableFreeSpace > inputStream.Length)
                        {
                            // Write data to file in isolated storage.
                            Logger.Log("OpenRead3dsCompleted: writing 3DS model to isolated storage: {0}", _modelFileName);
                            using (IsolatedStorageFileStream outputStream =
                                new IsolatedStorageFileStream(_modelFileName, FileMode.Create, isolatedStorage))
                            {
                                // Reset input stream to the beginning.
                                inputStream.Seek(0, SeekOrigin.Begin);

                                int numBytesRead;
                                while (0 != (numBytesRead = inputStream.Read(Buffer, 0, BufferSize)))
                                {
                                    outputStream.Write(Buffer, 0, numBytesRead);
                                }
                            }
                            Logger.Log("OpenRead3dsCompleted: done writing 3DS model to isolated storage");
                        }
                    }
                }
                catch (IsolatedStorageException ex)
                {
                    Logger.Log("OpenReadAsyncCompleted IsolatedStorageException: " + ex.Message);
//                    isolatedStorage.DeleteFile(_modelFileName);
                }

                }
            }
            catch (Exception)
            {
                LoadingError = true;
                throw;
            }
        }

        private void Load3ds(Stream stream)
        {
            Logger.Log("Load3ds start");

            if (stream == null)
            {
//                MessageBox.Show("Load3ds: stream is null");
                return;
            }

            // TODO: get a model file name from somewhere!
            _modelFileName = "unknown";

            Vertices = new List<Vertex>();
            Normals = new List<Vector>();
            Triangles = new List<Triangle>();
            Min = new Vector(double.MaxValue, double.MaxValue, double.MaxValue);
            Max = new Vector(double.MinValue, double.MinValue, double.MinValue);

            Logger.Log("Load3ds ThreeDSFile");

            SalmonViewer.ThreeDSFile loader = new SalmonViewer.ThreeDSFile();
            loader.ModelLoadProgressEvent += OnModelLoadProgressUpdate;
            loader.LoadModel(stream);

            // TODO: These null checks may be unnecessary
            if (loader.Model.Entities == null)
            {
//                MessageBox.Show("Load3ds: loader.Model.Entities is null");
                return;
            }
            if (loader.Model.Entities.Count == 0)
            {
                throw new FormatException("No entities in model. 3DS file may be corrupt.");
            }

            Logger.Log("Load3ds merge entities and post-process model");

            foreach (SalmonViewer.Entity entity in loader.Model.Entities)
            {
                // Verify vertices and indices are not missing or corrupt.
                if (entity.vertices == null)
                {
                    throw new ArgumentNullException("entity.vertices", "3DS file may be corrupt.");
                }
                if (entity.vertices.Length < 3)
                {
                    throw new FormatException("Entity has less than 3 vertices. 3DS file may be corrupt.");
                }

                if (entity.triangles == null)
                {
                    throw new ArgumentNullException("entity.indices", "3DS file may be corrupt.");
                }
                if (entity.triangles.Length == 0)
                {
                    throw new FormatException("Entity has no triangles. 3DS file may be corrupt.");
                }

                // Calculate offsets to apply to vertex and normal indicies for merging this entity into the global geometry lists.
                int vertexOffset = Vertices.Count;
                int normalOffset = Normals.Count;

                foreach (SalmonViewer.Vector v in entity.vertices)
                {
                    double x = v.X;
                    double y = v.Y;
                    double z = v.Z;

                    // If any coordinate is not-a-number, or (positive or negative) infinity, set the coordinate
                    // to a finate number so that the box bounding the model does not become infinite in size.
                    if (double.IsNaN(x) || double.IsInfinity(x) || Math.Abs(x) > maxCoordinateSize)
                    {
                        x = 0.0;
                    }
                    if (double.IsNaN(y) || double.IsInfinity(y) || Math.Abs(y) > maxCoordinateSize)
                    {
                        y = 0.0;
                    }
                    if (double.IsNaN(z) || double.IsInfinity(z) || Math.Abs(z) > maxCoordinateSize)
                    {
                        z = 0.0;
                    }

                    Vertices.Add(new Vertex(x, y, z));
                    Min = new Vector(Math.Min(Min.x, x), Math.Min(Min.y, y), Math.Min(Min.z, z));
                    Max = new Vector(Math.Max(Max.x, x), Math.Max(Max.y, y), Math.Max(Max.z, z));
                }

                if (entity.normals != null)
                {
                    foreach (SalmonViewer.Vector n in entity.normals)
                    {
                        Normals.Add(new Vector(n.X, n.Y, n.Z));
                    }
                }

                foreach (SalmonViewer.Triangle t in entity.triangles)
                {
/*
                    // Ensure that this triangle's material is not too dark.
                    float[] diffuse = t.material.Diffuse;
                    Vector color = new Vector(diffuse[0], diffuse[1], diffuse[2]);
                    if (color.Length < 0.3)
                    {
                        if (color.Length == 0.0)
                        {
                            color = new Vector(0.3, 0.3, 0.3);
                        }
                        else
                        {
                            color *= 0.5 / color.Length;
                        }
                        entity.material.Diffuse = new float[] { (float)color.x, (float)color.y, (float)color.z };
                    }
*/

                    // Copy triangle's vertex indicies. The same indices are used to index into the normal collection.
                    Triangles.Add(new Triangle(vertexOffset + t.Vertex1, vertexOffset + t.Vertex2, vertexOffset + t.Vertex3,
                                                normalOffset + t.Vertex1, normalOffset + t.Vertex2, normalOffset + t.Vertex3,
                                                t.material));
                }
            }

            PostProcessGeometry();
            LoadingComplete = true;
            LoadingError = false;

            Logger.Log("Model has {0} vertices, {1} normals, {2} triangles and {3} materials", Vertices.Count, Normals.Count, Triangles.Count, loader.Materials.Count);
//            Logger.Log("Model extends from ({0},{1},{2}) to ({3},{4},{5})", Min.x, Min.y, Min.z, Max.x, Max.y, Max.z);
            Logger.Log("Load3ds end");
        }

/*
        private void LoadObj(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream))
            {
                LoadObj(reader);
            }
        }

        private void LoadObj(TextReader reader)
        {
            Vertices = new List<Vertex>();
            Normals = new List<Vector>();
            Triangles = new List<Triangle>();
            Min = new Vector(double.MaxValue, double.MaxValue, double.MaxValue);
            Max = new Vector(double.MinValue, double.MinValue, double.MinValue);

            Random random = new Random();
            string line;
            while (null != (line = reader.ReadLine()))
            {
                char[] separator = { ' ' };
                string[] tokens = line.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 4)
                {
                    continue;
                }

                switch (tokens[0])
                {
                    case "v": // Vertex position
                        Vertex v = new Vertex(double.Parse(tokens[1]), double.Parse(tokens[2]), double.Parse(tokens[3]));
                        Vertices.Add(v);
                        Min.x = Math.Min(Min.x, v.pos.x);
                        Min.y = Math.Min(Min.y, v.pos.y);
                        Min.z = Math.Min(Min.z, v.pos.z);
                        Max.x = Math.Max(Max.x, v.pos.x);
                        Max.y = Math.Max(Max.y, v.pos.y);
                        Max.z = Math.Max(Max.z, v.pos.z);
                        break;
                    case "vn": // Vertex normal
                        Vector normal = new Vector(double.Parse(tokens[1]), double.Parse(tokens[2]), double.Parse(tokens[3]));
                        normal.Normalise();
                        Normals.Add(normal);
                        break;
                    case "f": // Face
                        string[] v1 = tokens[1].Split('/');
                        string[] v2 = tokens[2].Split('/');
                        string[] v3 = tokens[3].Split('/');

                        int index1 = int.Parse(v1[0]) - 1;
                        int index2 = int.Parse(v2[0]) - 1;
                        int index3 = int.Parse(v3[0]) - 1;

                        // Are we missing normals for any vertex of this face?
                        Triangle tri;
                        if (v1.Length < 3 || v2.Length < 3 || v3.Length < 3)
                        {
                            // Yes, so create a triangle without any normals
                            tri = new Triangle(index1, index2, index3);
                        }
                        else
                        {
                            // No, so create a triangle with normals
                            // TODO: We reverse the order of vertices here to flip the normal. This should actually be fixed elsewhere.
                            int normalIndex1 = int.Parse(v1[2]) - 1;
                            int normalIndex2 = int.Parse(v2[2]) - 1;
                            int normalIndex3 = int.Parse(v3[2]) - 1;
                            tri = new Triangle(index1, index2, index3,
                                               normalIndex1, normalIndex2, normalIndex3);
                        }

                        Triangles.Add(tri);
                        break;
                }
            }

            PostProcessGeometry();
            LoadingComplete = true;
            LoadingError = false;
        }
*/

        protected void CalcExtent()
        {
            Min = new Vector(double.MaxValue, double.MaxValue, double.MaxValue);
            Max = new Vector(double.MinValue, double.MinValue, double.MinValue);

            foreach (var v in Vertices)
            {
                Min = new Vector(Math.Min(Min.x, v.pos.x), Math.Min(Min.y, v.pos.y), Math.Min(Min.z, v.pos.z));
                Max = new Vector(Math.Max(Max.x, v.pos.x), Math.Max(Max.y, v.pos.y), Math.Max(Max.z, v.pos.z));
            }
        }

        protected void PostProcessGeometry()
        {
            Logger.Log("PostProcessGeometry");

/*
            if (AddGroundPlane)
            {
                // Add a ground plane to the model. This will be scaled along with the model, but will not affect the amount of scaling.
                AddGroundPlaneToModel(1.0); // Max.y);
            }
*/

            // Scale and translate vertex positions to fit into the unit cube
            // Also convert List to Array for better cache coherency.
            ICollection<Vertex> preVertices = Vertices;
            Vertex[] postVertices = new Vertex[Vertices.Count];
            Vector centre = new Vector((Min.x + Max.x) / 2, (Min.y + Max.y) / 2, (Min.z + Max.z) / 2);
            Vector extent = new Vector(Max.x - Min.x, Max.y - Min.y, Max.z - Min.z);
            Contract.Assert(!extent.IsZeroVector);
            double scaleFactor = 1.0 / Math.Max(Math.Max(extent.x, extent.y), extent.z);
            int index = 0;
            foreach(Vertex preVertex in preVertices)
            {

                // Scale such that major axis is unit length, and model is centred at origin.
                Vertex postVertex = new Vertex((preVertex.pos - centre) * scaleFactor);

                    //(preVertex.pos.x - centre.x) * scaleFactor,
                    //(preVertex.pos.y - centre.y) * scaleFactor,
                    //(preVertex.pos.z - centre.z) * scaleFactor);

                // Scale such that all 3 axes are unit length.
                //                    v.x = (v.x - Min.x) / (Max.x - Min.x) - 0.5;
                //                    v.y = (v.y - Min.y) / (Max.y - Min.y) - 0.5;
                //                    v.z = (v.z - Min.z) / (Max.z - Min.z) - 0.5;

                postVertices[index++] = postVertex;
            }
            Min = (Min - centre) * scaleFactor;
            Max = (Max - centre) * scaleFactor;
            Vertices = postVertices;

            // Convert List to Array for better cache coherency.
            Vector[] normalArray = new Vector[Normals.Count];
            Normals.CopyTo(normalArray, 0);
            Normals = normalArray;

            // Ensure that all normals are unit length
            for (int i = 0; i < normalArray.Length; i++)
            {
                // TODO: use Vector.IsZeroVector
                if (normalArray[i].Length < 0.001)
                {
                    normalArray[i] = new Vector(0.0, 0.0, -1.0);
                }
                else
                {
                    normalArray[i].Normalise();
                }
            }

            // Convert List to Array for better cache coherency.
            Triangle[] triArray = new Triangle[Triangles.Count];
            Triangles.CopyTo(triArray, 0);
            Triangles = triArray;

            // Calculate the centre of every triangle.
            for (int i = 0; i < triArray.Length; i++)
            {
                Triangle tri = triArray[i];
                centre = new Vector(0.0, 0.0, 0.0);
                centre += postVertices[tri.vertexIndex1].pos;
                centre += postVertices[tri.vertexIndex2].pos;
                centre += postVertices[tri.vertexIndex3].pos;
                centre /= 3.0;
                Contract.Assert(!centre.ContainsNaN && !centre.ContainsInfinity);
                triArray[i].centre = centre;
            }

//            MessageBox.Show("Stats: " + vertices.Count + " vertices, " + normals.Count + " vertex normals, " + triangles.Count + " triangles");
//            MessageBox.Show(string.Format("{0}, {1}, {2}", vertices[0].normal.x, vertices[0].normal.y, vertices[0].normal.z));
        }

/*
        private void AddGroundPlaneToModel(double height)
        {
            const double size = 100.0;

            int vertIndex = Vertices.Count;
            Vertices.Add(new Vertex(-size, height, -size));
            Vertices.Add(new Vertex(-size, height, +size));
            Vertices.Add(new Vertex(+size, height, +size));
            Vertices.Add(new Vertex(+size, height, -size));

            int normIndex = Normals.Count;
            Normals.Add(new Vector(0.0, -1.0, 0.0));
            Normals.Add(new Vector(0.0, -1.0, 0.0));
            Normals.Add(new Vector(0.0, -1.0, 0.0));
            Normals.Add(new Vector(0.0, -1.0, 0.0));

            SalmonViewer.Material material = new SalmonViewer.Material();
            material.Diffuse = new float[] { 1.0f, 1.0f, 1.0f };

            Triangles.Add(new Triangle(vertIndex, vertIndex + 1, vertIndex + 2, normIndex, normIndex + 1, normIndex + 2, material));
            Triangles.Add(new Triangle(vertIndex, vertIndex + 2, vertIndex + 3, normIndex, normIndex + 2, normIndex + 3, material));
        }
*/ 
    }
}