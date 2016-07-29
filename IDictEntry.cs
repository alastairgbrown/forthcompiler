using System.Collections.Generic;

namespace ForthCompiler
{
    public interface IDictEntry
    {
        void Process(Compiler compiler);
    }

    public class Variable : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            compiler.Token.TokenType = TokenType.Variable;
            compiler.Encode(HeapAddress);
        }

        public int HeapAddress { get; set; }
    }

    public class Constant : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            compiler.Token.TokenType = TokenType.Constant;
            compiler.Encode(Value);
        }

        public int Value { get; set; }
    }

    public class Definition : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            compiler.Token.TokenType = TokenType.Definition;
            compiler.Macro($"addr Global.{compiler.Token.Text} /jsr label Global.Placeholder");
        }
    }

    public class Prerequisite : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            foreach (var reference in References)
            {
                compiler.Prerequisite(reference);
            }
        }

        public List<string> References { get; } = new List<string>();
    }

    public class Macro : IDictEntry
    {
        public string Text { get; set; }

        public void Process(Compiler compiler)
        {
            compiler.Macro(Text);
        }
    }

    public class MacroCode : IDictEntry
    {
        public Code Code { get; set; }

        public void Process(Compiler compiler)
        {
            compiler.Encode(Code);
        }
    }

}