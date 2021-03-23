using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CLHelpFunctions;
using CommandLine;
using Newtonsoft.Json;

namespace WordSpottingByExample
{
    class Program
    {



        static void Main(string[] args)
        {
            var parsed = Parser.Default.ParseArguments<IndexingOptions, RetrievalOptions, DescriptorOptions, SearchOptions>(args).MapResult((IndexingOptions opts) =>
                {
                    string[] Documents = Directory.GetFiles(Path.GetFullPath(opts.ImagesDirectory), "*." + opts.ImageFormat);
                    if (Documents.Length == 0) Error("We do not found any images. Please check if you defined the image format correctly");

                    var myProgressBar = new ProgressBar();
                    Console.WriteLine($"Found and Indexing {Documents.Length} Documents...");
                    var Dataset = new ConcurrentDictionary<string, Retrieval.DocumentInfo>();
                    var myWatch = new Stopwatch();
                    myWatch.Start();
                    Dataset = opts.Segm == Options.SegmentationType.SegmBased ?
                            Retrieval.IndexingSegmBased(Documents, myProgressBar) :
                            Retrieval.IndexingSegmFree(Documents, myProgressBar);
                    myWatch.Stop();
                    double duration = myWatch.Elapsed.TotalSeconds / Documents.Length;
                    Console.WriteLine("Done!");
                    Console.WriteLine($"Average Time per Document: {duration.ToString("0.00")} sec");
                    Console.WriteLine("Writing Database file...");
                    using (var file = File.CreateText(Path.GetFullPath(opts.OutputDatabaseFile)))
                    {
                        var serializer = new JsonSerializer();
                        serializer.Serialize(file, Dataset);
                    }
                    return true;

                }, (RetrievalOptions opts) =>
                 {
                     if (!File.Exists(opts.DatabaseFile)) Error("The Database File does not exists!");
                     string[] queries = Directory.Exists(opts.Query) ? Directory.GetFiles(opts.Query, $"*.{opts.ImageFormat.ToString()}") : new[] { opts.Query };
                     double duration = 0;
                     var hKws14XMLFormat = new VCGXMLFormat.RelevanceListings
                     {
                         gtrels = new List<VCGXMLFormat.RelevanceListings.rel>()
                     };
                     var concurrentGtRels = new ConcurrentBag<VCGXMLFormat.RelevanceListings.rel>();
                     Console.WriteLine("Loading Database to memory...");
                     ConcurrentDictionary<string, Retrieval.DocumentInfo> Dataset = null;
                     using (var file = File.OpenText(opts.DatabaseFile))
                     {
                         var serializer = new JsonSerializer();
                         Dataset = (ConcurrentDictionary<string, Retrieval.DocumentInfo>)serializer.Deserialize(file, typeof(ConcurrentDictionary<string, Retrieval.DocumentInfo>));
                     }
                     Console.WriteLine($"Found {queries.Length} queries. Running...");
                     Stopwatch totalDuration = new Stopwatch();
                     totalDuration.Start();
                     var myProgressBar = new ProgressBar();
                     //foreach (var q in queries)
                     Parallel.ForEach(queries, q =>
                       {

                           var queryimg = new NewImage(q);
                           var mywatch = new Stopwatch();
                           mywatch.Start();
                           var results = opts.Segm == Options.SegmentationType.SegmBased ?
                           Retrieval.SearchSegmBased(queryimg, Dataset) :
                           Retrieval.SearchSegmFree(queryimg, Dataset);
                           mywatch.Stop();
                           ProgressBar.Add(ref duration, mywatch.Elapsed.TotalSeconds);

                           //add the results to the final xml file
                           concurrentGtRels.Add(new VCGXMLFormat.RelevanceListings.rel
                           {
                               queryid = Path.GetFileNameWithoutExtension(q),
                               words = results.Select(r => new VCGXMLFormat.QueryRelevanceJudgements.Rels
                               {
                                   documentName = r.Document,
                                   x = r.X,
                                   y = r.Y,
                                   width = r.Width,
                                   height = r.Height,
                                   similarity = r.Similarity
                               }).ToList()
                           });
                           myProgressBar.Increase(100d / queries.Length);
                       });
                     totalDuration.Stop();
                     hKws14XMLFormat.gtrels = concurrentGtRels.ToList();
                     //write the xml file and output the query time
                     Console.WriteLine("Done!");
                     Console.WriteLine($"Average Time per Query: {(duration / queries.Length).ToString("0.00")} sec");
                     Console.WriteLine($"Total Time: {totalDuration.Elapsed.TotalSeconds.ToString("0.00")}");
                     Console.WriteLine("Writing the output xml file...");

                     try
                     {
                         VCGXMLFormat.serializeToXML(hKws14XMLFormat, opts.OutXMLResults);
                     }
                     catch
                     {
                         Console.WriteLine("ERROR saving the xml file to the defined output name. I will try to save the result to the file: default.xml at the current directory.");
                         VCGXMLFormat.serializeToXML(hKws14XMLFormat, "default.xml");
                     }


                     return true;
                 }, (DescriptorOptions Options) =>
                {
                    string imagebase64 = Options.ImageBase64 ?? Console.ReadLine();
                    if (string.IsNullOrEmpty(imagebase64))
                        Error("Need to supply image base64 encoding through argument or standard input stream");
                    var arrayString = Retrieval.GetDescriptor(imagebase64)
                        .Select(x => Convert.ToString(x, CultureInfo.InvariantCulture));
                    Console.WriteLine(
                        string.Join("@", arrayString)
                        );
                    return true;
                },
                (SearchOptions Options) =>
                {
                    float[] documentDescriptors = Options.DocumentDescriptorFloatVector
                                                ?? Options.GetFloatVectors(Console.ReadLine());
                    if (documentDescriptors == null)
                        Error("Need to supply document descriptors through argument or standard input stream");
                    if (Options.QueryDescriptorFloatVector == null)
                        Error("Need to supply query descriptors through argument");

                    Console.WriteLine(
                        string.Join("@",
                        Retrieval.Search(
                            Options.QueryDescriptorFloatVector,
                            Options.QueryWidth,
                            Options.QueryHeight,
                            documentDescriptors
                        )
                        )
                        );
                    return true;
                },
                err => false);
            //Console.ReadKey();
            Console.WriteLine();
        }



        static void Error(string Message)
        {
            Console.WriteLine("ERROR!");
            Console.WriteLine(Message);
            Environment.Exit(0);
        }






    }
}
