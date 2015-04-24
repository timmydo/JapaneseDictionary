using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.IO.Compression;

namespace BuildDict {
    class Program {

        public class KanjiElement {
            public string reading;
            public List<string> freq;
            public KanjiElement() {
                this.freq = new List<string>();
            }
        }

        public class ReadingElement {
            public string reading;
            public List<string> freq;

            public ReadingElement() {
                this.freq = new List<string>();
            }
        }

        public class SenseElement {
            public List<string> glosses;
            public List<string> partsOfSpeech;
            public SenseElement() {
                this.glosses = new List<string>();
                this.partsOfSpeech = new List<string>();
            }
        }

        public class IndexEntry {
            public HashSet<int> xref;
            public IndexEntry() {
                this.xref = new HashSet<int>();
            }
            static public void Add(Dictionary<string, IndexEntry> dict, string term, int newXref) {
                IndexEntry ie;
                if (dict.TryGetValue(term, out ie)) {
                    ie.xref.Add(newXref);
                } else {
                    ie = new IndexEntry();
                    dict.Add(term, ie);
                }

                ie.xref.Add(newXref);
            }
        }

        public class Entry : IComparable {
            public int index;
            public List<ReadingElement> readings;
            public List<KanjiElement> kanji;
            public List<SenseElement> senses;
            public Entry() {
                this.readings = new List<ReadingElement>();
                this.kanji = new List<KanjiElement>();
                this.senses = new List<SenseElement>();
            }

            public int CompareTo(object obj) {
                Entry other = obj as Entry;
                if (obj == null) {
                    return -1;
                }
                return this.index - other.index;
            }
        }

        //<!ELEMENT entry (ent_seq, k_ele*, r_ele+, info?, sense+)>
        //<!ELEMENT k_ele (keb, ke_inf*, ke_pri*)>
        //<!ELEMENT r_ele (reb, re_nokanji?, re_restr*, re_inf*, re_pri*)>
        //<!ELEMENT sense (stagk*, stagr*, pos*, xref*, ant*, field*, misc*, s_inf*, lsource*, dial*, gloss*, example*)>

        static List<Entry> ReadEntries(string filename) {
            List<Entry> entries = new List<Entry>(1000 * 1000);

            using (var stream = File.OpenRead(filename)) {
                var gz = new GZipStream(stream, CompressionMode.Decompress);
                var settings = new XmlReaderSettings();
                settings.DtdProcessing = DtdProcessing.Parse;
                var r = XmlReader.Create(gz, settings);
                for (int i = 0; i < 2000* 1000; i++) {
                    r.ReadToFollowing("entry");
                    var entry = new Entry();
                    bool insideElement = true;
                    if (r.ReadToFollowing("ent_seq")) {
                        entry.index = r.ReadElementContentAsInt();
                    }

                    KanjiElement kele = new KanjiElement();
                    ReadingElement rele = new ReadingElement();
                    SenseElement sense = new SenseElement();

                    while (insideElement && r.Read()) {
                        switch (r.NodeType) {
                            case XmlNodeType.Element:
                                switch (r.Name) {
                                    case "k_ele":
                                        kele = new KanjiElement();
                                        if (r.ReadToDescendant("keb")) {
                                            kele.reading = r.ReadElementContentAsString();
                                        }
                                        break;
                                    case "ke_pri":
                                        kele.freq.Add(r.ReadElementContentAsString());
                                        break;
                                    case "r_ele":
                                        rele = new ReadingElement();
                                        if (r.ReadToDescendant("reb")) {
                                            rele.reading = r.ReadElementContentAsString();
                                        }
                                        break;
                                    case "re_pri":
                                        rele.freq.Add(r.ReadElementContentAsString());
                                        break;
                                    case "sense":
                                        sense = new SenseElement();
                                        break;
                                    case "gloss":
                                        sense.glosses.Add(r.ReadElementContentAsString());
                                        break;
                                    case "pos":
                                        sense.partsOfSpeech.Add(r.ReadElementContentAsString());
                                        break;
                                    default:
                                        //Console.WriteLine("Start {0}", r.Name);
                                        break;
                                }
                                break;
                            case XmlNodeType.EndElement:
                                switch (r.Name) {
                                    case "k_ele":
                                        entry.kanji.Add(kele);
                                        kele = new KanjiElement();
                                        break;
                                    case "r_ele":
                                        entry.readings.Add(rele);
                                        rele = new ReadingElement();
                                        break;
                                    case "sense":
                                        entry.senses.Add(sense);
                                        sense = new SenseElement();
                                        break;
                                    case "entry":
                                        insideElement = false;
                                        entries.Add(entry);
                                        break;
                                    default:
                                        //Console.WriteLine("End {0}", r.Name);
                                        break;
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            return entries;
        }

        static Dictionary<string, IndexEntry> BuildIndex(List<Entry> entries) {
            var terms = new Dictionary<string, IndexEntry>();
            foreach (var e in entries) {
                foreach (var kanji in e.kanji) {
                    IndexEntry.Add(terms, kanji.reading, e.index);
                }
                foreach (var reading in e.readings) {
                    IndexEntry.Add(terms, reading.reading, e.index);
                }
                foreach (var sense in e.senses) {
                    foreach (var gloss in sense.glosses) {
                        IndexEntry.Add(terms, gloss, e.index);
                    }
                }
            }
            return terms;
        }


        static void WriteStringToFile(BinaryWriter bw, string str) {
            bw.Write((UInt16) System.Text.Encoding.UTF8.GetByteCount(str));
            bw.Write(System.Text.Encoding.UTF8.GetBytes(str));
        }
        static UInt32 WriteEntryToFile(BinaryWriter bw, Entry entry) {
            UInt32 pos = (UInt32)bw.Seek(0, SeekOrigin.Current);
            int sz = entry.kanji.Count + entry.readings.Count;
            foreach (var sense in entry.senses) {
                sz += sense.glosses.Count + sense.partsOfSpeech.Count;
            }
            UInt16 entrySize = (UInt16)sz;
            bw.Write(entrySize);

            foreach (var kanji in entry.kanji) {
                var sb = new StringBuilder("k");
                foreach (var f in kanji.freq) {
                    sb.Append("," + f);
                }
                WriteStringToFile(bw, sb.ToString());
                WriteStringToFile(bw, kanji.reading);
            }

            foreach (var reading in entry.readings) {
                var sb = new StringBuilder("r");
                foreach (var f in reading.freq) {
                    sb.Append("," + f);
                }
                WriteStringToFile(bw, sb.ToString());
                WriteStringToFile(bw, reading.reading);
            }

            int i = 0;
            foreach (var sense in entry.senses) {
                string s = "s," + i.ToString();
                foreach (var gloss in sense.glosses) {
                    WriteStringToFile(bw, s + ",g");
                    WriteStringToFile(bw, gloss);
                }
                foreach (var partOfSpeech in sense.partsOfSpeech) {
                    WriteStringToFile(bw, s + ",p");
                    WriteStringToFile(bw, partOfSpeech);
                }

                i++;
            }

            return pos;
        }

        static void WriteIndexItemToFile(BinaryWriter bw, string indexItem, IndexEntry entry, Dictionary<int, UInt32> offsets) {
            WriteStringToFile(bw, indexItem);
            bw.Write((UInt16)entry.xref.Count);
            foreach (var xref in entry.xref) {
                bw.Write((UInt32)offsets[xref]);
            }
        }

        public class OrdinalComparer : IComparer<string> {
            public int Compare(string a, string b) {
                return String.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }
        static void Main(string[] args) {
            var entries = ReadEntries(args[0]);
            var indexTerms = BuildIndex(entries);
            var maxIndexRefSize = indexTerms.Values.Max(x => x.xref.Count);
            var sortedIndex = new List<String>(from pair in indexTerms select pair.Key);
            sortedIndex.Sort(new OrdinalComparer());
            var dictFileOffsets = new Dictionary<int, UInt32>();
            entries.Sort();

            using (var stream = File.Create(args[1])) {
                var bw = new BinaryWriter(stream);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("TDIC"));
                bw.Write((UInt32)0xFEFEFEFE); // file offset of search index, fixed up below
                bw.Write((UInt32)indexTerms.Count);


                foreach (var entry in entries) {
                    var pos = WriteEntryToFile(bw, entry);
                    dictFileOffsets.Add(entry.index, pos);
                }

                // write the offset to the search index
                var afterEntryPos = bw.Seek(0, SeekOrigin.Current);
                bw.Seek(4, SeekOrigin.Begin);
                bw.Write((UInt32)afterEntryPos);
                bw.Seek(0, SeekOrigin.End);

                // write out the search index
                foreach (var indexItem in sortedIndex) {
                    var item = indexTerms[indexItem];
                    WriteIndexItemToFile(bw, indexItem, item, dictFileOffsets);
                }


            }


            return;
        }
    }
}
