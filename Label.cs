using System;
using System.Collections.Generic;

namespace ForthCompiler
{
    public class Label
    {
        public List<int> Patches { get; set; }
        public int CodeSlot { get; set; }
    }
}