using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Engine3D;

namespace Engine3D.Raytrace
{
    // Iterates over the grid cells intersected by a line/ray, in the order of intersection
    public class LineWalker3D
    {
        // Note: below comments are from original 2D line walker from height-map renderer.
        // TODO: gaps sometimes appear between heightmap columns. Are cells missed out when walking a line along X or Y axis along cell boundaries?
        // TODO: when moving the camera, strange artifacts show up along diagonal ray directions. Probably due to Bresenham-style diagonal lines
        // hitting diagonally adjacent cells, whereas here we need to hit every cell that the floating-point line crosses, i.e. a 'thick' line.
        // Looks like bilinear-filtered height wobbling artifact might be due to this too.
        // TODO: slow camera movement makes it easy to spot visual bugs.
        public static IEnumerable<Vector> WalkLine(Vector start, Vector end, double minStep = 1.0)
        {
            var delta = end - start;
            if (!delta.IsZeroVector)
            {
                var absDelta = new Vector(Math.Abs(delta.x), Math.Abs(delta.y), Math.Abs(delta.z));
                var maxDimSize = Math.Max(Math.Max(absDelta.x, absDelta.y), absDelta.z);
                int numSteps = Math.Max(1, (int)(maxDimSize / minStep));
                delta *= minStep / maxDimSize;

                var pos = start;
                for (var step = 0; step < numSteps; step++)
                //for (double x = startX, y = startY; x != endX && y != endY; x += dx, y += dy)
                {
                    yield return pos;
                    pos += delta;
                }
            }
        }

        // Test line walking by drawing lots of 2D lines on a view, to see how they look.
        public static void TestLineWalking(Surface view)
        {
            Contract.Requires(view != null);
            const int jump = 10;

            for (int col = 0; col < view.Width - 1; col++)
            {
                view.DrawPixel(col, 0, Color.Pink.ToARGB());
                view.DrawPixel(col, view.Height - 1, Color.Pink.ToARGB());
            }

            for (int col = 0; col < view.Height - 1; col++)
            {
                view.DrawPixel(0, col, Color.Cyan.ToARGB());
                view.DrawPixel(view.Width - 1, col, Color.Cyan.ToARGB());
            }

            for (int col = 0; col < view.Width / jump; col++)
            {
                foreach (var pt in LineWalker3D.WalkLine(new Vector(col * jump, 0, 0), new Vector(view.Width / 2, view.Height / 2, 0)))
                {
                    view.DrawPixel((int)(pt.x + 0.5), (int)(pt.y + 0.5), Color.Blue.ToARGB());
                }

/*
                foreach (var pt in LineWalker3D.WalkLine(col * jump, view.Height - 1, view.Width / 2, view.Height / 2))
                {
                    view.DrawViewPixel((int)(pt.x + 0.5), (int)(pt.y + 0.5), Color.Green.ToARGB());
                }
 */
            }

/*
            for (int col = 0; col < view.Height / jump; col++)
            {
                foreach (var pt in LineWalker3D.WalkLine(0, col * jump, view.Width / 2, view.Height / 2))
                {
                    view.DrawViewPixel((int)(pt.x + 0.5), (int)(pt.y + 0.5), Color.Red.ToARGB());
                }

                foreach (var pt in LineWalker3D.WalkLine(view.Width - 1, col * jump, view.Width / 2, view.Height / 2))
                {
                    view.DrawViewPixel((int)(pt.x + 0), (int)(pt.y + 0), Color.Yellow.ToARGB());
                }
            }
*/


            //for (int row = 0; row < view.Height; row++)
            //{
            //    for (int col = 0; col < view.Width; col++)
            //    {
            //        Vector start = new Vector(mouseViewPos.x, mouseViewPos.y);
            //        Vector end = new Vector(col, row);
            //        var hitPt = map.IntersectWorldLine(start, end, 1);
            //        var color = hitPt.IsInvalid() ? Color.Red : Color.Green;

            //        //var color = map.IsSolidTile(col, row) ? Color.Red : Color.Green;

            //        view.DrawViewPixel(col, row, color.ToARGB());
            //    }
            //}

        }
    }
}