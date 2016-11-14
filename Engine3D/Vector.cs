using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

// TODO: adding Contracts to this class might have slowed down the Debug code quite a lot!

namespace Engine3D
{
    public struct Vector
    {
        public double x;
        public double y;
        public double z;

        public static readonly Vector Zero = new Vector(0, 0, 0);
        public static readonly Vector Right = new Vector(1, 0, 0);
        public static readonly Vector Up = new Vector(0, 1, 0);
        public static readonly Vector Forward = new Vector(0, 0, 1);

        private const double epsilon = 1e-10;

        public Vector(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;

            Contract.Assert(!ContainsNaN && !ContainsInfinity);
        }

        public override int GetHashCode()
        {
            // TODO: optimise this?
            return (x * 1000000 + y * 1000 + z).GetHashCode();
        }

        public override bool Equals(object o)
        {
            // TODO: optimise this
            return this == (Vector)o;
        }

        public static bool operator ==(Vector a, Vector b)
        {
            // TODO: optimise this
            return (a - b).LengthSqr < epsilon;
        }

        public static bool operator !=(Vector a, Vector b)
        {
            // TODO: optimise this
            return !(a == b);
        }

        public static Vector operator +(Vector a, Vector b)
        {
            return new Vector(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static Vector operator -(Vector a, Vector b)
        {
            Vector r;
            r.x = a.x - b.x;
            r.y = a.y - b.y;
            r.z = a.z - b.z;
            return r;
        }

        public static Vector operator *(Vector a, double b)
        {
            return new Vector(a.x * b, a.y * b, a.z * b);
        }

        public static Vector operator *(double a, Vector b)
        {
            return new Vector(a * b.x, a * b.y, a * b.z);
        }

        public static Vector operator /(Vector a, double b)
        {
            Contract.Requires(Math.Abs(b) > epsilon);
            return new Vector(a.x / b, a.y / b, a.z / b);
        }

        // TODO: valid operation?
        public static Vector operator /(double a, Vector b)
        {
            Contract.Requires(b.x != 0 && b.y != 0 && b.z != 0);
            return new Vector(a / b.x, a / b.y, a / b.z);
        }

        public static Vector operator -(Vector v)
        {
            return new Vector(-v.x, -v.y, -v.z);
        }

        // TODO: might speed up ray-tracing, but requires .NET 4.5
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double DotProduct(Vector other)
        {
            return this.x * other.x + this.y * other.y + this.z * other.z;
        }

        public Vector CrossProduct(Vector other)
        {
            return new Vector(
                this.y * other.z - this.z * other.y,
                this.z * other.x - this.x * other.z,
                this.x * other.y - this.y * other.x);
        }

        public double Distance(Vector other)
        {
            // TODO: optimise this
            return (this - other).Length;
        }

        /// <summary>
        /// Get the length of the vector
        /// </summary>
        public double Length
        {
            get
            {
                return Math.Sqrt(x * x + y * y + z * z);
            }
        }

        /// <summary>
        /// Get the square of the length of the vector
        /// </summary>
        public double LengthSqr
        {
            get
            {
                return x * x + y * y + z * z;
            }
        }

        public bool IsZeroVector
        {
            get
            {
                return -epsilon < x && x < epsilon &&
                       -epsilon < y && y < epsilon &&
                       -epsilon < z && z < epsilon;
            }
        }

        public bool IsUnitVector
        {
            get
            {
                return Math.Abs(LengthSqr - 1.0) < epsilon;
            }
        }

        public bool ContainsNaN
        {
            get
            {
                return double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z);
            }
        }

        public bool ContainsInfinity
        {
            get
            {
                return double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(z);
            }
        }

        /// <summary>
        /// Scale this vector to be a unit vector, i.e. make this vector unit length without changing its direction.
        /// </summary>
        public void Normalise()
        {
            Contract.Requires(this.Length > 0);
            double len = this.Length;
            double inverseLen = 1.0 / len;
            x *= inverseLen;
            y *= inverseLen;
            z *= inverseLen;
        }

        public Vector Clone()
        {
            return new Vector(x, y, z);
        }

        public override string ToString()
        {
            return string.Format("{0},{1},{2}", x, y, z);
        }
    }
}
