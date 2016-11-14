using System;
using System.Diagnostics.Contracts;

namespace Engine3D
{
    /// <summary>
    /// A 4x4 matrix.
    /// Note that the default constructor creates an empty matrix (all elements are zero), NOT the identity matrix.
    /// </summary>
    public class Matrix
    {
        private const int NumRows = 4;
        private const int NumCols = 4;
        private readonly double[,] _m = new double[NumRows, NumCols];

        public double this[int row, int col]
        {
            get
            {
                return _m[row, col];
            }
            set
            {
                _m[row, col] = value;
            }
        }

        /// <summary>
        /// Multiply the top-left 3x3 sub-matrix by a 3-vector
        /// </summary>
        /// <param name="m">A 4x4 matrix within which the 3x3 matrix is embedded</param>
        /// <param name="v">The 3-vector to transform</param>
        /// <returns>The transformed 3-vector</returns>
        public static Vector Multiply3X3(Matrix m, Vector v)
        {
            Contract.Requires(m != null);
            return new Vector(
                v.x * m[0, 0] + v.y * m[0, 1] + v.z * m[0, 2],
                v.x * m[1, 0] + v.y * m[1, 1] + v.z * m[1, 2],
                v.x * m[2, 0] + v.y * m[2, 1] + v.z * m[2, 2]);
        }

        /// <summary>
        /// Multiply the top 3x4 sub-matrix by a 3-vector
        /// </summary>
        /// <param name="m">A 4x4 matrix within which the 3x4 matrix is embedded</param>
        /// <param name="v">The 3-vector to transform</param>
        /// <returns>The transformed 3-vector</returns>
        public static Vector Multiply3X4(Matrix m, Vector v)
        {
            Contract.Requires(m != null);
            return new Vector(
                v.x * m[0, 0] + v.y * m[0, 1] + v.z * m[0, 2] + m[0, 3],
                v.x * m[1, 0] + v.y * m[1, 1] + v.z * m[1, 2] + m[1, 3],
                v.x * m[2, 0] + v.y * m[2, 1] + v.z * m[2, 2] + m[2, 3]);
        }

        /// <summary>
        /// Multiply the top 3x4 sub-matrix by a 4-vector
        /// </summary>
        /// <param name="m">A 4x4 matrix within which the 3x4 matrix is embedded</param>
        /// <param name="v">The (x,y,z) components of the 4-vector to transform</param>
        /// <param name="w">The w component of the 4-vector to transform</param>
        /// <returns>The (x,y,z) components of the transformed 4-vector (the w component is discarded)</returns>
        public static Vector Multiply3X4(Matrix m, Vector v, double w)
        {
            Contract.Requires(m != null);
            return new Vector(
                v.x * m[0, 0] + v.y * m[0, 1] + v.z * m[0, 2] + w * m[0, 3],
                v.x * m[1, 0] + v.y * m[1, 1] + v.z * m[1, 2] + w * m[1, 3],
                v.x * m[2, 0] + v.y * m[2, 1] + v.z * m[2, 2] + w * m[2, 3]);
        }

        public static Matrix operator * (Matrix m1, Matrix m2)
        {
            Contract.Requires(m1 != null);
            Contract.Requires(m2 != null);
            var r = new Matrix();
            for (var row = 0; row < NumRows; row++)
            {
                for (var col = 0; col < NumCols; col++)
                {
                    var sum = 0.0;
                    for (var i = 0; i < NumRows; i++)
                    {
                        sum += m1[row, i] * m2[i, col];
                    }
                    r[row, col] = sum;
                }
            }
            return r;
        }

        public static Matrix MakeTranslationMatrix(Vector position)
        {
            var matrix = new Matrix();
            matrix[0, 0] = 1.0;         // \
            matrix[1, 0] = 0.0;         // | X-axis vector
            matrix[2, 0] = 0.0;         // /

            matrix[0, 1] = 0.0;         // \
            matrix[1, 1] = 1.0;         // | Y-axis vector
            matrix[2, 1] = 0.0;         // /

            matrix[0, 2] = 0.0;         // \
            matrix[1, 2] = 0.0;         // | Z-axis vector
            matrix[2, 2] = 1.0;         // /

            matrix[0, 3] = position.x;  // \
            matrix[1, 3] = position.y;  // | Position vector
            matrix[2, 3] = position.z;  // /
            matrix[3, 3] = 1.0;
            return matrix;
        }

        public static Matrix MakeYawMatrix(double yaw)
        {
            var yawMatrix = new Matrix();
            yawMatrix[0, 0] = Math.Cos(yaw);        // \
            yawMatrix[1, 0] = 0.0;                  // | X-axis vector
            yawMatrix[2, 0] = Math.Sin(yaw);        // /

            yawMatrix[0, 1] = 0.0;                  // \
            yawMatrix[1, 1] = 1.0;                  // | Y-axis vector
            yawMatrix[2, 1] = 0.0;                  // /

            yawMatrix[0, 2] = -Math.Sin(yaw);       // \
            yawMatrix[1, 2] = 0.0;                  // | Z-axis vector
            yawMatrix[2, 2] = Math.Cos(yaw);        // /
            yawMatrix[3, 3] = 1.0;
            return yawMatrix;
        }

        public static Matrix MakePitchMatrix(double pitch)
        {
            var pitchMatrix = new Matrix();
            pitchMatrix[0, 0] = 1.0;                // \
            pitchMatrix[1, 0] = 0.0;                // | X-axis vector
            pitchMatrix[2, 0] = 0.0;                // /

            pitchMatrix[0, 1] = 0.0;                // \
            pitchMatrix[1, 1] = Math.Cos(pitch);    // | Y-axis vector
            pitchMatrix[2, 1] = Math.Sin(pitch);    // /

            pitchMatrix[0, 2] = 0.0;                // \
            pitchMatrix[1, 2] = -Math.Sin(pitch);   // | Z-axis vector
            pitchMatrix[2, 2] = Math.Cos(pitch);    // /
            pitchMatrix[3, 3] = 1.0;
            return pitchMatrix;
        }

        public static Matrix MakeRollMatrix(double roll)
        {
            var rollMatrix = new Matrix();
            rollMatrix[0, 0] = Math.Cos(roll);      // \
            rollMatrix[1, 0] = Math.Sin(roll);      // | X-axis vector
            rollMatrix[2, 0] = 0.0;                 // /

            rollMatrix[0, 1] = -Math.Sin(roll);     // \
            rollMatrix[1, 1] = Math.Cos(roll);      // | Y-axis vector
            rollMatrix[2, 1] = 0.0;                 // /

            rollMatrix[0, 2] = 0.0;                 // \
            rollMatrix[1, 2] = 0.0;                 // | Z-axis vector
            rollMatrix[2, 2] = 1.0;                 // /
            rollMatrix[3, 3] = 1.0;
            return rollMatrix;
        }
    }
}
