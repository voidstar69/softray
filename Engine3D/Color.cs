using System;

namespace Engine3D
{
    public struct Color
    {
        public double r;
        public double g;
        public double b;
        // TODO: is alpha required?

        public static readonly Color Black = new Color(0.0, 0.0, 0.0);
        public static readonly Color White = new Color(1.0, 1.0, 1.0);
        public static readonly Color Grey = new Color(0.5, 0.5, 0.5);
        public static readonly Color Red = new Color(1.0, 0.0, 0.0);
        public static readonly Color Green = new Color(0.0, 1.0, 0.0);
        public static readonly Color Blue = new Color(0.0, 0.0, 1.0);
        public static readonly Color Yellow = new Color(1.0, 1.0, 0.0);
        public static readonly Color Orange = new Color(1.0, 0.5, 0.0);
        public static readonly Color Brown = new Color(0.5, 0.25, 0.0);
        public static readonly Color Pink = new Color(1.0, 0.0, 1.0);
        public static readonly Color Cyan = new Color(0.0, 1.0, 1.0);

        public Color(double r, double g, double b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }

        public Color(uint argbColor)
        {
            r = (byte)((argbColor >> 16) & 0xff) / 255.0;
            g = (byte)((argbColor >> 8) & 0xff) / 255.0;
            b = (byte)(argbColor & 0xff) / 255.0;
        }

        public static Color operator +(Color a, Color b)
        {
            return new Color(a.r + b.r, a.g + b.g, a.b + b.b);
        }

        public static Color operator -(Color a, Color b)
        {
            Color r;
            r.r = a.r - b.r;
            r.g = a.g - b.g;
            r.b = a.b - b.b;
            return r;
        }

        public static Color operator *(Color a, double b)
        {
            return new Color(a.r * b, a.g * b, a.b * b);
        }

        public static Color operator *(double a, Color b)
        {
            return new Color(a * b.r, a * b.g, a * b.b);
        }

        public static Color operator /(Color a, double b)
        {
            return new Color(a.r / b, a.g / b, a.b / b);
        }

        public static Color operator -(Color v)
        {
            return new Color(-v.r, -v.g, -v.b);
        }

        public double DotProduct(Color other)
        {
            return this.r * other.r + this.g * other.g + this.b * other.b;
        }

        public double Length
        {
            get
            {
                return Math.Sqrt(r * r + g * g + b * b);
            }
        }

        public bool IsUnitColor
        {
            get
            {
                return Math.Abs(Length - 1.0) < 1e-10;
            }
        }

        /// <summary>
        /// Scale this color to be a unit color, i.e. make this color fully intense without changing its chroma.
        /// </summary>
        public void Normalise()
        {
            double len = this.Length;
            double inverseLen = 1.0 / len;
            r *= inverseLen;
            g *= inverseLen;
            b *= inverseLen;
        }

        public uint ToARGB()
        {
            byte redByte = (byte)(r * 255.0);
            byte greenByte = (byte)(g * 255.0);
            byte blueByte = (byte)(b * 255.0);
            return (uint)((255u << 24) + (redByte << 16) + (greenByte << 8) + blueByte);
        }

        public Color Clone()
        {
            return new Color(r, g, b);
        }

        public override string ToString()
        {
            return string.Format("{0},{1},{2}", r, g, b);
        }
    }
}