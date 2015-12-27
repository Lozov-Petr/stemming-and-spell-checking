using System;
using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Ru;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.Store;
using SpellChecker.Net.Search.Spell;
using Lucene.Net.Util;
using System.Collections.Generic;

using ISourceAttribute = lucenenet.V1.ISourceAttribute;

using lucenenet.V1;

namespace lucenenet
{
    class Program
    {
        static void Main()
        {
            MainV1_2();
        }

        static void MainV1()
        {
            var numberOfSuggestion = 100;

            var testFilePath = @"C:/lucene/test1.txt";
            var testDictionaryPath = @"C:/lucene/ru.dict";
            var testIndexPath = @"C:/lucene/indexV1";
            var stopWordsPath = @"C:/lucene/stopWords.txt";

            var stopWordsSet = new HashSet<string>();
            using (var reader = new StreamReader(stopWordsPath))
            {
                while (!reader.EndOfStream) stopWordsSet.Add(reader.ReadLine());
            }

            using (var reader = new StreamReader(testFilePath))
            {
                var directory = new SimpleFSDirectory(new DirectoryInfo(testIndexPath));
                var spellChecker = new SpellChecker.Net.Search.Spell.SpellChecker(directory);
                spellChecker.IndexDictionary(new PlainTextDictionary(new FileInfo(testDictionaryPath)));

                StringDistance getDist = spellChecker.GetStringDistance();

                var analyzer = new StemmerCompareAnalyzer(stopWordsSet, spellChecker, numberOfSuggestion);

                var stream = analyzer.TokenStream(null, reader);

                while (stream.IncrementToken())
                {
                    var sourceAttribute = stream.GetAttribute<ISourceAttribute>().Term;
                    var spellAttribute = stream.GetAttribute<ISpellAttribute>().Term;
                    var stemAttribute = stream.GetAttribute<IStemAttribute>().Term;

                    Console.WriteLine("{0, 20} {1, 20} {2, 20}", sourceAttribute, spellAttribute, stemAttribute);
                }
            }
        }

        static void MainV2()
        {
            var numberOfSuggestion = 100;
            
            var testFilePath = @"C:/lucene/test1.txt";
            var testDictionaryPath = @"C:/lucene/ruStem.dict";
            var testIndexPath = @"C:/lucene/indexV2";
            var stopWordsPath = @"C:/lucene/stopWords.txt";

            var stopWordsSet = new HashSet<string>();
            using (var reader = new StreamReader(stopWordsPath))
            {
                while (!reader.EndOfStream) stopWordsSet.Add(reader.ReadLine());
            }

            using (var reader = new StreamReader(testFilePath))
            {
                var directory = new SimpleFSDirectory(new DirectoryInfo(testIndexPath));
                var spellChecker = new SpellChecker.Net.Search.Spell.SpellChecker(directory);
                spellChecker.IndexDictionary(new PlainTextDictionary(new FileInfo(testDictionaryPath)));

                StringDistance getDist = spellChecker.GetStringDistance();
                
                var analyzer = new StemmerCompareAnalyzer(stopWordsSet, spellChecker, numberOfSuggestion);

                var stream = analyzer.TokenStream(null, reader);

                while (stream.IncrementToken())
                {
                    var termAttribute = stream.GetAttribute<ITermAttribute>().Term;
                    var spellAttribute = stream.GetAttribute<ISpellAttribute>().Term;
                    var stemAttribute = stream.GetAttribute<IStemAttribute>().Term;

                    Console.WriteLine("{0, 20} {1, 20} {2, 20}", termAttribute, spellAttribute, stemAttribute);
                }

            }
        }

        static void MainV1_2()
        {
            var numberOfSuggestion = 100;

            var testFilePath = @"C:/lucene/test1.txt";
            var testDictionaryPath = @"C:/lucene/russian.dic";
            var testIndexPath = @"C:/lucene/indexV1_2";
            var stopWordsPath = @"C:/lucene/stopWords.txt";

            var stopWordsSet = new HashSet<string>();
            using (var reader = new StreamReader(stopWordsPath))
            {
                while (!reader.EndOfStream) stopWordsSet.Add(reader.ReadLine());
            }

            using (var reader = new StreamReader(testFilePath))
            {
                var directory = new SimpleFSDirectory(new DirectoryInfo(testIndexPath));
                var spellChecker = new SpellChecker.Net.Search.Spell.SpellChecker(directory);
                spellChecker.IndexDictionary(new PlainTextDictionary(new FileInfo(testDictionaryPath)));

                var analyzer = new StemmerCompareAnalyzer(stopWordsSet, spellChecker, numberOfSuggestion);

                var stream = analyzer.TokenStream(null, reader);

                while (stream.IncrementToken())
                {
                    var sourceAttribute = stream.GetAttribute<ISourceAttribute>().Term;
                    var spellAttribute = stream.GetAttribute<ISpellAttribute>().Term;
                    var stemAttribute = stream.GetAttribute<IStemAttribute>().Term;

                    Console.WriteLine("{0, 20} {1, 20} {2, 20}", sourceAttribute, spellAttribute, stemAttribute);
                }
            }
        }

        static void SupportMain()
        {
            var testDictionaryPath = @"C:/lucene/ru.dict";
            var outputDictionaryPath = @"C:/lucene/ruStem.dict";

            using (var reader = new StreamReader(testDictionaryPath))
            using (var writer = new StreamWriter(outputDictionaryPath))
            {
                var analyzer = new RussianAnalyzer(Lucene.Net.Util.Version.LUCENE_30);
                var stream = analyzer.TokenStream(null, reader);

                var wordsSet = new HashSet<string>();

                while (stream.IncrementToken())
                {
                    var term = stream.GetAttribute<ITermAttribute>().Term;
                    if (term.Length > 2) wordsSet.Add(term);
                }

                foreach (var word in wordsSet) writer.WriteLine(word);

            }
        }
    }
}

