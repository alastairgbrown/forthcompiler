using System;
using System.Collections.Generic;
using System.Reflection;

namespace ForthCompiler
{
    public class LabelEntry : IDictEntry
    {
        public MethodInfo Method => null;
        public TokenType TokenType => TokenType.Label;
        public List<int> Patches { get; set; }
        public int CodeSlot { get; set; }

    }
}