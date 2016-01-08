using System;
using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Ru;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.Store;
using SpellChecker.Net.Search.Spell;
using Lucene.Net.Util;
using System.Collections.Generic;
using System.Linq;
using ISourceAttribute = lucenenet.V1.ISourceAttribute;

using lucenenet.V1;
using System.Globalization;

namespace lucenenet
{
    class Program
    {
        static void Main()
        {
            //PrivateMain();
            SortOutput();
        }
    
        static void PrivateMain()
        {
            var numberOfSuggestion = 100;

            var testFilePath = @"../../../data/russianPosts.txt";
            var testDictionaryPath = @"../../../data/russian.dic";
            var testIndexPath = @"../../../data/indexV1_2";
            var stopWordsPath = @"../../../data/stopWords.txt";
            var outputFilePath = @"../../../data/output.txt";

            var stopWordsSet = new HashSet<string>();
            using (var reader = new StreamReader(stopWordsPath))
            {
                while (!reader.EndOfStream) stopWordsSet.Add(reader.ReadLine());
            }

            if (!File.Exists(testFilePath))
            {
                Console.WriteLine("Unpack the archive with the russian posts");
                Environment.Exit(1);
            }

            using (var reader1 = new StreamReader(testFilePath))
            using (var reader2 = new StreamReader(testFilePath))
            using (var writer = new StreamWriter(outputFilePath))
            {
                var directory = new SimpleFSDirectory(new DirectoryInfo(testIndexPath));
                var spellChecker = new SpellChecker.Net.Search.Spell.SpellChecker(directory);
                spellChecker.IndexDictionary(new PlainTextDictionary(new FileInfo(testDictionaryPath)));

                var corpusDictionaryAnalyzer = new CorpusDictionaryAnalyzer();
                var corpusDictionary = corpusDictionaryAnalyzer.GetCorpusDictionary(reader1);

                var analyzer = new StemmerCompareAnalyzer(stopWordsSet, spellChecker, numberOfSuggestion, corpusDictionary);

                var stream = analyzer.TokenStream(null, reader2);

                while (stream.IncrementToken())
                {
                    var source = stream.GetAttribute<ISourceAttribute>().Term;
                    var spellAndStem = stream.GetAttribute<ISpellAndStemAttribute>().Term;
                    var stem = stream.GetAttribute<IStemAttribute>().Term;
                    var spell = stream.GetAttribute<ISpellAttribute>().Term;
                    var confidence = stream.GetAttribute<IConfidenceAttribute>().Confidence;

                    writer.WriteLine("{0, -25} {1, -25} {2, -25} {3, -25}\t{4}", source, spell, stem, spellAndStem, confidence.ToString("F2"));
                    //Console.WriteLine("{0, -25} {1, -25} {2, -25} {3, -25}\t{4}", source, spell, stem, spellAndStem, confidence.ToString("F2"));
                }
            }
        }

        static void SupportMain()
        {
            var testDictionaryPath = @"../../../data/ru.dict";
            var outputDictionaryPath = @"../../../data/ruStem.dict";

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

        static void SortOutput()
        {
            var outputFilePath = @"..\..\..\data\output.txt";
            var result95FilePath = @"..\..\..\data\result95.txt";
            var rest95FilePath = @"..\..\..\data\rest95.txt";
            var resultFilePath = @"..\..\..\data\result.txt";
            var replacements = new Dictionary<string, int>();

            using (var reader = new StreamReader(outputFilePath))
            {
                while (!reader.EndOfStream)
                {
                    var replacement = reader.ReadLine();

                    if (replacements.ContainsKey(replacement))
                    {
                        replacements[replacement]++;
                    }
                    else
                    {
                        replacements.Add(replacement, 1);
                    }
                }
            }

            var result = from entry in replacements orderby entry.Value descending select entry;

            using (var result95Writer = new StreamWriter(result95FilePath))
            using (var rest95Writer = new StreamWriter(rest95FilePath))
            using (var resultWriter = new StreamWriter(resultFilePath))
            foreach (var entry in result)
            {
                resultWriter.WriteLine("{0}\t{1}", entry.Key, entry.Value);

                var confidence = double.Parse(entry.Key.Substring(entry.Key.Length - 4, 4), CultureInfo.CurrentCulture);
                
                if (confidence >= 0.95)
                    result95Writer.WriteLine("{0}\t{1}", entry.Key, entry.Value);
                else
                    rest95Writer.WriteLine("{0}\t{1}", entry.Key, entry.Value);
            }
        }
    }
}

