using System;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;

namespace lucenenet
{

    public class CustomFilter : TokenFilter
    {
        private Func<string, string>  _filterFunc;

        public CustomFilter(TokenStream in_Renamed, Func<string, string> filterFunc)
            : base(in_Renamed)
        {
            _filterFunc = filterFunc;
        }

        public override bool IncrementToken()
        {
            if (!input.IncrementToken())
            {
                return false;
            }

            ITermAttribute termAttribute = GetAttribute<ITermAttribute>();

            var stemTerm = _filterFunc(termAttribute.Term);

            termAttribute.SetTermBuffer(stemTerm);

            return true;
        }
    }
}