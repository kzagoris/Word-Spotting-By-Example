
using System;
using System.Collections.Generic;
using System.Linq;

namespace DoLFLibrary
{
    internal class DsLPsDetectorv2wFeatures
    {

        private static readonly Dictionary<int, int[]> gradientLevels = new Dictionary<int, int[]>
        {
            { 2, new[] { 0 }  },
            { 3, new[]  { -60, 60 } },
            { 4, new[]   { -90, 0, 90 } },
            { 5, new[] { -108, -36, 36, 108 } }
        };

        private static readonly int numberGradientLevels = 4;


        #region Settings

        internal int WindowSize { get; set; }

        internal int N { get; set; }

        internal DoLF.QuantizationLevelsNum QuantizationLevels { get; set; }

        internal int MinCCSize { get; set; }

        internal bool DynamicWindow { get; set; }

        internal bool Quantization { get; set; }

        #endregion




        public List<DoLF.DsLPoints> myDsLPs { get; set; }



        private class ConvMatrix
        {

            public int Factor { get; set; }

            public int Offset { get; set; }

            private int[] _matrix;

            public int[] Matrix
            {
                get { return _matrix; }
                set
                {
                    _matrix = value;

                    Factor = 0;
                    for (int y = 0; y < Size; y++)
                        for (int x = 0; x < Size; x++)
                            Factor += _matrix[x + y * Size];

                    if (Factor == 0)
                        Factor = 1;
                }
            }





            public int Size { get; set; }


            public ConvMatrix()
            {
                Offset = 0;
                Factor = 1;
            }

            public ConvMatrix(int[] Matrix, int Size)
            {
                Offset = 0;
                this.Size = Size;
                this.Matrix = Matrix;

            }


        }


        private void SetDebugImage<T>(ZagImage<T> Test)
        {
            if (typeof(T) == typeof(byte))
            {
                DoLF.TestImageData = new byte[Test.Data.Length];
                Test.Data.CopyTo(DoLF.TestImageData, 0);
            }
            else if (typeof(T) == typeof(int))
            {
                DoLF.TestMatrixData = new int[Test.Data.Length];
                Test.Data.CopyTo(DoLF.TestMatrixData, 0);
            }
            else if (typeof(T) == typeof(float))
            {
                var TestInt = Test.Data.Select(x => Convert.ToInt32(x)).ToArray();
                DoLF.TestMatrixData = new int[Test.Data.Length];
                TestInt.CopyTo(DoLF.TestMatrixData, 0);

            }
            DoLF.TestImageWidth = Test.Width;
            DoLF.TestImageHeight = Test.Height;
            DoLF.Depth = Test.Depth;
        }



        public void CalcDoLFs(ZagImage<byte> Img)
        {
            if (Img.Width > 5 && Img.Height > 5)
            {

                ZagImage<byte> origImg = GetGrayscaleRMY(Img);

                ZagImage<byte> mySmoothMedianImg = SmoothMedian(origImg);

                mySmoothMedianImg = SmoothGaussian(mySmoothMedianImg);

                Tuple<ZagImage<int>, ZagImage<float>> results = firstGrandient(mySmoothMedianImg);
                ZagImage<int> orientationMatrix = results.Item1;
                ZagImage<float> magnitudeMatrix = results.Item2;


                // smoothing
                orientationMatrix = SmoothMedian(orientationMatrix);

                orientationMatrix = SmoothAdaptive(orientationMatrix);
                ZagImage<int> quantMatrix = QuantizeFromTables(orientationMatrix);


                ZagImage<int>[] levels = getLevels(quantMatrix, (int)this.QuantizationLevels);
                ZagImage<byte>[] levelImg = levels.Select(x => getLevelImage(x)).ToArray();
                var totalPoints = new List<DoLF.DsLPoints>();
                for (int i = 0; i < levelImg.Length; i++)
                {

                    List<DoLF.DsLPoints> points = getCenterofGravityPoints(levelImg[i], this.MinCCSize);


                    totalPoints.AddRange(points);
                }


                myDsLPs = totalPoints;

                for (int i = 0; i < myDsLPs.Count(); i++)
                {
                    DoLF.DsLPoints p = myDsLPs[i];
                    //p.Gradient = quantMatrix.Data[(int)(p.Y * quantMatrix.Stride + p.X)];
                }




                CalcFeatures(origImg, quantMatrix, magnitudeMatrix);

            }
            else
            {
                myDsLPs = new List<DoLF.DsLPoints>();
            }
        }






        private void Invert(ref ZagImage<byte> SourceImage)
        {
            int stride = 3 * SourceImage.Width;
            int x, y;
            // for each line
            for (y = 0; y < SourceImage.Height; y++)
            {
                // for each pixel
                for (x = 0; x < SourceImage.Width; x++)
                {
                    SourceImage.Data[3 * x + y * stride] = (byte)(255 - SourceImage.Data[3 * x + y * stride]);
                    SourceImage.Data[3 * x + y * stride + 1] = (byte)(255 - SourceImage.Data[3 * x + y * stride + 1]);
                    SourceImage.Data[3 * x + y * stride + 2] = (byte)(255 - SourceImage.Data[3 * x + y * stride + 2]);
                }
            }
        }

        private ZagImage<byte> GetGrayscaleRMY(ZagImage<byte> SourceImage)
        {
            var gray = new ZagImage<byte>(SourceImage.Width, SourceImage.Height, 1);

            double cr = 0.5, cg = 0.419, cb = 0.081;
            // get width and height

            int x, y;
            // for each line
            for (y = 0; y < SourceImage.Height; y++)
            {
                // for each pixel
                for (x = 0; x < SourceImage.Width; x++)
                {
                    var greyValue = (byte)(cb * SourceImage.Data[SourceImage.Depth * x + y * SourceImage.Stride] + cg * SourceImage.Data[SourceImage.Depth * x + 1 + y * SourceImage.Stride] + cr * SourceImage.Data[SourceImage.Depth * x + 2 + y * SourceImage.Stride]);
                    gray.Data[gray.Depth * x + y * gray.Stride] = greyValue;
                }
            }
            return gray;
        }












        public void CalcFeatures(ZagImage<byte> origImg, ZagImage<int> gradientImg, ZagImage<float> Magnitude)
        {
            var newGradient = firstGrandientWithoutThreshold(origImg);

            int windowR = 0;
            DynamicWindow = false;
            for (int i = 0; i < myDsLPs.Count(); i++)
            {
                float[] features = null;


                myDsLPs[i].WindowSize = (DynamicWindow) ? CalcWindowSizeAverage(myDsLPs[i], origImg, this.WindowSize, 4, this.N) : this.WindowSize;

                windowR = myDsLPs[i].WindowSize / N;

                features = GradientDescriptorNxN(myDsLPs[i], newGradient.Item1, newGradient.Item2, windowR, N);


                NormalizeDescriptorAndThresholding(ref features, 0.2f);


                myDsLPs[i].Descriptor = features;
            }


        }



        private ZagImage<byte> Convolution(ZagImage<byte> myImage, ConvMatrix Mask)
        {
            int s = Mask.Size / 2;
            var cImage = myImage.Copy();
            for (int y = s; y < myImage.Height - s; y++)
            {
                for (int x = s; x < myImage.Width - s; x++)
                {
                    int r = 0, g = 0, b = 0;
                    for (int masky = 0; masky < Mask.Size; masky++)
                    {
                        for (int maskx = 0; maskx < Mask.Size; maskx++)
                        {
                            var maskValue = Mask.Matrix[maskx + masky * Mask.Size];

                            b += maskValue * myImage.Data[myImage.Depth * (x + maskx - s) + (y + masky - s) * myImage.Stride];
                            if (myImage.Depth == 3)
                            {
                                r += maskValue * myImage.Data[myImage.Depth * (x + maskx - s) + 2 + (y + masky - s) * myImage.Stride];
                                g += maskValue * myImage.Data[myImage.Depth * (x + maskx - s) + 1 + (y + masky - s) * myImage.Stride];
                            }

                        }
                    }
                    b = Math.Min(Math.Max((b / Mask.Factor) + Mask.Offset, 0), 255);
                    cImage.Data[myImage.Depth * x + y * myImage.Stride] = (byte)b;
                    if (myImage.Depth == 3)
                    {
                        r = Math.Min(Math.Max((r / Mask.Factor) + Mask.Offset, 0), 255);
                        g = Math.Min(Math.Max((g / Mask.Factor) + Mask.Offset, 0), 255);
                        cImage.Data[myImage.Depth * x + 1 + y * myImage.Stride] = (byte)g;
                        cImage.Data[myImage.Depth * x + 2 + y * myImage.Stride] = (byte)r;
                    }
                }
            }
            return cImage;
        }

        private Tuple<ZagImage<int>, ZagImage<int>> getGradients(ZagImage<byte> myImage)
        {

            var gradientX = new ZagImage<int>(myImage.Width, myImage.Height, 1);
            var gradientY = new ZagImage<int>(myImage.Width, myImage.Height, 1);

            for (int y = 1; y < myImage.Height - 1; y++)
            {
                for (int x = 1; x < myImage.Width - 1; x++)
                {

                    int gX = myImage.Data[(x + 1) + y * myImage.Stride] - myImage.Data[(x - 1) + y * myImage.Stride];
                    int gY = myImage.Data[x + (y + 1) * myImage.Stride] - myImage.Data[x + (y - 1) * myImage.Stride];

                    gradientX.Data[x + y * myImage.Stride] = gX;
                    gradientY.Data[x + y * myImage.Stride] = gY;



                }
            }
            return new Tuple<ZagImage<int>, ZagImage<int>>(gradientX, gradientY);
        }



        public Tuple<ZagImage<int>, ZagImage<float>> firstGrandient(ZagImage<byte> origImg)
        {

            //var mygradientX = new ConvMatrix(new int[] { 0, 0, 0, -1, 0, 1, 0, 0, 0 }, 3);
            //var mygradientY = new ConvMatrix(new int[] { 0, -1, 0, 0, 0, 0, 0, 1, 0 }, 3);

            //var imageX = Convolution(origImg, mygradientX);
            //var imageY = Convolution(origImg, mygradientY);

            var gradients = getGradients(origImg);
            var imageX = gradients.Item1;
            var imageY = gradients.Item2;

            int thrX = FindThreshold(imageX) / 2;
            int thrY = FindThreshold(imageY) / 2;
            var magnitude = new ZagImage<float>(origImg.Width, origImg.Height, 1);
            var orientation = new ZagImage<int>(origImg.Width, origImg.Height, 1);

            for (int y = 0; y < orientation.Height; y++)
            {
                for (int x = 0; x < orientation.Width; x++)
                {
                    if (Math.Abs(imageX.Data[x + y * imageX.Stride]) < thrX)
                        imageX.Data[x + y * imageX.Stride] = 0;
                    if (Math.Abs(imageY.Data[x + y * imageY.Stride]) < thrY)
                        imageY.Data[x + y * imageY.Stride] = 0;

                    orientation.Data[x + y * orientation.Stride] =
                        (int)Math.Round(180 * Math.Atan2(imageY.Data[y * imageY.Stride + x], imageX.Data[y * imageX.Stride + x]) / Math.PI);
                    magnitude.Data[y * magnitude.Stride + x] =
                        (float)
                            Math.Sqrt(imageX.Data[y * imageX.Stride + x] * imageX.Data[y * imageX.Stride + x] +
                        imageY.Data[y * imageY.Stride + x] * imageY.Data[y * imageY.Stride + x]);
                }
            }

            return new Tuple<ZagImage<int>, ZagImage<float>>(orientation, magnitude);
        }

        private int FindThreshold(int[] histog)
        {
            double sum = 0, csum = 0;
            int threshold = 0;
            double m1 = 0, m2 = 0, sb = 0, fmax = -1;
            int n = 0, n1 = 0, n2 = 0;
            for (int k = 0; k < histog.Length; k++)
            {
                sum += (k + 1) * histog[k];
                n += histog[k];
            }

            for (int k = 0; k < histog.Length; k++)
            {
                n1 += histog[k];
                if (n1 == 0)
                    continue;
                n2 = n - n1;
                if (n == 0)
                    break;
                csum += (k + 1) * histog[k];
                m1 = csum / n1;
                m2 = (sum - csum) / n2;
                sb = n1 * n2 * (m1 - m2) * (m1 - m2);
                if (sb > fmax)
                {
                    fmax = sb;
                    threshold = k;
                }
            }

            if (threshold == 0)
                threshold = histog.Length / 2;
            return threshold;
        }

        private int FindThreshold(ZagImage<int> SourceImage)
        {
            int width = SourceImage.Width;
            int height = SourceImage.Height;
            var histog = new int[256];
            int k, threshold = 1;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (SourceImage.Data[y * SourceImage.Stride + x] != 0)
                    {
                        histog[Math.Abs(SourceImage.Data[y * SourceImage.Stride + x])]++;
                    }
                }
            }
            double sum = 0, csum = 0, fmax = -1, sb = 0, m1 = 0, m2 = 0;
            int n = 0, n1 = 0, n2 = 0;
            for (k = 0; k < 256; k++)
            {
                sum += (double)k * histog[k];
                n += histog[k];
            }

            for (k = 0; k < 256; k++)
            {
                n1 += histog[k];
                if (n1 == 0)
                    continue;
                n2 = n - n1;
                if (n == 0)
                    break;
                csum += (double)k * histog[k];
                m1 = csum / n1;
                m2 = (sum - csum) / n2;
                sb = (double)n1 * n2 * (m1 - m2) * (m1 - m2);
                if (sb > fmax)
                {
                    fmax = sb;
                    threshold = k;
                }
            }

            if (threshold == 0)
                threshold = 127;
            return threshold;
        }




        public Tuple<ZagImage<int>, ZagImage<float>> firstGrandientWithoutThreshold(ZagImage<byte> origImg)
        {

            var gradients = getGradients(origImg);
            var imageX = gradients.Item1;
            var imageY = gradients.Item2;

            var magnitude = new ZagImage<float>(origImg.Width, origImg.Height);
            var orientation = new ZagImage<int>(origImg.Width, origImg.Height);

            for (int y = 0; y < orientation.Height; y++)
            {
                for (int x = 0; x < orientation.Width; x++)
                {
                    orientation.Data[x + y * orientation.Stride] =
                        (int)Math.Round(180 * Math.Atan2(imageY.Data[y * imageY.Stride + x], imageX.Data[y * imageX.Stride + x]) / Math.PI);
                    magnitude.Data[x + y * orientation.Stride] =
                        (float)
                            Math.Sqrt(imageX.Data[y * imageX.Stride + x] * imageX.Data[y * imageX.Stride + x] +
                        imageY.Data[y * imageY.Stride + x] * imageY.Data[y * imageY.Stride + x]);
                }
            }
            return new Tuple<ZagImage<int>, ZagImage<float>>(orientation, magnitude);
        }


        public ZagImage<int> SmoothMedian(ZagImage<int> quantGradient, int k = 1)
        {
            ZagImage<int> smoothQuantGradient = quantGradient.Copy();

            for (int y = k; y < quantGradient.Height - k; y++)
            {
                for (int x = k; x < quantGradient.Width - k; x++)
                {
                    if (quantGradient.Data[y * quantGradient.Stride + x] != 0)
                    {
                        var points = new List<int>();
                        for (int i = -k; i <= k; i++)
                        {
                            for (int j = -k; j <= k; j++)
                            {
                                points.Add(quantGradient.Data[(y + i) * quantGradient.Stride + (x + j)]);
                            }
                        }
                        smoothQuantGradient.Data[y * smoothQuantGradient.Stride + x] = points.OrderBy(a => a).ToArray()[points.Count() >> 1];
                    }
                }
            }
            return smoothQuantGradient;
        }

        public ZagImage<byte> SmoothMedian(ZagImage<byte> quantGradient, int k = 1)
        {
            ZagImage<byte> smoothQuantGradient = quantGradient.Copy();

            for (int y = k; y < quantGradient.Height - k; y++)
            {
                for (int x = k; x < quantGradient.Width - k; x++)
                {

                    var points = new List<byte>();
                    for (int i = -k; i <= k; i++)
                    {
                        for (int j = -k; j <= k; j++)
                        {
                            points.Add(quantGradient.Data[(y + i) * quantGradient.Stride + (x + j)]);
                        }
                    }
                    smoothQuantGradient.Data[y * smoothQuantGradient.Stride + x] = points.OrderBy(a => a).ToArray()[points.Count() >> 1];
                }
            }
            return smoothQuantGradient;
        }

        public ZagImage<byte> SmoothGaussian(ZagImage<byte> img)
        {
            var gaussMatrix = new ConvMatrix(new[] { 1, 2, 1, 2, 4, 2, 1, 2, 1 }, 3);
            return Convolution(img, gaussMatrix);
        }

        public ZagImage<int> SmoothAdaptive(ZagImage<int> origQuantGradient)
        {
            var quantGradient = new ZagImage<int>(origQuantGradient.Width, origQuantGradient.Height, 1);
            var smoothQuantGradient = new ZagImage<int>(origQuantGradient.Width, origQuantGradient.Height, 1);
            //quantGradient.SetZero();
            smoothQuantGradient.SetValue(127);

            for (int y = 0; y < quantGradient.Height; y++)
                for (int x = 0; x < quantGradient.Width; x++)
                    quantGradient.Data[y * quantGradient.Stride + x] = (int)Math.Round((origQuantGradient.Data[y * origQuantGradient.Stride + x] + 179) * 0.7111111111111);


            int k = 1;
            float factor = 3;
            double weights = 0;
            double weightPixels = 0;
            float Gx = 0, Gy = 0;

            for (int y = k + 1; y < quantGradient.Height - k - 1; y++)
            {
                for (int x = k + 1; x < quantGradient.Width - k - 1; x++)
                {
                    if (quantGradient.Data[y * quantGradient.Stride + x] != 127)
                    {
                        weightPixels = 0;
                        weights = 0;
                        Gx = 0;
                        Gy = 0;
                        for (int j = -k; j <= k; j++)
                        {
                            for (int i = -k, X = 0; i <= k; i++, X++)
                            {
                                Gx = (quantGradient.Data[(y + i) * quantGradient.Stride + x + j + 1] - quantGradient.Data[(y + i) * quantGradient.Stride + x + j - 1]) / 2f;
                                Gy = (quantGradient.Data[(y + i + 1) * quantGradient.Stride + x + j] - quantGradient.Data[(y + i - 1) * quantGradient.Stride + x + j]) / 2f;
                                double w = Math.Exp(-1 * (Gx * Gx + Gy * Gy) / (2 * factor * factor));
                                weights += w;
                                weightPixels += w * (quantGradient.Data[(y + i) * quantGradient.Stride + x + j]);
                            }
                        }

                        smoothQuantGradient.Data[y * smoothQuantGradient.Stride + x] = Math.Abs(weights) < double.Epsilon ? 0 : (int)Math.Round(weightPixels / weights);
                    }
                }
            }

            for (int y = 0; y < smoothQuantGradient.Height; y++)
                for (int x = 0; x < smoothQuantGradient.Width; x++)
                    smoothQuantGradient.Data[y * smoothQuantGradient.Stride + x] = (int)Math.Round(1.40625 * smoothQuantGradient.Data[y * smoothQuantGradient.Stride + x] - 179);

            return smoothQuantGradient;
        }




        public ZagImage<int> QuantizeFromTables(ZagImage<int> gradient)
        {

            var quantGradient = new ZagImage<int>(gradient.Width, gradient.Height, 1);

            var maxQuantizationLevel = gradientLevels[(int)this.QuantizationLevels].Length;


            for (int y = 0; y < gradient.Height; y++)
            {
                for (int x = 0; x < gradient.Width; x++)
                {
                    if (gradient.Data[y * gradient.Stride + x] != 0)
                    {
                        if (gradient.Data[y * gradient.Stride + x] > gradientLevels[(int)this.QuantizationLevels][maxQuantizationLevel - 1])
                        {
                            quantGradient.Data[y * quantGradient.Stride + x] = maxQuantizationLevel;
                        }
                        else
                        {
                            for (int q = 0; q < maxQuantizationLevel; q++)
                            {
                                if (gradient.Data[y * gradient.Stride + x] < gradientLevels[(int)this.QuantizationLevels][q])
                                {
                                    quantGradient.Data[y * quantGradient.Stride + x] = q + 1;
                                    break;
                                }

                            }
                        }



                    }
                }
            }
            return quantGradient;
        }

        public ZagImage<int>[] getLevels(ZagImage<int> gradient, int qLevels)
        {
            var level = new ZagImage<int>[qLevels];
            for (int i = 0; i < qLevels; i++)
                level[i] = new ZagImage<int>(gradient.Width, gradient.Height, 1);
            for (int y = 0; y < gradient.Height; y++)
            {
                for (int x = 0; x < gradient.Width; x++)
                {
                    if (gradient.Data[y * gradient.Stride + x] != 0)
                        level[gradient.Data[y * gradient.Stride + x] - 1].Data[y * gradient.Stride + x] = 255;
                }
            }
            return level;
        }

        public List<DoLF.DsLPoints> getCenterofGravityPoints(ZagImage<byte> img, int MinCCSize)
        {
            var myblobCounter = new BlobCounter();
            var myblobs = myblobCounter.GetObjectsWithoutArray(img).Where(x => x.Width >= MinCCSize && x.Height >= MinCCSize);


            //if (myblobs.Count() > 1)
            //{
            //    // Calculate Mean Area Size
            //    double average = myblobs.Average(x => x.Area);

            //    //Calculate Average Absolute Deviation
            //    double aad = myblobs.Select(x => Math.Abs(x.Area - average)).Average();

            //    myblobs = myblobs.Where(x => x.Area >= average - aad && x.Area <= average + aad).ToArray();
            //}



            var mypoints = new List<DoLF.DsLPoints>();


            foreach (var b in myblobs)
                mypoints.Add(new DoLF.DsLPoints((int)Math.Round(b.CenterOfGravityX), (int)Math.Round(b.CenterOfGravityY)));


            return mypoints;
        }

        public ZagImage<byte> getLevelImage(ZagImage<int> levelGradient)
        {
            var img = new ZagImage<byte>(levelGradient.Width, levelGradient.Height, 1);
            for (int y = 0; y < levelGradient.Height; y++)
            {
                for (int x = 0; x < levelGradient.Width; x++)
                {
                    img.Data[y * img.Stride + x] = levelGradient.Data[y * levelGradient.Stride + x] > 0 ? (byte)255 : (byte)0;
                }
            }
            return img;
        }

        public int CalcWindowSizeAverage(DoLF.DsLPoints lp, ZagImage<byte> img, int WindowSize, int Range, int GradientN)
        {

            int WindowR = WindowSize / GradientN;
            var pyramindWindowR = Enumerable.Range(WindowR - Range, 2 * Range).
                Select(x => x * GradientN);

            var averageGradientList = new List<Tuple<int, float>>();
            var gradientList = new List<float>();
            //get the window 
            foreach (var currentWindow in pyramindWindowR)
            {

                gradientList.Clear();
                for (int y = (int)lp.Y - currentWindow / 2; y <= lp.Y + currentWindow / 2; y++)
                {
                    if (y < 0 || y >= img.Height)
                        continue;
                    for (int x = (int)lp.X - currentWindow / 2; x <= lp.X + currentWindow / 2; x++)
                    {
                        if (x < 0 || x >= img.Width)
                            continue;
                        gradientList.Add(img.Data[y * img.Stride + x]);

                    }
                }
                averageGradientList.Add(new Tuple<int, float>(currentWindow, gradientList.Average()));
            }
            var localvalleys = LocalMaxima(averageGradientList.Select(x => x.Item2), 3);

            int size = localvalleys.Count() > 0
                ? localvalleys.Select(x => averageGradientList[x.Item1]).Min(x => x.Item1)
                : averageGradientList[0].Item1;


            return size;

        }

        public IEnumerable<Tuple<int, float>> LocalMaxima(IEnumerable<float> source, int windowSize)
        {
            // Round up to nearest odd value
            windowSize = windowSize - windowSize % 2 + 1;
            int halfWindow = windowSize / 2;

            int index = 0;
            var before = new Queue<float>(Enumerable.Repeat(float.NegativeInfinity, halfWindow));
            var after = new Queue<float>(source.Take(halfWindow + 1));

            foreach (float d in source.Skip(halfWindow + 1).Concat(Enumerable.Repeat(float.NegativeInfinity, halfWindow + 1)))
            {
                float curVal = after.Dequeue();
                if (before.All(x => curVal > x) && after.All(x => curVal >= x))
                {
                    yield return Tuple.Create(index, curVal);
                }

                before.Dequeue();
                before.Enqueue(curVal);
                after.Enqueue(d);
                index++;
            }
        }



        public float[] GradientDescriptorNxN(DoLF.DsLPoints lp, ZagImage<int> gradient, ZagImage<float> magnitude, int windowR = 4, int N = 3)
        {

            var descriptor = new float[N * N * numberGradientLevels];

            int cubeNumber = 0;
            for (int y = (int)lp.Y - N * windowR / 2, Y = 0; y < (lp.Y + N * windowR / 2); y += windowR, Y++)
            {
                for (int x = (int)lp.X - N * windowR / 2, X = 0; x < (lp.X + N * windowR / 2); x += windowR, X++)
                {
                    for (int yy = 0; yy < windowR; yy++)
                    {
                        for (int xx = 0; xx < windowR; xx++)
                        {
                            if (y + yy < 0 || x + xx < 0 || yy + y >= gradient.Height || xx + x >= gradient.Width)
                                continue;
                            int cAngle = getAngle(gradient.Data[(y + yy) * gradient.Stride + (x + xx)]);
                            //angles[Y * windowR + yy, X * windowR + xx] = cAngle;
                            descriptor[cubeNumber * numberGradientLevels + cAngle] += magnitude.Data[(y + yy) * gradient.Stride + (x + xx)];
                        }
                    }
                    cubeNumber++;
                }
            }

            return descriptor;
        }

        private int getAngle(float angle)
        {
            int pos = -1;
            var qLevels = (int)QuantizationLevels;

            for (int i = 0; i < gradientLevels[qLevels].Length; i++)
            {
                if (angle <= gradientLevels[qLevels][i])
                {
                    pos = i;
                    break;
                }
            }
            if (pos == -1)
                pos = qLevels - 1;
            return pos;
        }


        public void NormalizeDescriptorAndThresholding(ref float[] descriptor, float thr = 0.6f)
        {
            //normalize with norm
            var norm = (float)Math.Sqrt(descriptor.Select(k => k * k).Sum());
            if (Math.Abs(norm) > float.Epsilon)
                for (int k = 0; k < descriptor.Length; k++)
                    descriptor[k] /= norm;
            if (Math.Abs(thr - -1) < float.Epsilon)
                return;
            // Remove ilumination
            for (int k = 0; k < descriptor.Length; k++)
                if (descriptor[k] > thr)
                    descriptor[k] = thr;
            // renormalize
            norm = (float)Math.Sqrt(descriptor.Select(k => k * k).Sum());
            if (Math.Abs(norm) > float.Epsilon)
                for (int k = 0; k < descriptor.Length; k++)
                    descriptor[k] /= norm;
        }








    }
}