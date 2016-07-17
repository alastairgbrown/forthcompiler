using System.Linq;

namespace ForthCompiler
{
    public abstract class Macro 
    {
        public TokenType TokenType { get; set; }
    }

    public class MacroCode : Macro, IDictEntry
    {
        public Code Code { get; set; }

        public void Process(Compiler compiler)
        {
            compiler.Encode(Code);
        }
    }

    public class MacroText : Macro, IDictEntry
    {
        public string Text { get; set; }

        public void Process(Compiler compiler)
        {
            compiler.Macro(Text);
        }
    }
}