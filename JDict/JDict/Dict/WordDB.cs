using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Windows.ApplicationModel.Search;

namespace JDict.Dict {
    public class Entry {

        public class Kanji {
            public string text;
            public List<string> freq;
            public Kanji() {
                this.freq = new List<string>();
            }
        }
        public class Reading {
            public string text;
            public List<string> freq;
            public Reading() {
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

        public int uid;
        public List<Kanji> kanji;
        public List<Reading> readings;
        public List<SenseElement> senses;

        public Entry() {
            this.kanji = new List<Kanji>();
            this.readings = new List<Reading>();
            this.senses = new List<SenseElement>();
            this.Score = 0;
        }


        public int Score;
        public void CalculateFrequencyScore() {
            this.Score = 0;
            foreach (var r in this.readings) {
                foreach (var f in r.freq) {
                    if (f.StartsWith("ichi1")) {
                        this.Score = Math.Max(this.Score, 25);
                    }
                    if (f.StartsWith("gai1")) {
                        this.Score = Math.Max(this.Score, 35);
                    }
                    if (f.StartsWith("nf")) {
                        var score = int.Parse(f.Substring(2));
                        score = 60 - score;
                        this.Score = Math.Max(score, this.Score);
                    }
                }
            }
        }
    }

    public class EntryFrequencyComparer : IComparer<Entry> {
        public int Compare(Entry a, Entry b) {
            return b.Score - a.Score;
        }
    }
    
    public class IndexItem : IComparable {
        public string item;
        public UInt32[] matchOffsets;

        public int CompareTo(object o) {
            return String.Compare(this.item, (string)o, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    public class WordDB {
        private BinaryReader reader;
        private List<IndexItem> index;
        private Dictionary<uint, Entry> cache;
        public int IndexCount;

        public WordDB() {
            this.cache = new Dictionary<uint, Entry>();
            this.index = new List<IndexItem>();
            this.IndexCount = 0;
            LoadDatabase();
        }

        static public string ReadDatabaseString(BinaryReader r) {
            UInt16 sz = r.ReadUInt16();
            byte[] ba = r.ReadBytes(sz);
            return System.Text.Encoding.UTF8.GetString(ba,0,ba.Length);
        }
        static public int SearchOrder(string str, string word) {
            int comp = 0;
            if (str.Length <= word.Length) {
                comp = String.Compare(word.Substring(0, str.Length), str, StringComparison.OrdinalIgnoreCase);
            } else {
                string substr = str.Substring(0, word.Length);
                comp = String.Compare(word.Substring(0, substr.Length), substr, StringComparison.OrdinalIgnoreCase);
                if (comp == 0) {
                    // we compared a substring, so they can't match
                    comp = -1;
                }
            }

            return comp;
        }

        public IEnumerable<IndexItem> IndexLinearMatchesForTerm(string str) {
            return this.index.Where((x) => x.item.StartsWith(str));
        }

        public List<IndexItem> IndexMatchesForTerm(string str) {
            int min = 0;
            int max = this.index.Count;
            List<IndexItem> matches = new List<IndexItem>();
            while (max >= min) {
                int mid = (min + max) / 2;
                int comp = SearchOrder(str, this.index[mid].item);
                
                if (comp < 0) {
                    min = mid + 1;
                } else if (comp > 0) {
                    max = mid - 1;
                } else {
                    // we hit a match. grab everything before this that also matches, reverse the order so it's in alphabetical order, then add everything after this
                    // since everything is sorted, we know it's continuous
                    int start = mid;
                    do {
                        matches.Add(this.index[start]);
                        start--;
                        if (start < 0) {
                            break;
                        }
                    } while (SearchOrder(str, this.index[start].item) == 0);

                    matches.Reverse();
                    start = mid + 1;
                    while (start < this.index.Count && SearchOrder(str, this.index[start].item) == 0) {
                        matches.Add(this.index[start]);
                        start++;
                    }
                    
                    return matches;
                }
            }

            return matches;
        }

        public void InjectSuggestions(string userText, SearchSuggestionCollection ssc, HashSet<string> existingEntries) {
            List<IndexItem> matches = IndexMatchesForTerm(userText);
            foreach (var match in matches) {
                if (!existingEntries.Contains(match.item)) {
                    ssc.AppendQuerySuggestion(match.item);
                    existingEntries.Add(match.item);
                }
            }
            //suggestion.StartsWith(textToMatch, StringComparison.CurrentCultureIgnoreCase);
        }

        async public void LoadDatabase() {
            var uri = new System.Uri("ms-appx:///DB/JMdict.db");
            var file = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(uri);
            var stream = await file.OpenReadAsync();
            var r = new BinaryReader(stream.AsStreamForRead());
            var header = r.ReadBytes(4);
            var magic = System.Text.Encoding.UTF8.GetBytes("TDIC");
            if (!header.SequenceEqual(magic)) {
                throw new Exception("Database has invalid magic header. Please reinstall the application.");
            }
            UInt32 searchIndexOffset = r.ReadUInt32();
            UInt32 searchIndexSize = r.ReadUInt32();
            r.BaseStream.Seek(searchIndexOffset, SeekOrigin.Begin);

            var idx = new List<IndexItem>((int)searchIndexSize);
            for (UInt32 i = 0; i < searchIndexSize; i++) {
                var item = new IndexItem();
                item.item = WordDB.ReadDatabaseString(r);
                UInt16 xrefCount = r.ReadUInt16();
                UInt32[] xrefs = new UInt32[xrefCount];
                for (var xc = 0; xc < xrefCount; xc++) {
                    xrefs[xc] = r.ReadUInt32();
                }
                item.matchOffsets = xrefs;
                idx.Add(item);
            }
            
            this.index = idx;
            this.IndexCount = idx.Count;

            this.reader = r;

        }


        public Entry FetchEntry(uint offset) {
            if (this.cache.ContainsKey(offset)) {
                return this.cache[offset];
            }
            this.reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            Int16 entryCount = this.reader.ReadInt16();
            Entry e = new Entry();
            Entry.SenseElement se = new Entry.SenseElement();
            int seCurrentIndex = 0;
            for (int i = 0; i < entryCount; i++) {
                string itemprop = ReadDatabaseString(this.reader);
                string item = ReadDatabaseString(this.reader);
                if (itemprop.StartsWith("k")) {
                    var k = new Entry.Kanji();
                    k.text = item;
                    k.freq = new List<string>(itemprop.Split(',').Skip(1));
                    e.kanji.Add(k);
                } else if (itemprop.StartsWith("r")) {
                    var r = new Entry.Reading();
                    r.text = item;
                    r.freq = new List<string>(itemprop.Split(',').Skip(1));
                    e.readings.Add(r);
                } else if (itemprop.StartsWith("s")) {
                    string[] spl = itemprop.Split(',');
                    int seIndex = int.Parse(spl[1]);
                    if (seIndex != seCurrentIndex) {
                        e.senses.Add(se);
                        se = new Entry.SenseElement();
                        seCurrentIndex = seIndex;
                    }
                    
                    if (spl[2].Equals("g")) {
                        se.glosses.Add(item);
                    } else if (spl[2].Equals("p")) {
                        se.partsOfSpeech.Add(item);
                    }
                }
            }

            if (se.glosses.Count > 0) {
                e.senses.Add(se);
            }

            e.CalculateFrequencyScore();

            this.cache.Add(offset, e);
            return e;
        }
    }
}
