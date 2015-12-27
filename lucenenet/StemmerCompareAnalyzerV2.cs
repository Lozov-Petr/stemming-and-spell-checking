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
using SpellChecker.Net.Search.Spell;
using LuceneAttribute = Lucene.Net.Util.Attribute;
using LuceneSpellChecker = SpellChecker.Net.Search.Spell.SpellChecker;

namespace lucenenet.V2
{
    class StemmerCompareAnalyzer : Analyzer
    {
        public ISet<string> StopWords { get; private set; }
        public LuceneSpellChecker SpellChecker { get; private set; }
        public int NumberOfSuggestions { get; private set; }

        public StemmerCompareAnalyzer( ISet<string> stopWords, LuceneSpellChecker spellChecker, int numberOfSuggestion)
        {
            StopWords = stopWords;
            SpellChecker = spellChecker;
            NumberOfSuggestions = numberOfSuggestion;
        }

        public override TokenStream TokenStream(string fieldName, TextReader reader)
        {
            var attributeSource = new AttributeSource();
            attributeSource.AddAttributeImpl(new SpellAttribute());
            attributeSource.AddAttributeImpl(new StemAttribute());

            var tokenizer = new RussianLetterTokenizer(attributeSource, reader);
            var lowercaseFilter = new LowerCaseFilter(tokenizer);
            var badWordsFilter = new BadWordsFilter(lowercaseFilter);
            var stopWordFilter = new StopFilter(false, badWordsFilter, StopWords);
            var preFilter = new StemFilter(stopWordFilter, SpellChecker, NumberOfSuggestions);
            var similarFilter = new SimilarFilter(preFilter);

            return similarFilter;
        }
    }

    class StemFilter : TokenFilter
    {
        public LuceneSpellChecker SpellChecker { get; private set; }
        public int NumberOfSuggestions { get; set; }

        private StringDistance _defaultDistance;
        private StringDistance _customDistance;

        private RussianStemmer _stemmer = new RussianStemmer();

        public StemFilter(TokenStream in_Renamed, LuceneSpellChecker spellChecker, int numberOfSuggestions)
            : base(in_Renamed)
        {
            SpellChecker = spellChecker;
            NumberOfSuggestions = numberOfSuggestions;
            _defaultDistance = spellChecker.GetStringDistance();
            _customDistance = new StemDistance(_defaultDistance);
        }

        public override bool IncrementToken()
        {
            if (!input.IncrementToken())
            {
                return false;
            }

            ITermAttribute termAttribute = GetAttribute<ITermAttribute>();
            ISpellAttribute spellAttribute = GetAttribute<ISpellAttribute>();
            IStemAttribute stemAttribute = GetAttribute<IStemAttribute>();

            var term = termAttribute.Term;
            var stem = _stemmer.Stem(term);
            stemAttribute.Term = stem;

            SpellChecker.setStringDistance(_customDistance);

            if (!SpellChecker.Exist(term))
            {
                var similarWords = SpellChecker.SuggestSimilar(term, NumberOfSuggestions);
                if (similarWords.Length != 0)
                {
                    spellAttribute.Term = similarWords[0];
                    return true;
                }
            }
            else
            {
                spellAttribute.Term = term;
                return true;
            }

            SpellChecker.setStringDistance(_defaultDistance);

            if (!SpellChecker.Exist(stem))
            {
                var similarWords = SpellChecker.SuggestSimilar(stem, NumberOfSuggestions);
                if (similarWords.Length != 0)
                {
                    spellAttribute.Term = similarWords[0];
                    return true;
                }
            }

            spellAttribute.Term = stem;
            return true;

        }
    }

    class BadWordsFilter : TokenFilter
    {
        static private HashSet<char> _goodChars;
        static private string _goodCharsStr = "абвгдеёжзийклмнопрстуфхцчшщьыъэя-";

        static BadWordsFilter()
        {
            _goodChars = new HashSet<char>(_goodCharsStr.ToCharArray());
        }

        public BadWordsFilter(TokenStream in_Renamed)
            : base(in_Renamed)
        {

        }

        public override bool IncrementToken()
        {
            bool wasGoodWord = false;

            while (!wasGoodWord)
            {
                if (!input.IncrementToken())
                {
                    return false;
                }

                ITermAttribute termAttribute = GetAttribute<ITermAttribute>();
                var termArr = termAttribute.Term.ToCharArray();

                if (termArr.Length < 3) continue;

                wasGoodWord = true;
               
                for (int i = 0; i < termArr.Length; ++i)
                    if (!_goodChars.Contains(termArr[i])) wasGoodWord = false;

            }

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

                ISpellAttribute spellAttribute = GetAttribute<ISpellAttribute>();
                IStemAttribute stemAttribute = GetAttribute<IStemAttribute>();

                if (stemAttribute.Term != spellAttribute.Term)
                {
                    wasSimilar = true;
                }
            }

            return true;
        }
    }

    interface ISpellAttribute : IAttribute
    {
        string Term { get; set; }
    }

    interface IStemAttribute : IAttribute
    {
        string Term { get; set; }
    }

    class SpellAttribute : LuceneAttribute, ISpellAttribute
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

    class StemDistance : StringDistance
    {
        StringDistance _parentDistance;

        public StemDistance(StringDistance parentDistance)
        {
            _parentDistance = parentDistance;
        } 

        public float GetDistance(string s1, string s2)
        {
            if (s1.Length < s2.Length || _parentDistance.GetDistance(s1.Substring(0, s2.Length), s2) != 1) return 0;

            if (s1.Length == s2.Length) return 1;

            return 1 / (float)(s2.Length - s1.Length);
        }
    }
}
