using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CLHelpFunctions;
using DoLFLibrary;

namespace WordSpottingByExample
{
    class Retrieval
    {





        public static ConcurrentDictionary<string, DocumentInfo> IndexingSegmFree(string[] Documents, ProgressBar myProgressBar)
        {
            var Dataset = new ConcurrentDictionary<string, DocumentInfo>();
            Parallel.ForEach(Documents, doc =>
            {
                var img = new NewImage(doc);
                var myDSLPLibrary = new DoLF();
                var myDSLPpoints = myDSLPLibrary.GetDSLPoints(img.Data, img.Width, img.Height, 3);
                var myDocInfo = new DocumentInfo
                {
                    Descriptors = myDSLPLibrary.CreateNormalizedDescriptorForSegmFree(myDSLPpoints),
                    Width = img.Width,
                    Height = img.Height
                };
                Dataset.TryAdd(Path.GetFileNameWithoutExtension(doc), myDocInfo);
                myProgressBar.Increase(100d / Documents.Length);
            });

            return Dataset;
        }

        public static float[] GetDescriptor(string ImageBase64)
        {

            using var memoryStream = new MemoryStream(Convert.FromBase64String(ImageBase64)) { Position = 0 };
            Bitmap bmpImage = (Bitmap)Image.FromStream(memoryStream);
            var img = new NewImage(bmpImage);
            var myDSLPLibrary = new DoLF();
            var myDSLPpoints = myDSLPLibrary.GetDSLPoints(img.Data, img.Width, img.Height, 3);
            return myDSLPLibrary.CreateNormalizedDescriptorForSegmFree(myDSLPpoints);
        }

        public static ConcurrentDictionary<string, DocumentInfo> IndexingSegmBased(string[] Documents, ProgressBar myProgressBar)
        {
            var Dataset = new ConcurrentDictionary<string, DocumentInfo>();
            Parallel.ForEach(Documents, doc =>
            {
                var img = new NewImage(doc);
                var myDSLPLibrary = new DoLF();
                var myDSLPpoints = myDSLPLibrary.GetDSLPoints(img.Data, img.Width, img.Height, 3);
                var myDocInfo = new DocumentInfo
                {
                    Descriptors = myDSLPLibrary.CreateNormalizedDescriptorForSegmBased(myDSLPpoints, img.Width, img.Height),
                    Width = img.Width,
                    Height = img.Height
                };
                Dataset.TryAdd(Path.GetFileNameWithoutExtension(doc), myDocInfo);
                myProgressBar.Increase(100d / Documents.Length);
            });
            return Dataset;
        }



        public static IEnumerable<int> Search(float[] QueryVector, int QueryWidth, int QueryHeight, float[] DocumentVector)
        {
            var myDSLPLibrary = new DoLF();
            return myDSLPLibrary.DistanceSegmFree(QueryVector, QueryWidth, QueryHeight, DocumentVector)
                .OrderBy(d => d.Similarity)
                .SelectMany(d => new int[] {
                    d.Block[0],
                    d.Block[1],
                    d.Block[2],
                    d.Block[3],
                    (int)Math.Round(10000*d.Similarity)
                });
        }


        public static Result[] SearchSegmFree(NewImage QueryImage, ConcurrentDictionary<string, DocumentInfo> Dataset)
        {
            var myDSLPLibrary = new DoLF();
            var myDSLPpoints = myDSLPLibrary.GetDSLPoints(QueryImage.Data, QueryImage.Width, QueryImage.Height, 3);
            var queryVector = myDSLPLibrary.CreateNormalizedDescriptorForSegmFree(myDSLPpoints);
            var results = new ConcurrentBag<Result>();
            //Parallel.ForEach(Dataset.Keys, doc =>
            foreach (var doc in Dataset.Keys)
            {
                var _results = myDSLPLibrary.DistanceSegmFree(queryVector, QueryImage.Width, QueryImage.Height, Dataset[doc].Descriptors);
                foreach (var r in _results)
                    results.Add(new Result
                    {
                        X = (short)r.Block[0],
                        Y = (short)r.Block[1],
                        Width = (short)r.Block[2],
                        Height = (short)r.Block[3],
                        Similarity = r.Similarity,
                        Document = doc
                    });
            }
            return results.OrderBy(x => x.Similarity).ToArray();
        }

        public static Result[] SearchSegmBased(NewImage QueryImage, ConcurrentDictionary<string, DocumentInfo> Dataset)
        {
            var myDSLPLibrary = new DoLF();
            var myDSLPpoints = myDSLPLibrary.GetDSLPoints(QueryImage.Data, QueryImage.Width, QueryImage.Height, 3);
            var queryVector = myDSLPLibrary.CreateNormalizedDescriptorForSegmBased(myDSLPpoints, QueryImage.Width, QueryImage.Height);
            var results = new ConcurrentBag<Result>();
            Parallel.ForEach(Dataset.Keys, doc =>
            {
                var r = myDSLPLibrary.DistanceSegmBased(queryVector, Dataset[doc].Descriptors);
                results.Add(new Result
                {
                    X = 0,
                    Y = 0,
                    Width = Dataset[doc].Width,
                    Height = Dataset[doc].Height,
                    Similarity = r,
                    Document = doc
                });
            });
            return results.OrderBy(x => x.Similarity).ToArray();

        }





        public class DocumentInfo
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public float[] Descriptors { get; set; }
        }


        public class Result
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public float Similarity { get; set; }

            public int DocumentId { get; set; }
            public string Document { get; set; }

            public int Area => Width * Height;

            public Result Intersect(Result ResultB)
            {
                var x = Math.Max(this.X, ResultB.X);
                var num1 = Math.Min(this.X + this.Width, ResultB.X + ResultB.Width);
                var y = Math.Max(this.Y, ResultB.Y);
                var num2 = Math.Min(this.Y + this.Height, ResultB.Y + ResultB.Height);

                return (num1 >= x && num2 >= y)
                    ? new Result { X = x, Y = y, Width = num1 - x, Height = num2 - y, DocumentId = this.DocumentId, Similarity = this.Similarity }
                    : null;
            }

            public Result Union(Result ResultB)
            {
                var x1 = Math.Min(this.X, ResultB.X);
                var x2 = Math.Max(this.X + this.Width, ResultB.X + ResultB.Width);
                var y1 = Math.Min(this.Y, ResultB.Y);
                var y2 = Math.Max(this.Y + this.Height, ResultB.Y + ResultB.Height);
                return new Result { X = x1, Y = y1, Width = x2 - x1, Height = y2 - y1, DocumentId = this.DocumentId, Similarity = this.Similarity };
            }
        }



    }
}
