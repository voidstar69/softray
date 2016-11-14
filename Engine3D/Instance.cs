using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Engine3D
{
    /// <summary>
    /// An instance of a 3D model, i.e. a 3D object.
    /// </summary>
    public class Instance
    {
        #region Private data

        private Matrix _transform = new Matrix();
        private Matrix _inverseTransform = new Matrix();

        #endregion

        #region Constructor

        public Instance(Model model)
        {
            Contract.Requires(model != null);

            Model = model;

            // Choose default camera Field Of View
            FieldOfViewDepth = 0.5; // 90 degree FOV

            // Choose default object position (in view space).
            Position = new Vector(0.0, 0.0, 1.5);

            // TODO: some of this is not required for raytracing. Evaluate these lazily?

            // Size the view-space vertex and normal lists to have the same number of vertices and normals as the 3D model.
            ViewSpaceVertices = new Vector[model.Vertices.Count];
            ViewSpaceNormals = new Vector[model.Normals.Count];
            VertexLightIntensity = new double[model.Normals.Count];
        }

        #endregion

        #region Properties

        public Model Model { get; private set; }

        public Vector Position { get; set; }   // position of object (in view space)

        public double Yaw { get; set; }         // in radians
        public double Pitch { get; set; }       // in radians
        public double Roll { get; set; }        // in radians

        /// <summary>
        /// Depth of the camera's Field Of View.
        /// x and y vary from -0.5 to +0.5 in view space, so a depth of 0.5 gives a 90 degree FOV
        /// </summary>
        public double FieldOfViewDepth { get; set; }

        // TODO: make these into more generic types
        public Vector[] ViewSpaceVertices { get; private set; }
        public Vector[] ViewSpaceNormals { get; private set; }
        public double[] VertexLightIntensity { get; private set; } // TODO: change from intensity to color

        /// <summary>
        /// Euler angles: pitch, yaw, roll (in radians)
        /// </summary>
        public Vector EulerAngles
        {
            get
            {
                return new Vector(Pitch, Yaw, Roll);
            }
            set
            {
                Pitch = value.x;
                Yaw = value.y;
                Roll = value.z;
            }
        }

        /// <summary>
        /// Get the triangles of the model (in undefined order).
        /// </summary>
        public IEnumerable<Triangle> Triangles
        {
            get
            {
                return Model.Triangles;
            }
        }

        /// <summary>
        /// Get the triangles of the model in roughly view-space depth order (based on viewSpaceVertices).
        /// </summary>
        public IEnumerable<Triangle> TrianglesInDepthOrder
        {
            get
            {
                // TODO: how to ensure that viewSpaceVertices has been updated?
                // TODO: cache results of sorting?
                return Model.Triangles.OrderByDescending(t => ViewSpaceVertices[t.vertexIndex1].z);
                // TODO: centre is in object space, not view space!
                //_model.Triangles.OrderBy(t => t.centre);
            }
        }

/*
        /// <summary>
        /// Perform a single pass of a bubble sort to keep the triangles in roughly depth order.
        /// </summary>
        private static void SortTriangles_SinglePass(IList<Triangle> triangles, IList<Vertex> vertices)
        {
            for (int i = 0; i < triangles.Count - 1; i++)
            {
                Triangle triA = triangles[i];
                Triangle triB = triangles[i + 1];
                if (vertices[triA.vertexIndex1].pos.z > vertices[triB.vertexIndex1].pos.z)
                {
                    // Swap triangles
                    triangles[i] = triB;
                    triangles[i + 1] = triA;
                }
            }
        }
*/

        #endregion

        #region Public methods

        public void InitRender(Surface.CalcLightingCallback calcLightingFunc)
        {
            // Create forward and reverse transformation matrices.
            _transform = Matrix.MakeTranslationMatrix(Position) * Matrix.MakeRollMatrix(Roll) * Matrix.MakePitchMatrix(Pitch) * Matrix.MakeYawMatrix(Yaw);
            _inverseTransform = Matrix.MakeYawMatrix(-Yaw) * Matrix.MakePitchMatrix(-Pitch) * Matrix.MakeRollMatrix(-Roll) * Matrix.MakeTranslationMatrix(-Position);

            // Transform every vertex in the model to view space.
            int index = 0;
            foreach (Vertex vertex in Model.Vertices)
            {
                ViewSpaceVertices[index++] = TransformPosToView(vertex.pos);
            }

            // Transform every normal in the model to view space.
            index = 0;
            foreach (Vector normal in Model.Normals)
            {
                Assert.IsTrue(normal.IsUnitVector, "normal is not a unit vector");
                ViewSpaceNormals[index] = TransformDirection(normal);
                Assert.IsTrue(ViewSpaceNormals[index].IsUnitVector, "view-space normal is not a unit vector");
                index++;
            }

            // Calculate lighting for every vertex in the model.
            // TODO: This assumes that the N'th normal belongs to the N'th vertex. This is true for 3DS files, but not OBJ files!
            for (int i = 0; i < Model.Normals.Count; i++)
            {
                VertexLightIntensity[i] = calcLightingFunc(ViewSpaceVertices[i], ViewSpaceNormals[i]);
            } 
        }

        // TODO: view space is post-projection, which breaks the lighting. Redefine view space to be pre-projection!
        /// <summary>
        /// Transform a position vector from model space to view space.
        /// </summary>
        /// <remarks>Model space is a Left-Handed coordinate system.</remarks>
        /// <remarks>View space is a Left-Handed coordinate system with X+ve to the right, Y+ve up and Z+ve forward.</remarks>
        public Vector TransformPosToView(Vector pos)
        {
            // Rotate, scale and translate vector.
            var v = Matrix.Multiply3X4(_transform, pos);

            //var v = new Vector(
            //    pos.x * transform[0, 0] + pos.y * transform[0, 1] + pos.z * transform[0, 2] + transform[0, 3],
            //    pos.x * transform[1, 0] + pos.y * transform[1, 1] + pos.z * transform[1, 2] + transform[1, 3],
            //    pos.x * transform[2, 0] + pos.y * transform[2, 1] + pos.z * transform[2, 2] + transform[2, 3]);

            // Project vector, invert and normalise depth to [0, 1] range
            // TODO: encode this into the transform matrix?
            v.x = v.x / v.z * FieldOfViewDepth;
            v.y = v.y / v.z * FieldOfViewDepth;
            v.z = (v.z - Position.z + 1.0) * 0.5;
            return v;
        }

        // TODO: view space is post-projection, which breaks the lighting. Redefine view space to be pre-projection!
        /// <summary>
        /// Transform a position vector from view space to model space.
        /// </summary>
        /// <remarks>View space is a Left-Handed coordinate system with X+ve to the right, Y+ve up and Z+ve forward.</remarks>
        /// <remarks>Model space is a Left-Handed coordinate system.</remarks>
        public Vector TransformPosFromView(Vector pos)
        {
            // Unproject vector and un-invert and de-normalise depth
            // TODO: Should this method reverse projection, or leave this out?
            // TODO: encode this into the inverse transform matrix?
            Vector v;
            v.z = pos.z * 2.0 - 1.0 + Position.z;
            v.x = pos.x * v.z / FieldOfViewDepth;
            v.y = pos.y * v.z / FieldOfViewDepth;

            // Reverse the rotation, scaling and translation of the vector.
            return Matrix.Multiply3X4(_inverseTransform, pos);

            //return new Vector(
            //    v.x * inverseTransform[0, 0] + v.y * inverseTransform[0, 1] + v.z * inverseTransform[0, 2] + inverseTransform[0, 3],
            //    v.x * inverseTransform[1, 0] + v.y * inverseTransform[1, 1] + v.z * inverseTransform[1, 2] + inverseTransform[1, 3],
            //    v.x * inverseTransform[2, 0] + v.y * inverseTransform[2, 1] + v.z * inverseTransform[2, 2] + inverseTransform[2, 3]);
        }

        /// <summary>
        /// Transform a direction from model space to view space.
        /// The direction will be rotated but not translated.
        /// </summary>
        /// <remarks>TODO: handle scaling?</remarks>
        public Vector TransformDirection(Vector dir)
        {
            return new Vector(
                dir.x * _transform[0, 0] + dir.y * _transform[0, 1] + dir.z * _transform[0, 2],
                dir.x * _transform[1, 0] + dir.y * _transform[1, 1] + dir.z * _transform[1, 2],
                dir.x * _transform[2, 0] + dir.y * _transform[2, 1] + dir.z * _transform[2, 2]);
        }

        /// <summary>
        /// Transform a direction from view space to model space.
        /// The direction will be rotated but not translated.
        /// </summary>
        /// <remarks>TODO: handle scaling?</remarks>
        public Vector TransformDirectionReverse(Vector dir)
        {
            return new Vector(
                dir.x * _inverseTransform[0, 0] + dir.y * _inverseTransform[0, 1] + dir.z * _inverseTransform[0, 2],
                dir.x * _inverseTransform[1, 0] + dir.y * _inverseTransform[1, 1] + dir.z * _inverseTransform[1, 2],
                dir.x * _inverseTransform[2, 0] + dir.y * _inverseTransform[2, 1] + dir.z * _inverseTransform[2, 2]);
        }

        #endregion
    }
}
