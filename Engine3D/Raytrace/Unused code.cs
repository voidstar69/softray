namespace Engine3D
{
    public class Raytracer
    {
/*
        private void renderDisplacedGeometry()
        {
            // Show displacement map
            for (int y = 0; y < displacementMap.Height; y++)
            {
                for (int x = 0; x < displacementMap.Width; x++)
                {
                    surface.DrawPixel(x, y, displacementMap.GetPixel(x, y));
                }
            }

            // Project displacement map onto viewport.
            for (int x = 0; x < surface.Width; x++)
            {
                for (int y = surface.Height - 1; y >= 0; y--)
                {
                    surface.DrawPixel(x, y, (uint)(y * x));
                }
            }
        }

        private static Surface MakeDisplacementMap()
        {
            return GenerateMap();
        }
*/

/*
        private void LoadTexture()
        {
            Debug.WriteLine("LoadTexture");

            BitmapImage bitmap = new BitmapImage(new Uri("image.jpg", UriKind.RelativeOrAbsolute));
            bitmap.CreateOptions = BitmapCreateOptions.None;

            Image image = new Image();
            image.Source = bitmap;
            canvas.Children.Add(image);

            WriteableBitmap bitmapBuffer = new WriteableBitmap(image, null);
//            bitmapBuffer.Invalidate();

            // TODO: bitmap not loaded yet, so resolution is 0x0. Force bitmap to load somehow, or wait until it loads async?
            textureWidth = bitmapBuffer.PixelWidth;
            textureHeight = bitmapBuffer.PixelHeight;

            texture = new int[textureWidth * textureHeight];

            uint texelIndex = 0;
            for (uint y = 0; y < textureHeight; y++)
            {
                for (uint x = 0; x < textureWidth; x++)
                {
                    texture[texelIndex++] = bitmapBuffer.Pixels[texelIndex];
                }
            }
        }
*/

/*
*/
    }
}
