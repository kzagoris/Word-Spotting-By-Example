using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace WordSpottingByExample
{
    public static class VCGXMLFormat
    {



        public static T DeserializeFromXML<T>(string xmlFilename) where T : class
        {
            T result = null;
            XmlSerializer xmlser = new XmlSerializer(typeof(T));
            using (TextReader tr = new StreamReader(xmlFilename, Encoding.UTF8))
                result = xmlser.Deserialize(tr) as T;
            return result;
        }

        public static void serializeToXML<T>(T PAGEFormatInfo, string xmlFilename)
        {
            XmlSerializer xmlser = new XmlSerializer(typeof(T));
            using (TextWriter tw = new StreamWriter(xmlFilename, false, Encoding.UTF8))
                xmlser.Serialize(tw, PAGEFormatInfo);
        }

        [Serializable, XmlRoot(ElementName = "RelevanceListings")]
        public class RelevanceListings
        {
            [XmlElement("Rel")]
            public List<rel> gtrels { get; set; }

            [Serializable]
            public class rel
            {
                [XmlAttribute("queryid")]
                public string queryid { get; set; }
                [XmlElement("word", typeof(QueryRelevanceJudgements.Rels))]
                public List<QueryRelevanceJudgements.Rels> words { get; set; }
            }
        }



        [Serializable, XmlRoot(ElementName = "GroundTruthRelevanceJudgements")]
        public class QueryRelevanceJudgements
        {
            [XmlElement("GTRel")]
            public List<gtrel> gtrels { get; set; }

            [Serializable]
            public class gtrel
            {
                [XmlAttribute("queryid")]
                public string queryid { get; set; }
                [XmlElement("word", typeof(GTRels))]
                public List<GTRels> words { get; set; }
            }

            [Serializable]
            public class GTRels : Rels
            {

                [XmlAttribute("Relevance")]
                public float Relevance { get; set; }

                [XmlAttribute("Text")]
                public string Text;

                public GTRels(string docKey, string document, string Text, int x, int y, int width, int height, float relevance = 1)
                    : base(docKey, document, x, y, width, height)
                {
                    this.Text = Text;
                    this.Relevance = relevance;
                }
                public GTRels() { }


            }

            [Serializable]
            public class Rels
            {
                //[XmlAttribute("id")]
                [XmlIgnore]
                public string docKey { get; set; }

                [XmlAttribute("document")]
                public string documentName { get; set; }

                [XmlAttribute("x")]
                public int x { get; set; }
                [XmlAttribute("y")]
                public int y { get; set; }
                [XmlAttribute("width")]
                public int width { get; set; }
                [XmlAttribute("height")]
                public int height { get; set; }
                [XmlAttribute("similarity")]
                public float similarity { get; set; }

                public Rels(string docKey, string document, int x, int y, int width, int height)
                {
                    this.docKey = docKey; this.documentName = document;
                    this.x = x; this.y = y; this.height = height; this.width = width;
                }

                public Rels() { }


            }

        }

     



    }
}
