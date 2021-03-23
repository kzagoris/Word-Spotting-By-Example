namespace DoLFLibrary
{
    internal class ZagImage<T>
    {
        public T[] Data { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        
        public int Depth {get;set;}

        public int Stride => _stride;

        private int _stride;

        public ZagImage(T[] tImage, int tWidth, int tHeight, int tDepth = 3)
        {
            this.Data = tImage;
            this.Width = tWidth;
            this.Height = tHeight;
            this.Depth = tDepth;
            _stride = Width * Depth;
        }

        public ZagImage(int tWidth, int tHeight, int tDepth = 3)
        {
            this.Width = tWidth; this.Height = tHeight; this.Depth = tDepth;
            this.Data = new T[tDepth * tWidth * tHeight];
            _stride = tWidth * tDepth;
        }

        

        
        public ZagImage() { }
        public ZagImage<T> Copy()
        {
            var copyimage = new T[Data.Length];
            Data.CopyTo(copyimage, 0);
            return new ZagImage<T>(copyimage, Width, Height, Depth);
        }

        public void SetValue (T value)
        {
            for (int i = 0; i < Data.Length; i++)
                Data[i] = value;
        }



        //public void SaveTif(string FileName)
        //{
        //    BitmapSource test = BitmapSource.Create(this.Width, this.Height, 96, 96, PixelFormats.Bgra32, null, this.Image, 4 * Width);
        //    TiffBitmapEncoder enc = new TiffBitmapEncoder();
        //    enc.Frames.Add(BitmapFrame.Create(test));
        //    FileStream fs = new FileStream(FileName, FileMode.Create);
        //    enc.Save(fs);
        //    fs.Close();
        //}

        //public static NewImage LoadTif(string FileName)
        //{
        //    FormatConvertedBitmap myFC = new FormatConvertedBitmap();
        //    TiffBitmapDecoder dec = new TiffBitmapDecoder(new Uri(FileName, UriKind.Relative), BitmapCreateOptions.None, BitmapCacheOption.Default);
        //    myFC.BeginInit();
        //    myFC.Source = dec.Frames[0];
        //    myFC.DestinationFormat = PixelFormats.Bgr32;
        //    myFC.EndInit();
        //    return new NewImage(myFC);
        //}



        //public BitmapSource GetBitmapSource()
        //{
        //    return BitmapSource.Create(this.Width, this.Height, 96, 96, PixelFormats.Bgra32, null, this.Image, 4 * Width);
        //}

        //public NewImage(BitmapSource image, PixelFormat mypixelformat = PixelFormat.Bgra)
        //{
        //    this.Width = image.PixelWidth;
        //    this.Height = image.PixelHeight;
        //    int s = (mypixelformat == PixelFormat.Bgra) ? 4 : 1;
        //    this.Image = new byte[s * Width * Height];
        //    int stride = s * Width;
        //    image.CopyPixels(this.Image, stride, 0);
        //}



        //public NewImage(Image<Bgra, Byte> image)
        //{
        //    this.Width = image.Width; this.Height = image.Height;
        //    int stride = 4 * image.Width;
        //    this.Image = new byte[stride * image.Height];
        //    for (int y = image.Height - 1; y >= 0; y--)
        //    {
        //        for (int x = image.Width - 1; x >= 0; x--)
        //        {
        //            for (int k = 0; k <= 3; k++)
        //            {
        //                this.Image[4 * x + k + y * stride] = image.Data[y, x, k];
        //            }
        //        }
        //    }

        //}

        //public NewImage(Image<Bgr, Byte> image)
        //{
        //    this.Width = image.Width; this.Height = image.Height;
        //    int stride = 4 * image.Width;
        //    this.Image = new byte[stride * image.Height];
        //    for (int y = image.Height - 1; y >= 0; y--)
        //    {
        //        for (int x = image.Width - 1; x >= 0; x--)
        //        {
        //            for (int k = 0; k <= 2; k++)
        //            {
        //                this.Image[4 * x + k + y * stride] = image.Data[y, x, k];
        //            }
        //            this.Image[4 * x + 3 + y * stride] = 255;
        //        }
        //    }

        //}

        
    }


}
