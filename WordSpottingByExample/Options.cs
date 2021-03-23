using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommandLine;
using CommandLine.Text;
using MoreLinq;




namespace WordSpottingByExample
{
    internal class Options
    {
        public enum SegmentationType
        {
            SegmFree, SegmBased
        }

        public enum IndexingType
        {
            Hash, RandomTrees
        }

        public enum ImageFormatOptions
        {
            png,
            tif,
            jpeg,
            jpg

        }
        [Option('i', "imageformat", HelpText = "Image Extension", Required = false, Default = ImageFormatOptions.png)]
        public ImageFormatOptions ImageFormat { get; set; }


        [Option(HelpText = "Segmentation scenario: SegmFree or SegmBased", Required = false, Default = SegmentationType.SegmFree)]
        public SegmentationType Segm { get; set; }



    }


    [Verb("retrieval", HelpText = "Retrieve Word")]
    internal class RetrievalOptions : Options
    {

        [Value(0, HelpText = "The query word image path. It must be a directory or a file", Required = true, MetaName = "QueryImagePath")]
        public string Query { get; set; }

        [Value(1, HelpText = "The database file", Required = true, MetaName = "Dataset")]
        public string DatabaseFile { get; set; }

        [Value(2, HelpText = "The XML Retrieval Results File. It follows the H-KWS2014 XML Format. Download Evaluation Tool from https://vc.ee.duth.gr/H-KWS2014/#VCGEval ", Required = true, MetaName = "Results")]
        public string OutXMLResults { get; set; }

        [Usage(ApplicationAlias = "WordSpottingByExample")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Locate a word", new RetrievalOptions { Query = ".\\query-word.png", DatabaseFile = ".\\dataset.json", OutXMLResults = "Results.xml" });
                yield return new Example("Word Spotting under a segmentation-based scenario", new RetrievalOptions { Segm = SegmentationType.SegmBased, Query = ".\\query-word.png", DatabaseFile = ".\\dataset.json" });
            }
        }
    }

    [Verb("indexing", HelpText = "Indexing Directory")]
    internal class IndexingOptions : Options
    {


        [Value(0, HelpText = "The directory path that contains the document images for indexing", Required = true, MetaName = "ImagesDirectory")]
        public string ImagesDirectory { get; set; }

        [Value(1, HelpText = "The output Database file that contains the dataset info", Required = true, MetaName = "Output Dataset File")]
        public string OutputDatabaseFile { get; set; }


        [Usage(ApplicationAlias = "WordSpottingByExample")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Indexing example", new IndexingOptions { ImageFormat = ImageFormatOptions.jpg, ImagesDirectory = "C:\\WordImages", OutputDatabaseFile = ".\\dataset.json" });
                yield return new Example("Indexing under a segmentation-based scenario", new IndexingOptions { Segm = SegmentationType.SegmBased, ImagesDirectory = "C:\\DocumentImages", OutputDatabaseFile = ".\\dataset.json" });
            }
        }



    }

    [Verb("descriptor", HelpText = "Calculate Descriptor")]
    internal class DescriptorOptions
    {
        [Value(0, HelpText = "Provide the Image in base64 encoding to calculate the descriptor", Required = false, Default = null, MetaName = "Image")]
        public string ImageBase64 { get; set; }

        [Usage(ApplicationAlias = "WordSpottingByExample")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Calculate descriptor example", new DescriptorOptions { ImageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAIcAAAAnCAIAAACqmcyzAAAgAElEQVRoBW3B55Nd930m+Of5/s45N3XuRmrknEGABANAUqIlW6LTTHlqX7hctf/Rvt95MbvjWle5diyJVtlTkq1EAmBAIhEJIhGx0Y3Oue+955zf99nbl8Isp3Y/H14+/zMAkgAIJolwkugSTF1mxi5JAEiqy8zQJV9nZkmSuNbhFUlmBkASAJLokAAHDIAAkpKCmUt4hQBISQCIP4oxupRlGQBJ7k6KJGAkJQGQBIBdktjlLgAkAbg7ScmdZDC4vKssy5nZ" });
            }
        }
    }

    [Verb("search", HelpText = "Find similar Words in Document")]
    internal class SearchOptions
    {
        [Option('w', "queryWidth", HelpText = "The query image width", Required = true)]
        public int QueryWidth { get; set; }
        [Option('h', "queryHeight", HelpText = "The query image width", Required = true)]
        public int QueryHeight { get; set; }
        [Option('l', "length", HelpText = "The descriptor length", Required = true)]
        public int Length { get; set; }
        [Value(0, HelpText = "Query descriptors using format x@y@descriptor1@...@ ", Required = true)]
        public string QueryDescriptors { get; set; }
        [Value(1, HelpText = "Document descriptors using format x@y@descriptor1@...@ ", Required = false, Default = null)]
        public string DocumentDescriptors { get; set; }

        public float[] QueryDescriptorFloatVector => GetFloatVectors(QueryDescriptors);

        public float[] DocumentDescriptorFloatVector => GetFloatVectors(DocumentDescriptors);

        public float[] GetFloatVectors(string descriptors)
        {
            if (string.IsNullOrEmpty(descriptors)) return null;
            var descriptor = new List<float>();
            var batchDescriptor = descriptors.Split("@")
                .Select(x => Convert.ToSingle(x, CultureInfo.InvariantCulture))
                .Batch(Length + 2);

            foreach (IEnumerable<float> d in batchDescriptor)
            {
                using var dEnumerator = d.GetEnumerator();
                dEnumerator.MoveNext();
                descriptor.Add(dEnumerator.Current);
                if (!dEnumerator.MoveNext()) continue;
                descriptor.Add(dEnumerator.Current);
                descriptor.Add(0);
                descriptor.Add(0);
                while (dEnumerator.MoveNext())
                {
                    descriptor.Add(dEnumerator.Current);
                }
            }
            descriptor.Add(Length + 4);
            return descriptor.ToArray();
        }

        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Calculate descriptor example", new SearchOptions
                {
                    QueryWidth = 20,
                    QueryHeight = 30,
                    Length = 4,
                    QueryDescriptors = "3@4@0.334@0.3456@",
                    DocumentDescriptors = "3@4@0.334@0.3456@16@4@0.0034@0.003456"
                });
            }
        }




    }
}
