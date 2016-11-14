using System.Diagnostics.Contracts;

namespace Engine3D
{
    // TODO: is class or struct more efficient here? Struct means no null checks required in client code.
    public class Attributes
    {
        private readonly double[] values;

        public Attributes(int length)
        {
            Contract.Requires(0 <= length);
            values = new double[length];
        }

        public Attributes(params double[] attr)
        {
            Contract.Requires(attr != null);
            values = (double[])attr.Clone();
        }

        public Attributes(Attributes attr)
        {
            Contract.Requires(attr != null);
            values = (double[])attr.values.Clone();
        }

        public int Length
        {
            get
            {
                return values.Length;
            }
        }

        public double this[int index]
        {
            get
            {
                Contract.Requires(0 <= index);
                return values[index];
            }
            set
            {
                Contract.Requires(0 <= index);
                values[index] = value;
            }
        }

        public static Attributes operator +(Attributes a, Attributes b)
        {
            Contract.Requires(a != null);
            Contract.Requires(b != null);
            Attributes r = new Attributes(a.Length);
            for (int i = 0; i < a.Length; i++)
            {
                r[i] = a[i] + b[i];
            }
            return r;
        }

        public static Attributes operator -(Attributes a, Attributes b)
        {
            Contract.Requires(a != null);
            Contract.Requires(b != null);
            Attributes r = new Attributes(a.Length);
            for (int i = 0; i < a.Length; i++)
            {
                r[i] = a[i] - b[i];
            }
            return r;
        }

        public static Attributes operator *(Attributes a, double b)
        {
            Contract.Requires(a != null);
            Attributes r = new Attributes(a.Length);
            for (int i = 0; i < a.Length; i++)
            {
                r[i] = a[i] * b;
            }
            return r;
        }

        public static Attributes Lerp(Attributes a, Attributes b, double frac)
        {
            Contract.Requires(a != null);
            Contract.Requires(b != null);
            Attributes r = new Attributes(a.Length);
            for (int i = 0; i < a.Length; i++)
            {
                r[i] = a[i] + (b[i] - a[i]) * frac;
            }
            return r;
        }
    }
}