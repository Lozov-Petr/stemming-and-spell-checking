using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Ru;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.Util;

using LuceneAttribute = Lucene.Net.Util.Attribute;
using LuceneSpellChecker = SpellChecker.Net.Search.Spell.SpellChecker;
using LevenshteinDistance = SpellChecker.Net.Search.Spell.LevenshteinDistance;

namespace lucenenet.V1
{
    class CorpusDictionaryAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            var tokenizer = new RussianLetterTokenizer(reader);
            var lowerCaseFilter = new LowerCaseFilter(tokenizer);
            var goodWordsFilter = new GoodWordsFilter(lowerCaseFilter);

            return goodWordsFilter;
        }

        public Dictionary<string, int> GetCorpusDictionary(TextReader reader)
        {
            var stream = TokenStream(null, reader);
            var dict = new Dictionary<string, int>();

            while (stream.IncrementToken())
            {
                var term = stream.GetAttribute<ITermAttribute>().Term;

                if (dict.ContainsKey(term)) ++dict[term];
                else dict.Add(term, 1);
            }

            return dict;
        }
    }

    class StemmerCompareAnalyzer : Analyzer
    {
        public ISet<string> StopWords { get; private set; }
        public LuceneSpellChecker SpellChecker { get; private set; }
        public int NumberOfSuggestions { get; private set; }
        public Dictionary<string, int> CorpusDictionary { get; private set; }

        public StemmerCompareAnalyzer( ISet<string> stopWords, LuceneSpellChecker spellChecker, int numberOfSuggestion, Dictionary<string, int> corpusDictionary)
        {
            StopWords = stopWords;
            SpellChecker = spellChecker;
            NumberOfSuggestions = numberOfSuggestion;
            CorpusDictionary = corpusDictionary;
        }

        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            var attributeSource = new AttributeSource();
            attributeSource.AddAttributeImpl(new SpellAndStemAttribute());
            attributeSource.AddAttributeImpl(new StemAttribute());
            attributeSource.AddAttributeImpl(new SourceAttribute());
            attributeSource.AddAttributeImpl(new SpellAttribute());
            attributeSource.AddAttributeImpl(new ConfidenceAttribute());
            attributeSource.AddAttributeImpl(new SpellsAttribute());

            var tokenizer = new RussianLetterTokenizer(attributeSource, reader);
            var lowerCaseFilter = new LowerCaseFilter(tokenizer);
            var goodWordsFilter = new GoodWordsFilter(lowerCaseFilter);
            var stopWordFilter = new StopFilter(false, goodWordsFilter, StopWords);
            var preFilter = new PreFilter(stopWordFilter, SpellChecker, NumberOfSuggestions);
            var confidenceFilter = new ConfidenceFilter(preFilter, CorpusDictionary);
            var stemFilter = new StemFilter(confidenceFilter);
            var similarFilter = new SimilarFilter(stemFilter);

            return similarFilter;
        }
    }

    class GoodWordsFilter : TokenFilter
    {
        static private HashSet<char> _goodChars;
        static private string _goodCharsStr = "абвгдеёжзийклмнопрстуфхцчшщьыъэя-";

        static GoodWordsFilter()
        {
            _goodChars = new HashSet<char>(_goodCharsStr.ToCharArray());
        }

        public GoodWordsFilter(TokenStream in_Renamed) : base(in_Renamed)
        {

        }

        public override bool IncrementToken()
        {
            bool wasGoodWord = false;
            ITermAttribute termAttribute = null;
            string term = null;

            while (!wasGoodWord)
            {
                if (!input.IncrementToken())
                {
                    return false;
                }

                termAttribute = GetAttribute<ITermAttribute>();
                
                term = termAttribute.Term.Replace('ё', 'е');

                var termArr = term.ToCharArray();

                if (termArr.Length < 3) continue;

                wasGoodWord = true;

                for (int i = 0; i < termArr.Length; ++i)
                    if (!_goodChars.Contains(termArr[i])) wasGoodWord = false;

            }

            termAttribute.SetTermBuffer(term);

            return true;
        }
    }

    class PreFilter : TokenFilter
    {
        public LuceneSpellChecker SpellChecker { get; private set; }
        public int NumberOfSuggestions { get; set; }

        public PreFilter(TokenStream in_Renamed, LuceneSpellChecker spellChecker, int numberOfSuggestions) : base(in_Renamed)
        {
            SpellChecker = spellChecker;
            NumberOfSuggestions = numberOfSuggestions;
        }

        public override bool IncrementToken()
        {
            if (!input.IncrementToken())
            {
                return false;
            }

            var termAttribute = GetAttribute<ITermAttribute>();
            var sourceAttribute = GetAttribute<ISourceAttribute>();
            var spellsAttribute = GetAttribute<ISpellsAttribute>();

            sourceAttribute.Term = termAttribute.Term;

            if (!SpellChecker.Exist(sourceAttribute.Term))
            {
                if (sourceAttribute.Term == "зарание")
                {

                }
                
                var res = SpellChecker.SuggestSimilar(sourceAttribute.Term, NumberOfSuggestions);
                if (res.Length != 0) spellsAttribute.Terms = res;
            }

            return true;
        }
    }

    class ConfidenceFilter : TokenFilter
    {
        private Dictionary<string, int> corpusDictionary;
        private LevenshteinDistance levenshteinDistance = new LevenshteinDistance();
        
        public ConfidenceFilter(TokenStream stream, Dictionary<string, int> dict) : base(stream)
        {
            corpusDictionary = dict;
        }
        
        public override bool IncrementToken()
        {
            if (!input.IncrementToken())
            {
                return false;
            }

            var spellsAttribute = GetAttribute<ISpellsAttribute>();
            var sourceAttribute = GetAttribute<ISourceAttribute>();
            var spellAttribute = GetAttribute<ISpellAttribute>();
            var confAttribute = GetAttribute<IConfidenceAttribute>();

            var source = sourceAttribute.Term;
            var spells = spellsAttribute.Terms;

            if (spells == null || spells.Length == 0)
            {
                spellAttribute.Term = source;
                return true;
            }

            string bestSpell = null;
            double bestConfidence = 0;

            foreach (var spell in spells)
            {
                var levenshteinValue = (int)Math.Round((1 - levenshteinDistance.GetDistance(source, spell)) * Math.Max(source.Length, spell.Length));

                if (levenshteinValue > 2 && bestSpell != null) break;

                if (levenshteinValue == 0)
                {
                    bestSpell = spell;
                    bestConfidence = 1;
                    break;
                }

                int sourceFreq, spellFreq;

                if (!corpusDictionary.TryGetValue(source, out sourceFreq)) sourceFreq = 0;
                if (!corpusDictionary.TryGetValue(spell, out spellFreq)) spellFreq = 0;

                var ratio = (double)(1 + sourceFreq) / (double)(1 + spellFreq);

                if (ratio >= 1) --ratio;
                else ratio = Math.Max(-1.99, 1 - 1 / ratio);

                var result = 1 / (1 + Math.Max(0, levenshteinValue + ratio));

                if (result > bestConfidence)
                {
                    bestConfidence = result;
                    bestSpell = spell;
                    if (bestConfidence == 1 || levenshteinValue > 2) break;
                }
            }

            spellAttribute.Term = bestSpell;
            confAttribute.Confidence = bestConfidence;

            return true;
        }
    }

    class StemFilter : TokenFilter
    {
        public StemFilter(TokenStream in_Renamed) : base(in_Renamed)
        {

        }

        private RussianStemmer _stemmer = new RussianStemmer();

        public override bool IncrementToken()
        {
            if (!input.IncrementToken())
            {
                return false;
            }

            ISourceAttribute sourceAttribute = GetAttribute<ISourceAttribute>();
            IStemAttribute stemAttribute = GetAttribute<IStemAttribute>();
            ISpellAttribute spellAttribute = GetAttribute<ISpellAttribute>();
            ISpellAndStemAttribute spellAndStemAttribute = GetAttribute<ISpellAndStemAttribute>();

            stemAttribute.Term = _stemmer.Stem(sourceAttribute.Term);
            spellAndStemAttribute.Term = _stemmer.Stem(spellAttribute.Term);

            return true;
        }
    }

    class SimilarFilter : TokenFilter
    {
        public SimilarFilter(TokenStream in_Renamed)
            : base(in_Renamed)
        {

        }

        public override bool IncrementToken()
        {
            bool wasSimilar = false;

            while (!wasSimilar)
            {
                if (!input.IncrementToken())
                {
                    return false;
                }

                ISpellAndStemAttribute spellAttribute = GetAttribute<ISpellAndStemAttribute>();
                IStemAttribute stemAttribute = GetAttribute<IStemAttribute>();

                if (stemAttribute.Term != spellAttribute.Term)
                {
                    wasSimilar = true;
                }
            }

            return true;
        }
    }

    interface ISpellAndStemAttribute : IAttribute
    {
        string Term { get; set; }
    }

    interface ISpellsAttribute : IAttribute
    {
        string[] Terms { get; set; }
    }

    interface ISpellAttribute : IAttribute
    {
        string Term { get; set; }
    }

    interface IStemAttribute : IAttribute
    {
        string Term { get; set; }
    }

    interface ISourceAttribute : IAttribute
    {
        string Term { get; set; }
    }

    interface IConfidenceAttribute : IAttribute
    {
        double Confidence { get; set; }
    }

    class SpellAndStemAttribute : LuceneAttribute, ISpellAndStemAttribute
    {
        public string Term { get; set; }

        public override void Clear()
        {
        }

        public override void CopyTo(LuceneAttribute target)
        {
            throw new NotImplementedException();
        }

        public override bool Equals(object other)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }

    class SpellAttribute : LuceneAttribute, ISpellAttribute
    {
        public override void Clear()
        {
        }

        public override void CopyTo(LuceneAttribute target)
        {
 	        throw new NotImplementedException();
        }

        public override bool Equals(object other)
        {
 	        throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
 	        throw new NotImplementedException();
        }

        public string Term { get; set; }
    }

    class SpellsAttribute : LuceneAttribute, ISpellsAttribute
    {
        public override void Clear()
        {
            Terms = null;
        }

        public override void CopyTo(LuceneAttribute target)
        {
            throw new NotImplementedException();
        }

        public override bool Equals(object other)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public string[] Terms { get; set; }
    }

    class StemAttribute : LuceneAttribute, IStemAttribute
    {
        public string Term { get; set; }


        public override void Clear()
        {
        }

        public override void CopyTo(LuceneAttribute target)
        {
            throw new NotImplementedException();
        }

        public override bool Equals(object other)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }

    class SourceAttribute : LuceneAttribute, ISourceAttribute
    {
        public string Term { get; set; }

        public override void Clear()
        {
        }

        public override void CopyTo(LuceneAttribute target)
        {
            throw new NotImplementedException();
        }

        public override bool Equals(object other)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }

    class ConfidenceAttribute : LuceneAttribute, IConfidenceAttribute
    {
        public override void Clear()
        {
        }

        public override void CopyTo(LuceneAttribute target)
        {
            throw new NotImplementedException();
        }

        public override bool Equals(object other)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public double Confidence { get; set; }
    }
}
