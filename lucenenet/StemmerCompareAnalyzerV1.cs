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

namespace lucenenet.V1
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
            attributeSource.AddAttributeImpl(new SourceAttribute());

            var tokenizer = new RussianLetterTokenizer(attributeSource, reader);
            var lowerCaseFilter = new LowerCaseFilter(tokenizer);
            var goodWoarsFilter = new GoodWordsFilter(lowerCaseFilter);
            var stopWordFilter1 = new StopFilter(false, goodWoarsFilter, StopWords);
            var preFilter = new PreFilter(stopWordFilter1, SpellChecker, NumberOfSuggestions);
            var stopWordFilter2 = new StopFilter(false, preFilter, StopWords);
            var stemFilter = new StemFilter(stopWordFilter2);
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

        public GoodWordsFilter(TokenStream in_Renamed)
            : base(in_Renamed)
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

        public PreFilter(TokenStream in_Renamed, LuceneSpellChecker spellChecker, int numberOfSuggestions)
            : base(in_Renamed)
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

            ITermAttribute termAttribute = this.GetAttribute<ITermAttribute>();
            ISourceAttribute sourceAttribute = GetAttribute<ISourceAttribute>();
            ISpellAttribute spellAttribute = GetAttribute<ISpellAttribute>();
            IStemAttribute stemAttribute = GetAttribute<IStemAttribute>();

            sourceAttribute.Term = termAttribute.Term;
            spellAttribute.Term = termAttribute.Term;
            stemAttribute.Term = termAttribute.Term;

            if (!SpellChecker.Exist(spellAttribute.Term))
            {
                var res = SpellChecker.SuggestSimilar(spellAttribute.Term, 100);
                if (res.Length != 0) spellAttribute.Term = res[0];
            }

            termAttribute.SetTermBuffer(spellAttribute.Term);

            return true;
        }
    }

    class StemFilter : TokenFilter
    {
        public StemFilter(TokenStream in_Renamed)
            : base(in_Renamed)
        {

        }

        private RussianStemmer _stemmer = new RussianStemmer();

        public override bool IncrementToken()
        {
            if (!input.IncrementToken())
            {
                return false;
            }

            ITermAttribute termAttribute = GetAttribute<ITermAttribute>();
            IStemAttribute stemAttribute = GetAttribute<IStemAttribute>();
            ISpellAttribute spellAttribute = GetAttribute<ISpellAttribute>();

            stemAttribute.Term = _stemmer.Stem(stemAttribute.Term);
            spellAttribute.Term = _stemmer.Stem(spellAttribute.Term);

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

    interface ISourceAttribute : IAttribute
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
}
