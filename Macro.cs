using System.Collections.Generic;
using System.Reflection;

namespace ForthCompiler
{
    public abstract class Macro : IDictEntry
    {
        public abstract MethodInfo Method { get; }
        public TokenType TokenType { get; set; }
    }

    public class MacroCode : Macro
    {
        public override MethodInfo Method => typeof(Compiler).GetMethod(nameof(Compiler.MacroCode));
        public Code[] Codes { get; set; }
    }
    public class MacroText : Macro
    {
        public override MethodInfo Method => typeof(Compiler).GetMethod(nameof(Compiler.MacroText));
        public string Text { get; set; }
    }
}