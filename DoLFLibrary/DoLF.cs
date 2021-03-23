using System;
using System.Collections.Generic;
using System.Linq;

namespace DoLFLibrary
{
    public class DoLF
    {

        public static byte[] TestImageData;
        public static int[] TestMatrixData;
        public static int TestImageWidth;
        public static int TestImageHeight;
        public static int Depth;

        /// <summary>
        /// The Document Specific Local Points
        /// </summary>
        public class DsLPoints
        {
            /// <summary>
            /// Window Size
            /// </summary>
            public int WindowSize { get; set; }

            /// <summary>
            /// Descriptor
            /// </summary>
            public float[] Descriptor { get; set; }

            public float X { get; set; }

            public float Y { get; set; }

            /// <summary>
            /// Quantization Gradient 
            /// </summary>
            public int Gradient { get; set; }

            public DsLPoints(float X, float Y)
            {
                this.X = X;
                this.Y = Y;
            }


        }

        public class Result
        {
            public int[] Block { get; set; }




            public int Position { get; set; }

            public float Similarity { get; set; }

            public Result Clone()
            {
                var newBlock = new int[Block.Length];
                Block.CopyTo(newBlock, 0);
                return new Result
                {
                    Position = Position,
                    Similarity = Similarity,
                    Block = newBlock
                };
            }
        }



        #region Settings

        public int WindowSize { get; set; }

        public int N { get; set; }

        public QuantizationLevelsNum QuantizationLevels { get; set; }

        /// <summary>
        /// Enable Dynamic Window
        /// </summary>
        public bool DynamicWindow { get; set; }

        public int MinCCSize { get; set; }

        #endregion

        public enum QuantizationLevelsNum
        {
            Two = 2,
            Three,
            Four
        }


        /// <summary>
        /// Get the DSLP Local Points
        /// </summary>
        /// <param name="Image">an 24bit RGB Image</param>
        /// <param name="Width"></param>
        /// <param name="Height"></param>
        /// <returns></returns>
        public DsLPoints[] GetDSLPoints(byte[] Image, int Width, int Height, int ImageDepth, int WindowSize = 68, int N = 4, QuantizationLevelsNum QuantizationLevels = QuantizationLevelsNum.Four, int MinCCSize = 4, bool DynamicWindow = true)
        {

            this.WindowSize = WindowSize;
            this.N = N;
            this.QuantizationLevels = QuantizationLevels;
            this.MinCCSize = MinCCSize;
            this.DynamicWindow = DynamicWindow;
            var NewPixels = new byte[3 * Width * Height];
            int cstride = 3 * Width;
            int stride = ImageDepth * Width;
            switch (ImageDepth)
            {
                case 1:
                    for (int y = 0; y < Height; y++)
                        for (int x = 0; x < Width; x++)
                        {
                            NewPixels[y * cstride + 3 * x]
                                = NewPixels[y * cstride + 3 * x + 1]
                                = NewPixels[y * cstride + 3 * x + 2]
                                = Image[y * stride + ImageDepth * x];
                        }
                    break;
                case 4:
                    for (int y = 0; y < Height; y++)
                        for (int x = 0; x < Width; x++)
                        {
                            NewPixels[y * cstride + 3 * x] = Image[y * stride + ImageDepth * x];
                            NewPixels[y * cstride + 3 * x + 1] = Image[y * stride + ImageDepth * x + 1];
                            NewPixels[y * cstride + 3 * x + 2] = Image[y * stride + ImageDepth * x + 2];
                        }
                    break;
                case 3:
                    NewPixels = Image;
                    break;
                default:
                    throw new Exception("Not Supported Depth");

            }
            var myImage = new ZagImage<byte>(NewPixels, Width, Height);
            var myDSLP = new DsLPsDetectorv2wFeatures
            {
                N = this.N,
                WindowSize = this.WindowSize,
                QuantizationLevels = this.QuantizationLevels,
                MinCCSize = this.MinCCSize,
                DynamicWindow = this.DynamicWindow
            };
            myDSLP.CalcDoLFs(myImage);
            return myDSLP.myDsLPs.Count() > 0 ? myDSLP.myDsLPs.ToArray() : new DsLPoints[0];
        }



        #region Segmentation - based Distance Functions

        public float[] CreateNormalizedDescriptorForSegmBased(DsLPoints[] LocalPoints, int Width, int Height)
        {
            DistanceSegmBased mySegmDistance = new DistanceSegmBased();
            return mySegmDistance.GetNormalizedDescriptor(LocalPoints, Width, Height);
        }

        public float DistanceSegmBased(DsLPoints[] QueryLocalPoints, int QueryWidth, int QueryHeight, DsLPoints[] WordLocalPoints, int WordWidth, int WordHeight, float NearNeighborArea = 0.35f)
        {
            DistanceSegmBased mySegmDistance = new DistanceSegmBased
            {
                NearNeighborArea = NearNeighborArea
            };
            var vector1 = mySegmDistance.GetNormalizedDescriptor(QueryLocalPoints, QueryWidth, QueryHeight);
            var vector2 = mySegmDistance.GetNormalizedDescriptor(WordLocalPoints, WordWidth, WordHeight);
            return DistanceSegmBased(vector1, vector2, mySegmDistance);

        }

        public float DistanceSegmBased(float[] QueryNormalizedDescriptor, float[] WordNormalizedDescriptor, float NearNeighborArea = 0.35f)
        {

            DistanceSegmBased mySegmDistance = new DistanceSegmBased
            {
                NearNeighborArea = NearNeighborArea
            };
            return DistanceSegmBased(QueryNormalizedDescriptor, WordNormalizedDescriptor, mySegmDistance);

        }

        private float DistanceSegmBased(float[] Vector1NormalizedDescriptor, float[] Vector2NormalizedDescriptor, DistanceSegmBased SegmDistanceObject)
        {
            return SegmDistanceObject.GetSimilarity(Vector1NormalizedDescriptor, Vector2NormalizedDescriptor);

        }

        #endregion

        #region Segmentation - free Distance Functions

        public float[] CreateNormalizedDescriptorForSegmFree(DsLPoints[] LocalPoints)
        {
            var mySegmFreeDistance = new DistanceSegmFree();
            return mySegmFreeDistance.GetNormalizedDescriptor(LocalPoints);
        }

        public List<Result> DistanceSegmFree(DsLPoints[] QueryLocalPoints, int QueryWidth, int QueryHeight, DsLPoints[] DocumentLocalPoints, float NearNeighborArea = 0.35f, int ClosestCenters = 2, float SimilarityCenterLocalPoints = 0.04f)
        {
            var mySegmFreeDistance = new DistanceSegmFree
            {
                NearNeighborArea = NearNeighborArea,
                ClosestCenters = ClosestCenters,
                SimilarityCenterLocalPoints = SimilarityCenterLocalPoints

            };
            var query = mySegmFreeDistance.GetNormalizedDescriptor(QueryLocalPoints);
            var doc = mySegmFreeDistance.GetNormalizedDescriptor(DocumentLocalPoints);
            return DistanceSegmFree(query, QueryWidth, QueryHeight, doc, mySegmFreeDistance);

        }

        public List<Result> DistanceSegmFree(float[] QueryNormalizedDescirptor, int QueryWidth, int QueryHeight, float[] DocumentNormalizedDescriptor, float NearNeighborArea = 0.35f, int ClosestCenters = 2, float SimilarityCenterLocalPoints = 0.04f)
        {
            var mySegmFreeDistance = new DistanceSegmFree
            {
                NearNeighborArea = NearNeighborArea,
                ClosestCenters = ClosestCenters,
                SimilarityCenterLocalPoints = SimilarityCenterLocalPoints

            };
            return DistanceSegmFree(QueryNormalizedDescirptor, QueryWidth, QueryHeight, DocumentNormalizedDescriptor, mySegmFreeDistance);

        }

        private List<Result> DistanceSegmFree(float[] QueryNormalizedDescriptor, int QueryWidth, int QueryHeight, float[] DocumentNormalizedDescriptor, DistanceSegmFree DistanceObject)
        {
            return DistanceObject.GetSimilarity(QueryNormalizedDescriptor, QueryWidth, QueryHeight, DocumentNormalizedDescriptor);

        }

        #endregion






    }
}
