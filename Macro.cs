using System.Linq;

namespace ForthCompiler
{
    public abstract class Macro 
    {
        public TokenType TokenType { get; set; }
    }

    public class MacroCode : Macro, IDictEntry
    {
        public Code[] Codes { get; set; }

        public void Process(Compiler compiler)
        {
            compiler.Encode(Codes.Select(c => (CodeSlot)c).ToArray());
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