using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CLHelpFunctions
{
    public class NewImage
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public byte[] Data { get; set; }

        public int Depth { get; set; }


        public NewImage(int Width, int Height, int Depth, byte[] Data = null)
        {
            this.Data = Data ?? new byte[Depth * Width * Height];
            this.Width = Width;
            this.Height = Height;
            this.Depth = Depth;
        }

        public NewImage(string ImagePath)
        {
            using var image = (Bitmap)Image.FromFile(ImagePath);
            PopulateFrom(image);
        }



        public NewImage(Bitmap Image)
        {
            PopulateFrom(Image);
        }

        public NewImage GetRect(Rectangle Rect)
        {
            var rectImg = new NewImage(Rect.Width, Rect.Height, this.Depth);
            for (int x = 0; x < Rect.Width; x++)
                for (int y = 0; y < Rect.Height; y++)
                    for (int k = 0; k < Depth; k++)
                    {
                        rectImg.Data[y * rectImg.Depth * rectImg.Width + x * rectImg.Depth + k] =
                            Data[(y + Rect.Y) * Depth * Width + (x + Rect.X) * Depth + k];
                    }
            return rectImg;
        }

        private void PopulateFrom(Bitmap Image)
        {
            this.Width = Image.Width;
            this.Height = Image.Height;
            BitmapData srcData = Image.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, Image.PixelFormat);
            int stride = srcData.Stride;
            int srcDepth = 0;
            switch (Image.PixelFormat)
            {
                case PixelFormat.Format24bppRgb:
                    srcDepth = 3;
                    break;
                case PixelFormat.Format32bppArgb:
                    srcDepth = 4;
                    break;
                case PixelFormat.Format8bppIndexed:
                    srcDepth = 1;
                    break;
                default:
                    throw new Exception("Image Format Not Supported!");

            }
            this.Depth = 3;
            var pixels = new byte[3 * Width * Height];

            int nstride = 3 * Width;
            unsafe
            {
                var src = (byte*)srcData.Scan0.ToPointer();
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        pixels[y * nstride + 3 * x] = src[y * stride + srcDepth * x];
                        pixels[y * nstride + 3 * x + 1] = src[y * stride + srcDepth * x + 1];
                        pixels[y * nstride + 3 * x + 2] = src[y * stride + srcDepth * x + 2];
                    }
            }
            this.Data = pixels;
        }


        public NewImage ConvertToDepth(int ToDepth)
        {
            NewImage convertedImage = null;
            int stride = Depth * Width;
            int cstride = ToDepth * Width;
            var convertedData = new byte[cstride * Height];
            const float cr = 0.5f;
            const float cg = 0.419f;
            const float cb = 0.081f;
            switch (this.Depth)
            {
                case 1:

                    switch (ToDepth)
                    {
                        case 3:
                            for (int y = 0; y < Height; y++)
                                for (int x = 0; x < Width; x++)
                                {
                                    convertedData[y * cstride + ToDepth * x]
                                    = convertedData[y * cstride + ToDepth * x + 1]
                                        = convertedData[y * cstride + ToDepth * x + 2]
                                        = Data[y * stride + Depth * x];
                                }
                            break;

                        case 4:
                            for (int y = 0; y < Height; y++)
                                for (int x = 0; x < Width; x++)
                                {
                                    convertedData[y * cstride + ToDepth * x]
                                    = convertedData[y * cstride + ToDepth * x + 1]
                                        = convertedData[y * cstride + ToDepth * x + 2]
                                        = Data[y * stride + Depth * x];
                                    convertedData[y * cstride + ToDepth * x + 3] = 255;
                                }
                            break;
                        default:
                            throw new Exception("Image Depth to Converted Not Supported");

                    }
                    break;
                case 3:
                    switch (ToDepth)
                    {
                        case 1:
                            for (int y = 0; y < Height; y++)
                                for (int x = 0; x < Width; x++)
                                    convertedData[y * cstride + ToDepth * x] = (byte)(cb * Data[y * stride + Depth * x] + cg * Data[y * stride + Depth * x + 1] + cr * Data[y * stride + Depth * x + 2]);
                            break;
                        case 4:
                            for (int y = 0; y < Height; y++)
                                for (int x = 0; x < Width; x++)
                                {
                                    convertedData[y * cstride + ToDepth * x] = Data[y * stride + Depth * x];
                                    convertedData[y * cstride + ToDepth * x + 1] = Data[y * stride + Depth * x + 1];
                                    convertedData[y * cstride + ToDepth * x + 2] = Data[y * stride + Depth * x + 2];
                                    convertedData[y * cstride + ToDepth * x + 3] = 255;
                                }
                            break;
                        default:
                            throw new Exception("Image Depth to Converted Not Supported");
                    }
                    break;
                case 4:
                    switch (ToDepth)
                    {
                        case 1:
                            for (int y = 0; y < Height; y++) 
                                for (int x = 0; x < Width; x++)
                                    convertedData[y * cstride + ToDepth * x] = (byte)(cb * Data[y * stride + Depth * x] + cg * Data[y * stride + Depth * x + 1] + cr * Data[y * stride + Depth * x + 2]);

                            break;
                        case 3:
                            for (int y = 0; y < Height; y++)
                                for (int x = 0; x < Width; x++)
                                {
                                    convertedData[y * cstride + ToDepth * x] = Data[y * stride + Depth * x];
                                    convertedData[y * cstride + ToDepth * x + 1] = Data[y * stride + Depth * x + 1];
                                    convertedData[y * cstride + ToDepth * x + 2] = Data[y * stride + Depth * x + 2];
                                }
                            break;
                        default:
                            throw new Exception("Image Depth to Converted Not Supported");
                    }
                    break;
                default:
                    throw new Exception("Image Depth Not Supported!");
            }
            return convertedImage;
        }

        public Bitmap GetBitmap()
        {
            var imageData2 = new byte[4 * Width * Height];
            switch (Depth)
            {
                case 1:
                    for (int y = 0; y < Height; y++)
                        for (int x = 0; x < Width; x++)
                        {
                            var value = Data[y * Depth * Width + x];
                            imageData2[y * 4 * Width + 4 * x] = value;
                            imageData2[y * 4 * Width + 4 * x + 1] = value;
                            imageData2[y * 4 * Width + 4 * x + 2] = value;
                            imageData2[y * 4 * Width + 4 * x + 3] = 255;
                        }

                    break;
                case 3:
                    for (int y = 0; y < Height; y++)
                        for (int x = 0; x < Width; x++)
                        {
                            imageData2[y * 4 * Width + 4 * x] = Data[y * Depth * Width + Depth * x];
                            imageData2[y * 4 * Width + 4 * x + 1] = Data[y * Depth * Width + Depth * x + 1];
                            imageData2[y * 4 * Width + 4 * x + 2] = Data[y * Depth * Width + Depth * x + 2];

                            imageData2[y * 4 * Width + 4 * x + 3] = 255;
                        }

                    break;
                case 4:
                    for (int y = 0; y < Height; y++)
                        for (int x = 0; x < Width; x++)
                        {
                            imageData2[y * 4 * Width + 4 * x] = Data[y * Depth * Width + Depth * x];
                            imageData2[y * 4 * Width + 4 * x + 1] = Data[y * Depth * Width + Depth * x + 1];
                            imageData2[y * 4 * Width + 4 * x + 2] = Data[y * Depth * Width + Depth * x + 2];

                            imageData2[y * 4 * Width + 4 * x + 3] = Data[y * Depth * Width + Depth * x + 3];
                        }

                    break;
                default:
                    throw new Exception("Not supported depth");
            }

            Bitmap img;
            unsafe
            {
                fixed (byte* ptr = imageData2)
                {
                    img = new Bitmap(Width, Height, Width * 4,
                        PixelFormat.Format32bppArgb, new IntPtr(ptr));
                }
            }
            return img;
        }

        public void Save(string fileName)
        {
            var image = GetBitmap();
            image.Save(fileName);
            image.Dispose();
        }

        public NewImage Copy()
        {
            byte[] copyimage = new byte[Data.Length];
            Data.CopyTo(copyimage, 0);

            return new NewImage(Width, Height, Depth, copyimage);
        }

        public Bitmap ResizeHq(float scale)
        {
            var newWidth = (int)Math.Round(scale * Width);
            var newHeight = (int)Math.Round(scale * Height);
            var destRect = new Rectangle(0, 0, newWidth, newHeight);
            var destImage = new Bitmap(newWidth, newHeight);


            using (var img = GetBitmap())
            {
                destImage.SetResolution(img.HorizontalResolution, img.VerticalResolution);
                using (var graphics = Graphics.FromImage(destImage))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    using (var wrapMode = new ImageAttributes())
                    {
                        wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                        graphics.DrawImage(img, destRect, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, wrapMode);
                    }
                }
            }

            return destImage;
        }
    }

}
