using System;
using System.Collections.Generic;

namespace ForthCompiler
{
    public class LabelEntry : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            throw new NotImplementedException();
        }

        public TokenType TokenType => TokenType.Label;
        public List<int> Patches { get; set; }
        public int CodeSlot { get; set; }

    }
}