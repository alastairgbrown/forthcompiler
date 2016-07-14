namespace ForthCompiler
{
    public abstract class Macro : IDictEntry
    {
        public abstract void Process(Compiler compiler);

        public TokenType TokenType { get; set; }
    }

    public class MacroCode : Macro
    {
        public Code[] Codes { get; set; }

        public override void Process(Compiler compiler)
        {
            compiler.Encode(Codes);
        }
    }

    public class MacroText : Macro
    {
        public string Text { get; set; }
        public override string ToString()
        {
            return Text;
        }

        public override void Process(Compiler compiler)
        {
            compiler.Macro(Text);
        }
    }
}