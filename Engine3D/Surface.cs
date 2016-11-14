using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace Engine3D
{
    using DepthType = Single;
    //using DepthType = Double;

    /// <summary>
    /// A 2D buffer of pixels / texels, with various drawing operations supported.
    /// </summary>
    public class Surface
    {
        private readonly int width;
        private readonly int height;
        private int[] pixels;

        // Depth buffer, created on demand.
        private DepthType[] depthBuffer;

//        private static int[] intensityToColor = new int[256];
//        private static int[] intensityFixedPtToColor = new int[65536];
        private readonly int[] modulatedColor = new int[256];

        [ContractInvariantMethod]
        void ClassContract()
        {
            Contract.Invariant(width > 0);
            Contract.Invariant(height > 0);
            Contract.Invariant(width * height >= 0);
            Contract.Invariant(pixels != null);
            Contract.Invariant(pixels.Length == width * height);
            Contract.Invariant(depthBuffer == null || depthBuffer.Length == width * height);
        }

        static Surface()
        {
/*
            for (int intensity = 0; intensity < 256; intensity++)
            {
                intensityToColor[intensity] = (intensity << 16) + (intensity << 8) + intensity;
            }
*/
/*
            for (int i = 0; i < 65536; i++)
            {
                byte intensity = (byte)(i >> 8);
                intensityFixedPtToColor[i] = (intensity << 16) + (intensity << 8) + intensity;
            }
*/
        }

        public Surface(int width, int height)
        {
            Contract.Requires(width * height >= 0);
            this.width = width;
            this.height = height;
            this.pixels = new int[width * height];
            SetModulationColor(1.0, 1.0, 1.0);
        }

        public Surface(int width, int height, int[] pixels)
        {
            Contract.Requires(width * height >= 0);
            this.width = width;
            this.height = height;
            this.pixels = pixels;
            SetModulationColor(1.0, 1.0, 1.0);
        }

        public int Width
        {
            get
            {
                return width;
            }
        }

        public int Height
        {
            get
            {
                return height;
            }
        }

        public int[] Pixels
        {
            get
            {
                return pixels;
            }
        }

        // TODO: might speed up ray-tracing, but requires .NET 4.5
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PackRgb(byte r, byte g, byte b)
        {
            return (uint)((255u << 24) + (r << 16) + (g << 8) + b);
        }

        public static void UnpackRgb(uint color, out byte r, out byte g, out byte b)
        {
            r = (byte)((color >> 16) & 0xff);
            g = (byte)((color >> 8) & 0xff);
            b = (byte)(color & 0xff);
        }

        public static void UnpackRgba(uint color, out byte r, out byte g, out byte b, out byte a)
        {
            a = (byte)((color >> 24) & 0xff);
            r = (byte)((color >> 16) & 0xff);
            g = (byte)((color >> 8) & 0xff);
            b = (byte)(color & 0xff);
        }

        public static uint PackArgb(byte a, byte r, byte g, byte b)
        {
            return (uint)((a << 24) + (r << 16) + (g << 8) + b);
        }

        public static uint PackColor(Color color)
        {
            byte r = (byte)(color.r * 255.0);
            byte g = (byte)(color.g * 255.0);
            byte b = (byte)(color.b * 255.0);
            return (uint)((255u << 24) + (r << 16) + (g << 8) + b);
        }

        public static uint PackColorAndAlpha(Color color, double alpha)
        {
            byte r = (byte)(color.r * 255.0);
            byte g = (byte)(color.g * 255.0);
            byte b = (byte)(color.b * 255.0);
            byte a = (byte)(alpha * 255.0);
            return (uint)((a << 24) + (r << 16) + (g << 8) + b);
        }

        public void SetPixelBuffer(int[] pixels)
        {
            Contract.Requires(pixels != null);
            Assert.Equals(pixels.Length, this.pixels.Length);
            this.pixels = pixels;
        }

        public void Clear()
        {
            Array.Clear(Pixels, 0, Pixels.Length);
        }

        public void Clear(uint color)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = (int)color;
            }
        }

        public void ClearDepthBuffer()
        {
            if (depthBuffer != null)
            {
                Array.Clear(depthBuffer, 0, depthBuffer.Length);

                // Initialise inverse-depth buffer.
                //for (int i = 0; i < depthBuffer.Length; i++)
                //{
                //    depthBuffer[i] = double.MaxValue;
                //}
            }
        }

        public void DrawPixel(int x, int y, uint color)
        {
//            Assert.IsTrue(x >= 0 && x < Width && y >= 0 && y < Height);
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                Pixels[y * Width + x] = (int)color;
            }
        }

        public uint GetPixel(int x, int y)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                return (uint)Pixels[y * Width + x];
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets a linear depth value from the floating-point depth buffer.
        /// </summary>
        /// <param name="x">Horizontal pixel coordinate</param>
        /// <param name="y">Vertical pixel coordinate</param>
        /// <returns>Linear depth value between 0.0 (nearest) to 1.0 (furthest), or positive infinity (missing depth).</returns>
        public DepthType GetPixelFloatDepth(int x, int y)
        {
            if (depthBuffer != null && x >= 0 && x < Width && y >= 0 && y < Height)
            {
                DepthType invDepth = depthBuffer[y * Width + x];
                
                // TODO: the assertions below seem to be broken by blank pixels (having inverse depth value of zero).
                if (invDepth == 0.0)
                {
                    return DepthType.PositiveInfinity;
                }

                Contract.Assert(invDepth >= 1.0 && invDepth <= double.MaxValue, "inverse depth is out of range"); // : {0}", invDepth);
                DepthType depth = 1.0f / invDepth;
                // depth is guarenteed to be in the range [0.0, 1.0]
                return depth;
            }
            else
            {
                return DepthType.PositiveInfinity;
            }
        }

        public delegate uint ColorFunc(uint color);

        public void ApplyColorFunc(ColorFunc func)
        {
            Contract.Requires(func != null);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = (int)func((uint)pixels[i]);
            }
        }

        public delegate uint ColorDepthFunc(uint color, DepthType depth);

        public void ApplyColorDepthFunc(ColorDepthFunc func)
        {
            Contract.Requires(func != null);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = (int)func((uint)pixels[i], depthBuffer[i]);
            }
        }

        /// <summary>
        /// Draw a hollow rectangle.
        /// </summary>
        public void DrawRect(int left, int top, int right, int bottom, uint color)
        {
            for (int y = top; y < bottom; y++)
            {
                DrawPixel(left, y, color);
                DrawPixel(right, y, color);
            }

            for (int x = left; x < right; x++)
            {
                DrawPixel(x, top, color);
                DrawPixel(x, bottom, color);
            }
        }

        /// <summary>
        /// Draw a filled rectangle.
        /// </summary>
        public void DrawFilledRect(int left, int top, int right, int bottom, uint color)
        {
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    DrawPixel(x, y, color);
                }
            }
        }

        /// <summary>
        /// Draw a line in a single colour.
        /// </summary>
        /// <remarks>Uses a slow recursive algorithm</remarks>
        public void DrawLine(int x1, int y1, int x2, int y2, uint color)
        {
            if (Math.Abs(x1 - x2) <= 1 && Math.Abs(y1 - y2) <= 1)
            {
                return;
            }

            int middleX = (x1 + x2) / 2;
            int middleY = (y1 + y2) / 2;
            DrawPixel(middleX, middleY, color);
            DrawLine(x1, y1, middleX, middleY, color);
            DrawLine(middleX, middleY, x2, y2, color);
        }

        public void DrawTriangle_Wireframe(int x1, int y1, int x2, int y2, int x3, int y3, uint color)
        {
            DrawLine(x1, y1, x2, y2, color);
            DrawLine(x2, y2, x3, y3, color);
            DrawLine(x3, y3, x1, y1, color);
        }

        public void DrawSpan_Solid(int y, int left, int right, uint color)
        {
            // Check for invalid input.
//            Debug.Assert(left <= right, "left <= right");
            //    Debug.Assert(y >= 0 && y < height, "y >= 0 && y < height");
            //    Debug.Assert(left >= 0 && left < width, "left >= 0 && left < width");
            //    Debug.Assert(right >= 0 && right < width, "right >= 0 && right < width");

            if(y < 0 || y >= height || right < 0 || left >= width)
            {
                return;
            }

            // Clip horizontal extent of span.
            if(left < 0)
            {
                left = 0;
            }
            if(right >= width)
            {
                right = width - 1;
            }
//            Debug.Assert(left <= right, "left <= right");

            // Draw pixels in span.
            int dest = y * width + left;
            for(int x = left; x <= right; x++)
            {
                pixels[dest] = (int)color;
                dest++;
            }
        }

        // Given a four sided figure with two opposite sides parallel to the x-axis,
        // generate a list of the spans where it covers pixels on the screen.
        private void DrawTrapezium_Solid(double top, double bottom,
                                         double topLeftX, double topRightX,
                                         double bottomLeftX, double bottomRightX,
                                         uint color)
        {
            // Nothing to do?
            if (bottom == top ||
                (topLeftX == topRightX && bottomLeftX == bottomRightX))
            {
                return;
            }

            // Illegal arguments?
//            Debug.Assert(top <= bottom, "top <= bottom");
//            Debug.Assert(topLeftX <= topRightX, "topLeftX <= topRightX");
//            Debug.Assert(bottomLeftX <= bottomRightX, "bottomLeftX <= bottomRightX");

            // Ensure that adjacent triangles do not overlap horizontally.
            if (topRightX - topLeftX >= 1.0)
            {
                topRightX -= 1.0;
            }
            if (bottomRightX - bottomLeftX >= 1.0)
            {
                bottomRightX -= 1.0;
            }
//            Debug.Assert(topLeftX <= topRightX, "topLeftX <= topRightX");
//            Debug.Assert(bottomLeftX <= bottomRightX, "bottomLeftX <= bottomRightX");

            // Compute edge gradients.
            double height = bottom - top;
            double left = topLeftX;
            double right = topRightX;
            double leftDelta = (bottomLeftX - topLeftX) / height;
            double rightDelta = ((bottomRightX - topRightX) / height) + double.Epsilon;

            // Adjust initial left and right to vertical pixel centre.
            int firstRow = (int)top;
            int lastRow = (int)bottom;
            left += (1.0 - (top - firstRow)) * leftDelta;
            right += (1.0 - (top - firstRow)) * rightDelta;

            // For each row between top and bottom, trace the left and right edges.
            for (int row = firstRow; row < lastRow; row++)
            {
//                Debug.Assert(left <= right, "left <= right");

                // Find where the left and right edges intersect this row.
                int spanLeft = (int)left;
                int spanRight = (int)right;

                // Draw the pixels in this row that fall between the two edges.
                DrawSpan_Solid(row, spanLeft, spanRight, color);

                // Jump to the next row, moving along the left and right edges.
                left += leftDelta;
                right += rightDelta;
            }
        }

/*
        /// <summary>
        /// Draw a triangle in a single colour.
        /// </summary>
        /// <remarks>Uses a slow recursive algorithm</remarks>
        public void DrawTriangle_Solid(int x1, int y1, int x2, int y2, int x3, int y3, uint color)
        {
            if ((Math.Abs(x1 - x2) <= 1 && Math.Abs(y1 - y2) <= 1))
            {
                return;
            }

            // Split first edge, and recursively draw sub-triangles that don't
            // have either fragment of the first edge as their first edge.
            int middleX = (x1 + x2) / 2;
            int middleY = (y1 + y2) / 2;
            DrawPixel(middleX, middleY, color);
            DrawTriangle_Solid(x3, y3, x1, y1, middleX, middleY, color);
            DrawTriangle_Solid(x2, y2, x3, y3, middleX, middleY, color);
        }
*/

        /// <summary>
        /// Draw a solid-color triangle onto the surface.
        /// </summary>
        public void DrawTriangle_Solid(double x1, double y1,
                                       double x2, double y2,
                                       double x3, double y3,
                                       uint color)
        {
            // Sort points by height.
            double topX = x1;
            double topY = y1;
            double midX = x2;
            double midY = y2;
            double bottomX = x3;
            double bottomY = y3;
            double tmp;
            if (topY > midY)
            {
                tmp = topY;
                topY = midY;
                midY = tmp;
                tmp = topX;
                topX = midX;
                midX = tmp;
            }
            if (midY > bottomY)
            {
                tmp = midY;
                midY = bottomY;
                bottomY = tmp;
                tmp = midX;
                midX = bottomX;
                bottomX = tmp;
            }
            if (topY > midY)
            {
                tmp = topY;
                topY = midY;
                midY = tmp;
                tmp = topX;
                topX = midX;
                midX = tmp;
            }

            // Degenerate triangle?
            if (Math.Abs(bottomY - topY) <= double.Epsilon)
            {
                return;
            }

            // Calculate x of halfway point (point at height of midY, along edge from top point to bottom point)
            // and sort halfway x and middle x by increasing order.
            double leftMidX = (midY - topY) / (bottomY - topY);
            leftMidX = leftMidX * (bottomX - topX);
            leftMidX += topX;
            double rightMidX = midX;
            if (leftMidX > rightMidX)
            {
                tmp = leftMidX;
                leftMidX = rightMidX;
                rightMidX = tmp;
            }

            // Draw top trapezium.
            DrawTrapezium_Solid(topY, midY, topX, topX, leftMidX, rightMidX, color);

            // Draw bottom trapezium.
            DrawTrapezium_Solid(midY, bottomY, leftMidX, rightMidX, bottomX, bottomX, color);
        }

        /// <summary>
        /// Swap the contents of two variables of the same type.
        /// </summary>
        /// <typeparam name="T">The type of the two variables.</typeparam>
        /// <param name="a">The first variable.</param>
        /// <param name="b">The second variable.</param>
        private void Swap<T>(ref T a, ref T b)
        {
            Contract.Requires(a != null);
            Contract.Requires(b != null);
            Contract.Ensures(a != null);
            Contract.Ensures(b != null);
            Contract.Ensures(Contract.OldValue(b).Equals(a));
            Contract.Ensures(Contract.OldValue(a).Equals(b));

            T tmp = a;
            a = b;
            b = tmp;
        }

        // TODO: add Contract constraints onto delegate types

        public delegate void DrawSpanCallback<T, U>(int row, T left, T right, U leftAttr, U rightAttr);

        public delegate void DrawPixelCallback(int x, int y, Attributes attr);
        public DrawPixelCallback DrawPixelFunc;

        public delegate double CalcLightingCallback(Vector point, Vector normal);
        public CalcLightingCallback CalcLightingFunc;

//        public Func<Attributes, int> CalcPixelColorFunc;

        public int Color;

        public void SetModulationColor(double red, double green, double blue)
        {
//            Assert.IsTrue(red >= 0.0 && red <= 1.0, "SetModulationColor: red out of range: {0}", red);
//            Assert.IsTrue(green >= 0.0 && green <= 1.0, "SetModulationColor: green out of range: {0}", green);
//            Assert.IsTrue(blue >= 0.0 && blue <= 1.0, "SetModulationColor: blue out of range: {0}", blue);

            byte baseRed = (byte)(red * 255.0);
            byte baseGreen = (byte)(green * 255.0);
            byte baseBlue = (byte)(blue * 255.0);

            for (ushort intensity = 0; intensity < 256; intensity++)
            {
                byte r = (byte)((baseRed * intensity) >> 8);
                byte g = (byte)((baseGreen * intensity) >> 8);
                byte b = (byte)((baseBlue * intensity) >> 8);
                modulatedColor[intensity] = (r << 16) + (g << 8) + b;
            }
        }

        public void DrawPixel_Lit(int x, int y, Attributes attr)
        {
            Contract.Requires(x >= 0);
            Contract.Requires(y >= 0);
            Contract.Requires(attr != null);
            pixels[y * width + x] = (int)(attr[0] * 255) * 65793;
        }

        public void DrawSpan_Flat(int y, double left, double right, Attributes leftAttr, Attributes rightAttr)
        {
            if (y < 0 || y >= height || right < 0 || left >= width)
            {
                return;
            }

            // Clip horizontal extent of span.
            if (left < 0)
            {
                left = 0;
                // TODO: clip attributes
            }
            if (right >= width)
            {
                right = width - 1;
                // TODO: clip attributes
            }

            // Draw pixels in span in a constant color.
            int leftInt = (int)left;
            int rightInt = (int)right;
            int dest = y * width + leftInt;
            for (int x = leftInt; x <= rightInt; x++)
            {
                pixels[dest++] = Color;
            }
        }

        public void DrawSpan_Depth(int y, double left, double right, Attributes leftAttr, Attributes rightAttr)
        {
            if (y < 0 || y >= height || right < 0 || left >= width)
            {
                return;
            }

            // Clip horizontal extent of span.
            if (left < 0)
            {
                left = 0;
                // TODO: clip attributes
            }
            if (right >= width)
            {
                right = width - 1;
                // TODO: clip attributes
            }

            // Compute depth gradients.
            double z = leftAttr[0];
            double zDelta = (rightAttr[0] - z) * (1.0 / (right - left + 1));

            // Draw pixels in span, with depth testing.
            Color &= 0x00ffffff;
            int dest = y * width + (int)left;
            for (int x = (int)left; x <= (int)right; x++)
            {
                byte currZ = (byte)(((uint)pixels[dest]) >> 24);
                if (z > currZ)
                {
                    pixels[dest] = (int)((uint)((byte)z << 24) | (uint)Color);
                }
                dest++;
                z += zDelta;
            }
        }

        public void DrawSpan_Depth(int y, double left, double right, double leftAttr, double rightAttr)
        {
//            Assert.IsTrue(leftAttr >= 0.0 && leftAttr <= 1.0, "leftAttr out of range: {0}", leftAttr);
//            Assert.IsTrue(rightAttr >= 0.0 && rightAttr <= 1.0, "rightAttr out of range: {0}", rightAttr);

            if (y < 0 || y >= height || right < 0 || left >= width)
            {
                return;
            }

            // Clip horizontal extent of span.
            if (left < 0)
            {
                left = 0;
                // TODO: clip attributes
            }
            if (right >= width)
            {
                right = width - 1;
                // TODO: clip attributes
            }

            // Compute depth gradients.
            //double z = leftAttr;
            //double zDelta = (rightAttr - z) * (1.0 / (right - left + 1));
            int zFixed = (int)(leftAttr * 256 * 255);
            int zFixedDelta = (int)((rightAttr - leftAttr) / (right - left + 1) * 256 * 255);

            // Draw pixels in span, with depth testing.
            Color &= 0x00ffffff;
            int dest = y * width + (int)left;
            for (int x = (int)left; x <= (int)right; x++)
            {
                byte oldZ = (byte)(((uint)pixels[dest]) >> 24);
                byte z = (byte)(zFixed >> 8);
                if (z > oldZ)
                {
                    pixels[dest] = (int)((uint)((byte)z << 24) | (uint)Color);
                }
                dest++;
                zFixed += zFixedDelta;
            }
        }

        // Draw a horizontal span of pixels with interpolated depth and monochrome lighting.
        // The depth information is stored in the 8-bit alpha channel of this surface.
        public void DrawSpan_LitDepth(int y, double left, double right, Attributes leftAttr, Attributes rightAttr)
        {
            Contract.Requires(leftAttr != null);
            Contract.Requires(rightAttr != null);
            Assert.IsTrue(leftAttr[0] >= 0.0 && leftAttr[0] <= 1.0, "leftAttr[0] out of range: {0}", leftAttr[0]);
            Assert.IsTrue(rightAttr[0] >= 0.0 && rightAttr[0] <= 1.0, "rightAttr[0] out of range: {0}", rightAttr[0]);
            Assert.IsTrue(leftAttr[1] >= 0.0 && leftAttr[1] <= 1.0, "leftAttr[1] out of range: {0}", leftAttr[1]);
            Assert.IsTrue(rightAttr[1] >= 0.0 && rightAttr[1] <= 1.0, "rightAttr[1] out of range: {0}", rightAttr[1]);

            if (y < 0 || y >= height || right < 0 || left >= width)
            {
                return;
            }

            // Clip horizontal extent of span.
            if (left < 0)
            {
                left = 0;
                // TODO: clip attributes
            }
            if (right >= width)
            {
                right = width - 1;
                // TODO: clip attributes
            }

            // Compute lighting gradients.
            //double light = leftAttr[0];
            //double lightDelta = (rightAttr[0] - light) * (1.0 / (right - left + 1));
            int lightFixed = (int)(leftAttr[0] * 256 * 255);
            int lightFixedDelta = (int)((rightAttr[0] - leftAttr[0]) / (right - left + 1) * 256 * 255);

            // Compute depth gradients.
//            double z = leftAttr[1];
//            double zDelta = (rightAttr[1] - z) * (1.0 / (right - left + 1));
            int zFixed = (int)(leftAttr[1] * 256 * 255);
            int zFixedDelta = (int)((rightAttr[1] - leftAttr[1]) / (right - left + 1) * 256 * 255);

            // Draw pixels in span with interpolated lighting and depth testing.
            int dest = y * width + (int)left;
            for (int x = (int)left; x <= (int)right; x++)
            {
                byte oldZ = (byte)(((uint)pixels[dest]) >> 24);
                byte z = (byte)(zFixed >> 8);
                if (z > oldZ)
                {
                    pixels[dest] = (int)(((byte)z << 24) + modulatedColor[(uint)lightFixed >> 8]);
                }
                dest++;
                lightFixed += lightFixedDelta;
                zFixed += zFixedDelta;
            }
        }

        // Draw a horizontal span of pixels with interpolated depth and monochrome lighting.
        // The depth information is stored in a 32-bit depth buffer attached to this surface.
        public void DrawSpan_LitDepthHires(int y, double left, double right, Attributes leftAttr, Attributes rightAttr)
        {
            Contract.Requires(leftAttr != null);
            Contract.Requires(rightAttr != null);

            // Check number of attributes.
            Contract.Requires(leftAttr.Length == 2, "leftAttr must have length 2");
            Contract.Requires(rightAttr.Length == 2, "rightAttr must have length 2");

            // Check range of lighting intensity values.
            Contract.Requires(leftAttr[0] >= 0.0 && leftAttr[0] <= 1.0, "leftAttr[0] out of range"); // : {0}", leftAttr[0]);
            Contract.Requires(rightAttr[0] >= 0.0 && rightAttr[0] <= 1.0, "rightAttr[0] out of range"); // : {0}", rightAttr[0]);

            // Check range of Z values.
            Contract.Requires(leftAttr[1] >= 1.0 && leftAttr[1] <= double.MaxValue, "leftAttr[1] out of range"); // : {0}", leftAttr[1]);
            Contract.Requires(rightAttr[1] >= 1.0 && rightAttr[1] <= double.MaxValue, "rightAttr[1] out of range"); // : {0}", rightAttr[1]);

            if(depthBuffer == null)
            {
                depthBuffer = new DepthType[width * height];
            }

            if (y < 0 || y >= height || right < 0 || left >= width)
            {
                return;
            }

            // Clip horizontal extent of span.
            if (left < 0)
            {
                left = 0;
                // TODO: clip attributes
            }
            if (right >= width)
            {
                right = width - 1;
                // TODO: clip attributes
            }

            // Compute lighting gradients.
            //double light = leftAttr[0];
            //double lightDelta = (rightAttr[0] - light) * (1.0 / (right - left + 1));
            int lightFixed = (int)(leftAttr[0] * 256 * 255);
            int lightFixedDelta = (int)((rightAttr[0] - leftAttr[0]) / (right - left + 1) * 256 * 255);

            // Compute inverse depth gradients.
            double invZ = leftAttr[1];
            //double rightInvZ = rightAttr[0];
            //double invZ = 1.0 / (1.0 - leftAttr[0]);
            //double rightInvZ = 1.0 / (1.0 - rightAttr[0]);
            double invZDelta = (rightAttr[1] - invZ) / (right - left + 1);

            // Compute depth gradients.
            //double z = leftAttr[1];
            //double zDelta = (rightAttr[1] - z) / (right - left + 1);
            //int zFixed = (int)(leftAttr[1] * 256 * 256 * 256);
            //int zFixedDelta = (int)((rightAttr[1] - leftAttr[1]) / (right - left + 1) * 256 * 256 * 256);
            //double invZ = 1.0 / z;
            //double rightInvZ = 1.0 / rightAttr[1];
            //double invZDelta = (rightInvZ - invZ) / (right - left + 1);

            // Draw pixels in span with interpolated lighting and depth testing.
            int dest = y * width + (int)left;
            for (int x = (int)left; x <= (int)right; x++)
            {
                double oldZ = depthBuffer[dest];
                if (invZ > oldZ)
                //if (z > oldZ)
                {
                    pixels[dest] = (int)modulatedColor[(uint)lightFixed >> 8];
                    //depthBuffer[dest] = z;
                    depthBuffer[dest] = (DepthType)invZ;
                }
                dest++;
                lightFixed += lightFixedDelta;
                //z += zDelta;
                invZ += invZDelta;
                //zFixed += zFixedDelta;
            }
        }

        // Draw a horizontal span of pixels with per-pixel interpolated depth and monochrome lighting.
        // The depth information is stored in a 32-bit depth buffer attached to this surface.
        public void DrawSpan_PerPixelLitDepthHires(int y, double left, double right, Attributes leftAttr, Attributes rightAttr)
        {
            Contract.Requires(leftAttr != null);
            Contract.Requires(rightAttr != null);

            // Check number of attributes.
            Contract.Requires(leftAttr.Length == 7, "leftAttr must have length 7");
            Contract.Requires(rightAttr.Length == 7, "rightAttr must have length 7");

            // Check range of Z values.
            Assert.IsTrue(leftAttr[0] >= 1.0 && leftAttr[0] <= double.MaxValue, "leftAttr[0] out of range: {0}", leftAttr[0]);
            Assert.IsTrue(rightAttr[0] >= 1.0 && rightAttr[0] <= double.MaxValue, "rightAttr[0] out of range: {0}", rightAttr[0]);
            //Assert.IsTrue(leftAttr[0] >= 0.0 && leftAttr[0] <= 1.0, "leftAttr[0] out of range: {0}", leftAttr[0]);
            //Assert.IsTrue(rightAttr[0] >= 0.0 && rightAttr[0] <= 1.0, "rightAttr[0] out of range: {0}", rightAttr[0]);

            //Assert.IsTrue(leftAttr[1] >= -1.0 && leftAttr[1] <= 1.0, "leftAttr[1] out of range: {0}", leftAttr[1]);
            //Assert.IsTrue(rightAttr[1] >= -1.0 && rightAttr[1] <= 1.0, "rightAttr[1] out of range: {0}", rightAttr[1]);

            if (depthBuffer == null)
            {
                depthBuffer = new DepthType[width * height];
            }

            if (y < 0 || y >= height || right < 0 || left >= width)
            {
                return;
            }

            // Clip horizontal extent of span.
            if (left < 0)
            {
                left = 0;
                // TODO: clip attributes
            }
            if (right >= width)
            {
                right = width - 1;
                // TODO: clip attributes
            }

            // Compute depth gradients.
            //double z = leftAttr[0];
            //double zDelta = (rightAttr[0] - z) / (right - left + 1);

            // Compute inverse depth gradients.
            double invZ = leftAttr[0];
            //double rightInvZ = rightAttr[0];
            //double invZ = 1.0 / (1.0 - leftAttr[0]);
            //double rightInvZ = 1.0 / (1.0 - rightAttr[0]);
            double invZDelta = (rightAttr[0] - invZ) / (right - left + 1);

            // Compute (view-space) position gradients.
            Vector pos = new Vector(leftAttr[1], leftAttr[2], leftAttr[3]);
            Vector rightPos = new Vector(rightAttr[1], rightAttr[2], rightAttr[3]);
            Vector posDelta = (rightPos - pos) * (1.0 / (right - left + 1));

            // Compute (view-space) normal gradients.
            Vector normal = new Vector(leftAttr[4], leftAttr[5], leftAttr[6]);
            Vector rightNormal = new Vector(rightAttr[4], rightAttr[5], rightAttr[6]);
            Vector normalDelta = (rightNormal - normal) * (1.0 / (right - left + 1));

            // Draw pixels in span with interpolated lighting and depth testing.
            int dest = y * width + (int)left;
            for (int x = (int)left; x <= (int)right; x++)
            {
                double oldZ = depthBuffer[dest];
                if (invZ > oldZ)
                {
                    Vector unitNormal = normal * invZ;
                    unitNormal.Normalise();
                    // TODO: * invZ seems to have no effect!
                    double intensity = CalcLightingFunc(pos /* * invZ */, unitNormal);
                    Contract.Assume(intensity >= 0.0 && intensity <= 1.0);
                    //Assert.IsTrue(intensity >= 0.0 && intensity <= 1.0, "intensity out of range: {0}", intensity);
                    pixels[dest] = (int)modulatedColor[(int)(intensity * 255)];

                    // TODO: only for visualisation.
                    //pixels[dest] = (int)ColorFromArgb(255, /*(byte)(pos.x * 127 + 128)*/ 0, /*(byte)(pos.y * 127 + 128)*/ 0, (byte)(pos.z * 1270 + 128));
                    //pixels[dest] = (int)ColorFromArgb(255, 0, 0, (byte)((1.0 / invZ) * 1270 + 128));

                    //depthBuffer[dest] = z;
                    depthBuffer[dest] = (DepthType)invZ;
                }
                dest++;
                pos += posDelta;
                normal += normalDelta;
                invZ += invZDelta;
            }
        }

        public void DrawSpan_Lit(int y, double left, double right, double leftAttr, double rightAttr)
        {
            // Check range of lighting intensity values.
            Contract.Requires(leftAttr >= 0.0 && leftAttr <= 1.0, "leftAttr out of range"); // : {0}", leftAttr);
            Contract.Requires(rightAttr >= 0.0 && rightAttr <= 1.0, "rightAttr out of range"); // : {0}", rightAttr);

            if (y < 0 || y >= height || right < 0 || left >= width)
            {
                return;
            }

            // Clip horizontal extent of span.
            if (left < 0)
            {
                left = 0;
                // TODO: clip attributes
            }
            if (right >= width)
            {
                right = width - 1;
                // TODO: clip attributes
            }

            Contract.Assert(y >= 0);
            Contract.Assert(y < height);
            Contract.Assert(left >= 0);
            Contract.Assert(right >= 0);
            Contract.Assert(left < width);
            Contract.Assert(right < width);

            // Compute lighting gradients.
//            double light = leftAttr;
//            double lightDelta = (rightAttr - light) / (right - left + 1);
            int light = (int)(leftAttr * 256 * 255);
            int lightDelta = (int)((rightAttr - leftAttr) / (right - left + 1) * 256 * 255);

            // Draw pixels in span with interpolated lighting.
            int leftInt = (int)left;
            int rightInt = (int)right;
            int dest = y * width + (int)left;
            Contract.Assert(dest - leftInt + rightInt < pixels.Length);
            for (int x = leftInt; x <= rightInt; x++)
            {
                // TODO: These two alternatives are about the same performance wise. Why?
                var lightIndex = light >> 8;
                Contract.Assert(lightIndex >= 0 && lightIndex < modulatedColor.Length);
                pixels[dest++] = modulatedColor[lightIndex];
//                pixels[dest++] = intensityFixedPtToColor[light];
                light += lightDelta;

//                byte intensity = (byte)(light * 255);
//                byte intensity = (byte)(light * 255 / 65536);
//                byte intensity = (byte)(light >> 8);
//                pixels[dest++] = (int)((intensity << 16) + (intensity << 8) + intensity);
//                pixels[dest++] = intensityToColor[intensity];
//                light += lightDelta;
//                light = (byte)((light << 8 + lightDelta) >> 8);
            }
        }

        public void DrawSpan_Lit(int y, double left, double right, Attributes leftAttr, Attributes rightAttr)
        {
            if (y < 0 || y >= height || right < 0 || left >= width)
            {
                return;
            }

            // Clip horizontal extent of span.
            if (left < 0)
            {
                left = 0;
                // TODO: clip attributes
            }
            if (right >= width)
            {
                right = width - 1;
                // TODO: clip attributes
            }

            // Compute lighting gradients.
            double light = leftAttr[0];
            double lightDelta = (rightAttr[0] - light) / (right - left + 1);

            // Draw pixels in span with interpolated lighting.
            int dest = y * width + (int)left;
            for (int x = (int)left; x <= (int)right; x++)
            {
                byte intensity = (byte)(light * 255);
//                byte intensity = (byte)(Math.Min(Math.Max(light, 0.0), 1.0) * 255);
                pixels[dest++] = (int)((intensity << 16) + (intensity << 8) + intensity);
//                pixels[dest] = (int)ColorFromArgb(0, intensity, intensity, intensity);
                light += lightDelta;
            }
        }

        public void InterpolateSpan(int y, double left, double right, Attributes leftAttr, Attributes rightAttr)
        {
            Contract.Requires(leftAttr != null);
            Contract.Requires(rightAttr != null);

            if (y < 0 || y >= height || right < 0 || left >= width)
            {
                return;
            }

            // Clip horizontal extent of span.
            if (left < 0)
            {
                left = 0;
                // TODO: clip attributes
            }
            if (right >= width)
            {
                right = width - 1;
                // TODO: clip attributes
            }

            // Compute attribute gradients.
            Attributes attr = new Attributes(leftAttr);
            Attributes attrDelta = (rightAttr - leftAttr) * (1.0 / (right - left));

            // Draw pixels in span.
            for (int x = (int)left; x <= (int)right; x++)
            {
//                pixels[y * width + x] = CalcPixelColorFunc(attr);
                DrawPixelFunc(x, y, attr);
                attr += attrDelta;
            }
        }

        // Given a four sided figure with two opposite sides parallel to the x-axis,
        // generate a list of the spans where it covers pixels on the screen.
        private void InterpolateTrapezium(double top, double bottom,
                                          double topLeftX, Attributes topLeftAttr,
                                          double topRightX, Attributes topRightAttr,
                                          double bottomLeftX, Attributes bottomLeftAttr,
                                          double bottomRightX, Attributes bottomRightAttr,
                                          DrawSpanCallback<double, Attributes> drawSpan)
        {
            Contract.Requires(topLeftAttr != null);
            Contract.Requires(topRightAttr != null);
            Contract.Requires(bottomLeftAttr != null);
            Contract.Requires(bottomRightAttr != null);
            Contract.Requires(drawSpan != null);

            // Nothing to do?
            if (bottom == top ||
                (topLeftX == topRightX && bottomLeftX == bottomRightX))
            {
                return;
            }

            // Ensure that adjacent triangles do not overlap horizontally.
            if (topRightX - topLeftX >= 1.0)
            {
                topRightX -= 1.0;
                // TODO: adjust attributes?
            }
            if (bottomRightX - bottomLeftX >= 1.0)
            {
                bottomRightX -= 1.0;
                // TODO: adjust attributes?
            }

            // Compute edge gradients.
            double height = bottom - top;
            double invHeight = 1.0 / height;
            double leftX = topLeftX;
            double rightX = topRightX;
            Attributes leftAttr = topLeftAttr;
            Attributes rightAttr = topRightAttr;
            double leftDeltaX = (bottomLeftX - topLeftX) * invHeight;
            Attributes leftDeltaAttr = (bottomLeftAttr - topLeftAttr) * invHeight;
            double rightDeltaX = (bottomRightX - topRightX) * invHeight; // + double.Epsilon;
            Attributes rightDeltaAttr = (bottomRightAttr - topRightAttr) * invHeight; // + double.Epsilon;

            // Adjust initial left and right to vertical pixel centre.
            int firstRow = (int)top;
            int lastRow = (int)bottom;
            leftX += (1.0 - (top - firstRow)) * leftDeltaX;
            rightX += (1.0 - (top - firstRow)) * rightDeltaX;

            // For each row between top and bottom, trace the left and right edges.
            for (int row = firstRow; row < lastRow; row++)
            {
                // Draw the pixels in this row that fall between the two edges.
                drawSpan(row, leftX, rightX, leftAttr, rightAttr);

                // Jump to the next row, moving along the left and right edges, and interpolating attributes.
                leftX += leftDeltaX;
                rightX += rightDeltaX;
                leftAttr = leftAttr + leftDeltaAttr;
                rightAttr = rightAttr + rightDeltaAttr;
            }
        }

        /// <summary>
        /// Interpolate a triangle with a variable number of attributes per vertex.
        /// A callback function is called with the interpolated attributes at each horizontal span.
        /// </summary>
        public void InterpolateTriangle(double x1, double y1, Attributes attr1,
                                        double x2, double y2, Attributes attr2,
                                        double x3, double y3, Attributes attr3,
                                        DrawSpanCallback<double, Attributes> drawSpan)
        {
            Contract.Requires(attr1 != null);
            Contract.Requires(attr2 != null);
            Contract.Requires(attr3 != null);
            Contract.Requires(drawSpan != null);

            // Sort points by height.
            double topX = x1;
            double topY = y1;
            Attributes topAttr = attr1;
            double midX = x2;
            double midY = y2;
            Attributes midAttr = attr2;
            double bottomX = x3;
            double bottomY = y3;
            Attributes bottomAttr = attr3;
//            double tmp;
            if (topY > midY)
            {
                Swap(ref topX, ref midX);
                Swap(ref topY, ref midY);
                Swap(ref topAttr, ref midAttr);
/*
                tmp = topY;
                topY = midY;
                midY = tmp;
                tmp = topX;
                topX = midX;
                midX = tmp;
*/
            }
            if (midY > bottomY)
            {
                Swap(ref midX, ref bottomX);
                Swap(ref midY, ref bottomY);
                Swap(ref midAttr, ref bottomAttr);
/*
                tmp = midY;
                midY = bottomY;
                bottomY = tmp;
                tmp = midX;
                midX = bottomX;
                bottomX = tmp;
*/
            }
            if (topY > midY)
            {
                Swap(ref topX, ref midX);
                Swap(ref topY, ref midY);
                Swap(ref topAttr, ref midAttr);
/*
                tmp = topY;
                topY = midY;
                midY = tmp;
                tmp = topX;
                topX = midX;
                midX = tmp;
*/
            }

            // Degenerate triangle?
            if (Math.Abs(bottomY - topY) <= double.Epsilon)
            {
                return;
            }

            // Calculate x and attributes of halfway point (point at height of midY, along edge from top point to bottom point)
            // and sort halfway x and middle x by increasing order.
            double frac = (midY - topY) / (bottomY - topY);
            double leftMidX = midX;
            Attributes leftMidAttr = midAttr;
            double rightMidX = topX + frac * (bottomX - topX);
            Attributes rightMidAttr = Attributes.Lerp(topAttr, bottomAttr, frac);
            if (leftMidX > rightMidX)
            {
                Swap(ref leftMidX, ref rightMidX);
                Swap(ref leftMidAttr, ref rightMidAttr);
/*
                tmp = leftMidX;
                leftMidX = rightMidX;
                rightMidX = tmp;
*/
            }

            // Draw top trapezium.
            InterpolateTrapezium(topY, midY, topX, topAttr, topX, topAttr, leftMidX, leftMidAttr, rightMidX, rightMidAttr, drawSpan);

            // Draw bottom trapezium.
            InterpolateTrapezium(midY, bottomY, leftMidX, leftMidAttr, rightMidX, rightMidAttr, bottomX, bottomAttr, bottomX, bottomAttr, drawSpan);
        }

        // Given a four sided figure with two opposite sides parallel to the x-axis,
        // generate a list of the spans where it covers pixels on the screen.
        private void InterpolateTrapezium_1Double(double top, double bottom,
                                                  double topLeftX, double topLeftAttr,
                                                  double topRightX, double topRightAttr,
                                                  double bottomLeftX, double bottomLeftAttr,
                                                  double bottomRightX, double bottomRightAttr,
                                                  DrawSpanCallback<double, double> drawSpan)
        {
            // Nothing to do?
            if (bottom == top ||
                (topLeftX == topRightX && bottomLeftX == bottomRightX))
            {
                return;
            }

            // Ensure that adjacent triangles do not overlap horizontally.
            if (topRightX - topLeftX >= 1.0)
            {
                topRightX -= 1.0;
                // TODO: adjust attributes?
            }
            if (bottomRightX - bottomLeftX >= 1.0)
            {
                bottomRightX -= 1.0;
                // TODO: adjust attributes?
            }

            // Compute edge gradients.
            double height = bottom - top;
            double invHeight = 1.0 / height;
            double leftX = topLeftX;
            double rightX = topRightX;
            double leftAttr = topLeftAttr;
            double rightAttr = topRightAttr;
            double leftDeltaX = (bottomLeftX - topLeftX) * invHeight;
            double leftDeltaAttr = (bottomLeftAttr - topLeftAttr) * invHeight;
            double rightDeltaX = (bottomRightX - topRightX) * invHeight; // + double.Epsilon;
            double rightDeltaAttr = (bottomRightAttr - topRightAttr) * invHeight; // + double.Epsilon;

            // Adjust initial left and right to vertical pixel centre.
            int firstRow = (int)top;
            int lastRow = (int)bottom;
            leftX += (1.0 - (top - firstRow)) * leftDeltaX;
            rightX += (1.0 - (top - firstRow)) * rightDeltaX;

            // For each row between top and bottom, trace the left and right edges.
            for (int row = firstRow; row < lastRow; row++)
            {
                // Draw the pixels in this row that fall between the two edges.
                drawSpan(row, leftX, rightX, leftAttr, rightAttr);

                // Jump to the next row, moving along the left and right edges, and interpolating attributes.
                leftX += leftDeltaX;
                rightX += rightDeltaX;
                leftAttr = leftAttr + leftDeltaAttr;
                rightAttr = rightAttr + rightDeltaAttr;
            }
        }

        /// <summary>
        /// Interpolate a triangle with a single attribute per vertex.
        /// A callback function is called with the interpolated attribute at each horizontal span.
        /// </summary>
        public void InterpolateTriangle_1Double(double x1, double y1, double attr1,
                                                double x2, double y2, double attr2,
                                                double x3, double y3, double attr3,
                                                DrawSpanCallback<double, double> drawSpan)
        {
            // Sort points by height.
            double topX = x1;
            double topY = y1;
            double topAttr = attr1;
            double midX = x2;
            double midY = y2;
            double midAttr = attr2;
            double bottomX = x3;
            double bottomY = y3;
            double bottomAttr = attr3;
            //            double tmp;
            if (topY > midY)
            {
                Swap(ref topX, ref midX);
                Swap(ref topY, ref midY);
                Swap(ref topAttr, ref midAttr);
                /*
                                tmp = topY;
                                topY = midY;
                                midY = tmp;
                                tmp = topX;
                                topX = midX;
                                midX = tmp;
                */
            }
            if (midY > bottomY)
            {
                Swap(ref midX, ref bottomX);
                Swap(ref midY, ref bottomY);
                Swap(ref midAttr, ref bottomAttr);
                /*
                                tmp = midY;
                                midY = bottomY;
                                bottomY = tmp;
                                tmp = midX;
                                midX = bottomX;
                                bottomX = tmp;
                */
            }
            if (topY > midY)
            {
                Swap(ref topX, ref midX);
                Swap(ref topY, ref midY);
                Swap(ref topAttr, ref midAttr);
                /*
                                tmp = topY;
                                topY = midY;
                                midY = tmp;
                                tmp = topX;
                                topX = midX;
                                midX = tmp;
                */
            }

            // Degenerate triangle?
            if (Math.Abs(bottomY - topY) <= double.Epsilon)
            {
                return;
            }

            // Calculate x and attributes of halfway point (point at height of midY, along edge from top point to bottom point)
            // and sort halfway x and middle x by increasing order.
            double frac = (midY - topY) / (bottomY - topY);
            double leftMidX = midX;
            double leftMidAttr = midAttr;
            double rightMidX = topX + frac * (bottomX - topX);
            double rightMidAttr = topAttr + (bottomAttr - topAttr) * frac;
            if (leftMidX > rightMidX)
            {
                Swap(ref leftMidX, ref rightMidX);
                Swap(ref leftMidAttr, ref rightMidAttr);
                /*
                                tmp = leftMidX;
                                leftMidX = rightMidX;
                                rightMidX = tmp;
                */
            }

            // Draw top trapezium.
            InterpolateTrapezium_1Double(topY, midY, topX, topAttr, topX, topAttr, leftMidX, leftMidAttr, rightMidX, rightMidAttr, drawSpan);

            // Draw bottom trapezium.
            InterpolateTrapezium_1Double(midY, bottomY, leftMidX, leftMidAttr, rightMidX, rightMidAttr, bottomX, bottomAttr, bottomX, bottomAttr, drawSpan);
        }

/*
        private void InterpolateTrapezium_2Double(double top, double bottom,
                                                  double topLeftX, double topLeftAttr,
                                                  double topRightX, double topRightAttr,
                                                  double bottomLeftX, double bottomLeftAttr,
                                                  double bottomRightX, double bottomRightAttr,
                                                  DrawSpanCallback<double[], double[]> drawSpan)
        {
            // Nothing to do?
            if (bottom == top ||
                (topLeftX == topRightX && bottomLeftX == bottomRightX))
            {
                return;
            }

            // Ensure that adjacent triangles do not overlap horizontally.
            if (topRightX - topLeftX >= 1.0)
            {
                topRightX -= 1.0;
                // TODO: adjust attributes?
            }
            if (bottomRightX - bottomLeftX >= 1.0)
            {
                bottomRightX -= 1.0;
                // TODO: adjust attributes?
            }

            // Compute edge gradients.
            double height = bottom - top;
            double invHeight = 1.0 / height;
            double leftX = topLeftX;
            double rightX = topRightX;
            double leftAttr = topLeftAttr;
            double rightAttr = topRightAttr;
            double leftDeltaX = (bottomLeftX - topLeftX) * invHeight;
            double leftDeltaAttr = (bottomLeftAttr - topLeftAttr) * invHeight;
            double rightDeltaX = (bottomRightX - topRightX) * invHeight; // + double.Epsilon;
            double rightDeltaAttr = (bottomRightAttr - topRightAttr) * invHeight; // + double.Epsilon;

            // Adjust initial left and right to vertical pixel centre.
            int firstRow = (int)top;
            int lastRow = (int)bottom;
            leftX += (1.0 - (top - firstRow)) * leftDeltaX;
            rightX += (1.0 - (top - firstRow)) * rightDeltaX;

            // For each row between top and bottom, trace the left and right edges.
            for (int row = firstRow; row < lastRow; row++)
            {
                // Draw the pixels in this row that fall between the two edges.
                drawSpan(row, leftX, rightX, leftAttr, rightAttr);

                // Jump to the next row, moving along the left and right edges, and interpolating attributes.
                leftX += leftDeltaX;
                rightX += rightDeltaX;
                leftAttr = leftAttr + leftDeltaAttr;
                rightAttr = rightAttr + rightDeltaAttr;
            }
        }

        /// <summary>
        /// Interpolate a triangle with a single attribute per vertex.
        /// A callback function is called with the interpolated attribute at each horizontal span.
        /// </summary>
        public void InterpolateTriangle_2Double(double x1, double y1, double attr1,
                                                double x2, double y2, double attr2,
                                                double x3, double y3, double attr3,
                                                DrawSpanCallback<double, double> drawSpan)
        {
            // Sort points by height.
            double topX = x1;
            double topY = y1;
            double topAttr = attr1;
            double midX = x2;
            double midY = y2;
            double midAttr = attr2;
            double bottomX = x3;
            double bottomY = y3;
            double bottomAttr = attr3;
            //            double tmp;
            if (topY > midY)
            {
                Swap(ref topX, ref midX);
                Swap(ref topY, ref midY);
                Swap(ref topAttr, ref midAttr);
            }
            if (midY > bottomY)
            {
                Swap(ref midX, ref bottomX);
                Swap(ref midY, ref bottomY);
                Swap(ref midAttr, ref bottomAttr);
            }
            if (topY > midY)
            {
                Swap(ref topX, ref midX);
                Swap(ref topY, ref midY);
                Swap(ref topAttr, ref midAttr);
            }

            // Degenerate triangle?
            if (Math.Abs(bottomY - topY) <= double.Epsilon)
            {
                return;
            }

            // Calculate x and attributes of halfway point (point at height of midY, along edge from top point to bottom point)
            // and sort halfway x and middle x by increasing order.
            double frac = (midY - topY) / (bottomY - topY);
            double leftMidX = midX;
            double leftMidAttr = midAttr;
            double rightMidX = topX + frac * (bottomX - topX);
            double rightMidAttr = topAttr + (bottomAttr - topAttr) * frac;
            if (leftMidX > rightMidX)
            {
                Swap(ref leftMidX, ref rightMidX);
                Swap(ref leftMidAttr, ref rightMidAttr);
            }

            // Draw top trapezium.
            InterpolateTrapezium_1Double(topY, midY, topX, topAttr, topX, topAttr, leftMidX, leftMidAttr, rightMidX, rightMidAttr, drawSpan);

            // Draw bottom trapezium.
            InterpolateTrapezium_1Double(midY, bottomY, leftMidX, leftMidAttr, rightMidX, rightMidAttr, bottomX, bottomAttr, bottomX, bottomAttr, drawSpan);
        }
*/

        /*
                        public void drawTexturedSpan(int y, int left, int right,
                                                     int leftU, int leftV, int deltaU, int deltaV,
                                                     Buffer2D texture)
                        {
                            // Vertical interlace.
                            y = rowStart + y * rowStep;

                            // Check for invalid input.
                            Debug.Assert(left <= right, "left <= right");
                            //    Debug.Assert(y >= 0 && y < height, "y >= 0 && y < height");
                            //    Debug.Assert(left >= 0 && left < width, "left >= 0 && left < width");
                            //    Debug.Assert(right >= 0 && right < width, "right >= 0 && right < width");

                            if (y < 0 || y >= height || right < 0 || left >= width)
                            {
                                return;
                            }

                            // Clip horizontal extent of span.
                            if (left < 0)
                            {
                                left = 0;
                            }
                            if (right >= width)
                            {
                                right = width - 1;
                            }
                            Debug.Assert(left <= right, "left <= right");

                            // Horizontal interlace.
                            left += Math.abs(left % colStep - colStart % colStep);

                            // Draw pixels in span.
                            int dest = y * width + left;
                            int u = leftU;
                            int v = leftV;
                            for (int x = left; x <= right; x += colStep)
                            {
                                //      pixels[dest] = texture.getPixel(FixedPt.toInt(u), FixedPt.toInt(v));
                                pixels[dest] = (short)(v * 11);
                                dest += colStep;
                                u += deltaU;
                                v += deltaV;
                            }
                        }

                          // Given a four sided figure with two opposite sides parallel to the x-axis,
                          // generate a list of the spans where it covers pixels on the screen.
                          // Input coordinates are fixed-point.
                          private static void rasteriseTexturedTrapezium(int top, int bottom,
                                                                         int topLeftX, int topRightX,
                                                                         int bottomLeftX, int bottomRightX,
                                                                         int topLeftU, int topRightU,
                                                                         int bottomLeftU, int bottomRightU,
                                                                         int topLeftV, int topRightV,
                                                                         int bottomLeftV, int bottomRightV,
                                                                         Buffer2D dest, Buffer2D texture)
                          {
                            // Nothing to do?
                            if (bottom == top ||
                                (topLeftX == topRightX && bottomLeftX == bottomRightX))
                            {
                              return;
                            }

                            // Illegal arguments?
                            Debug.Assert(top <= bottom, "top <= bottom");
                            Debug.Assert(topLeftX <= topRightX, "topLeftX <= topRightX");
                            Debug.Assert(bottomLeftX <= bottomRightX, "bottomLeftX <= bottomRightX");

                            // Ensure that adjacent triangles do not overlap horizontally.
                            if (topRightX - topLeftX >= FixedPt.one)
                            {
                              topRightX -= FixedPt.one;
                            }
                            if (bottomRightX - bottomLeftX >= FixedPt.one)
                            {
                              bottomRightX -= FixedPt.one;
                            }
                            Debug.Assert(topLeftX <= topRightX, "topLeftX <= topRightX");
                            Debug.Assert(bottomLeftX <= bottomRightX, "bottomLeftX <= bottomRightX");

                            // Compute edge gradients.
                            int height = bottom - top;
                            int leftX = topLeftX;
                            int rightX = topRightX;
                            int leftU = topLeftU;
                            int rightU = topRightU;
                            int leftV = topLeftV;
                            int rightV = topRightV;
                            int leftDeltaX = FixedPt.divide(bottomLeftX - topLeftX, height);
                            int rightDeltaX = FixedPt.divide(bottomRightX - topRightX, height) + 1;
                            int leftDeltaU = FixedPt.divide(bottomLeftU - topLeftU, height);
                            int rightDeltaU = FixedPt.divide(bottomRightU - topRightU, height) + 1;
                            int leftDeltaV = FixedPt.divide(bottomLeftV - topLeftV, height);
                            int rightDeltaV = FixedPt.divide(bottomRightV - topRightV, height) + 1;
 
                            //int horizDeltaU = 0;
                            //int horizDeltaV = 0;
                            //if(topRightX != topLeftX)
                            //{
                            //  horizDeltaU = FixedPt.divide(topRightU - topLeftU, topRightX - topLeftX);
                            //  horizDeltaV = FixedPt.divide(topRightV - topLeftV, topRightX - topLeftX);
                            //}
                            //else
                            //{
                            //  horizDeltaU = FixedPt.divide(bottomRightU - bottomLeftU, bottomRightX - bottomLeftX);
                            //  horizDeltaV = FixedPt.divide(bottomRightV - bottomLeftV, bottomRightX - bottomLeftX);
                            //}
            
                            // Adjust initial left and right to vertical pixel centre.
                            int firstRow = FixedPt.toInt(top);
                            int lastRow = FixedPt.toInt(bottom);
                            leftX += FixedPt.multiply(FixedPt.one - (top - FixedPt.fromInt(firstRow)), leftDeltaX);
                            rightX += FixedPt.multiply(FixedPt.one - (top - FixedPt.fromInt(firstRow)), rightDeltaX);

                            // For each row between top and bottom, trace the left and right edges.
                            for (int row = firstRow; row < lastRow; row++)
                            {
                              Debug.Assert(leftX <= rightX, "left <= right");

                              // Find where the left and right edges intersect this row.
                              int spanLeft = FixedPt.toInt(leftX);
                              int spanRight = FixedPt.toInt(rightX);

                              // Compute texture gradient across span.
                              int horizDeltaU = 0; 
                              int horizDeltaV = 0;
                              int spanWidth = rightX - leftX;
                              if(spanWidth > 0)
                              {
                                horizDeltaU = FixedPt.divide(rightU - leftU, spanWidth);
                                horizDeltaV = FixedPt.divide(rightV - leftV, spanWidth);
                              }
              
                              // Draw the pixels in this row that fall between the two edges.
                        //      dest.drawSpan(row, spanLeft, spanRight, 0x1234);
                              dest.drawTexturedSpan(row, spanLeft, spanRight,
                                                    leftU, leftV, horizDeltaU, horizDeltaV, texture);
                        //      polygonSpans.setSpan(row, spanLeft, spanRight);

                              // Jump to the next row, moving along the left and right edges,
                              // and along the left edge in texture space.
                              leftX += leftDeltaX;
                              rightX += rightDeltaX;
                              leftU += leftDeltaU;
                              rightU += rightDeltaU;
                              leftV += rightDeltaV;
                              rightV += rightDeltaV;
                            }
                          }
          
                          // Generate a list of the spans where a triangle covers pixels on the screen.
                          // Input coordinates are fixed-point.
                          public static void rasteriseTexturedTriangle(int x1, int y1, int u1, int v1,
                                                                       int x2, int y2, int u2, int v2,
                                                                       int x3, int y3, int u3, int v3,
                                                                       Buffer2D dest, Buffer2D texture
                                                                       )
                          {
                            // Sort points by height.
                            int topX = x1;
                            int topY = y1;
                            int topU = u1;
                            int topV = v1;
                            int midX = x2;
                            int midY = y2;
                            int midU = u2;
                            int midV = v2;
                            int bottomX = x3;
                            int bottomY = y3;
                            int bottomU = u3;
                            int bottomV = v3;
                            int tmp;
                            if (topY > midY)
                            {
                              tmp = topY;
                              topY = midY;
                              midY = tmp;
              
                              tmp = topX;
                              topX = midX;
                              midX = tmp;
              
                              tmp = topU;
                              topU = midU;
                              midU = tmp;
              
                              tmp = topV;
                              topV = midV;
                              midV = tmp;
                            }
                            if (midY > bottomY)
                            {
                              tmp = midY;
                              midY = bottomY;
                              bottomY = tmp;
              
                              tmp = midX;
                              midX = bottomX;
                              bottomX = tmp;

                              tmp = midU;
                              midU = bottomU;
                              bottomU = tmp;

                              tmp = midV;
                              midV = bottomV;
                              bottomV = tmp;
                            }
                            if (topY > midY)
                            {
                              tmp = topY;
                              topY = midY;
                              midY = tmp;
              
                              tmp = topX;
                              topX = midX;
                              midX = tmp;

                              tmp = topU;
                              topU = midU;
                              midU = tmp;

                              tmp = topV;
                              topV = midV;
                              midV = tmp;
                            }

                            // Degenerate triangle?
                            if (bottomY == topY)
                            {
                              return;
                            }

                            // Calculate x of halfway point (point at height of midY, along edge from top point to bottom point)
                            // and sort halfway x and middle x by increasing order.
                            int midFraction = FixedPt.divide(midY - topY, bottomY - topY);
                            int leftMidX = topX + FixedPt.multiply(bottomX - topX, midFraction);
                            int rightMidX = midX;
                            int leftMidU = topU + FixedPt.multiply(bottomU - topU, midFraction);
                            int rightMidU = midU;
                            int leftMidV = topV + FixedPt.multiply(bottomV - topV, midFraction);
                            int rightMidV = midV;
                            if (leftMidX > rightMidX)
                            {
                              tmp = leftMidX;
                              leftMidX = rightMidX;
                              rightMidX = tmp;

                              tmp = leftMidU;
                              leftMidU = rightMidU;
                              rightMidU = tmp;
              
                              tmp = leftMidV;
                              leftMidV = rightMidV;
                              rightMidV = tmp;
                            }

                            // Prepare the PolygonSpans for rendering.
                        //    polygonSpans.prepare(FixedPt.toInt(topY), FixedPt.toInt(bottomY) - 1);

                            // Draw top trapezium.
                            rasteriseTexturedTrapezium(topY, midY,
                                                       topX, topX, leftMidX, rightMidX,
                                                       topU, topU, leftMidU, rightMidU,
                                                       topV, topV, leftMidV, rightMidV,
                                                       dest, texture);

                            // Draw bottom trapezium.
                            rasteriseTexturedTrapezium(midY, bottomY,
                                                       leftMidX, rightMidX, bottomX, bottomX,
                                                       leftMidU, rightMidU, bottomU, bottomU,
                                                       leftMidV, rightMidV, bottomV, bottomV,
                                                       dest, texture);
                          }
                */
    }
}
