using System.Collections.Generic;
using System.Linq;

namespace ForthCompiler
{
    public interface IDictEntry
    {
        void Process(Compiler compiler);
    }

    public class Variable : IDictEntry
    {
        public long HeapAddress { get; set; }

        public void Process(Compiler compiler)
        {
            compiler.Token.TokenType = TokenType.Variable;
            compiler.Encode(OpCode.Psh);
            compiler.Encode(HeapAddress);
        }
    }

    public class Constant : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            foreach (var value in Value)
            {
                compiler.Token.TokenType = TokenType.Constant;
                compiler.Encode(OpCode.Psh);
                compiler.Encode(value);
            }
        }

        public long[] Value { get; set; }
    }

    public class Definition : IDictEntry
    {
        public string Label { get; set; }

        public Definition(string label)
        {
            Label = label;
        }

        public void Process(Compiler compiler)
        {
            compiler.Token.TokenType = TokenType.Definition;
            compiler.Macro($"addr {Label} /jsr label .Placeholder");
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
        public Token[] Tokens { get; set; }

        public Dictionary<bool,List<string>> Prereqs { get; set; }

        private static bool IsDefinition(Token token)
        {
            return token.IsEqual("IsDefinition");
        }

        public static bool IsRedefine(Token token)
        {
            return token.IsEqual("Redefine");
        }

        public void Process(Compiler compiler)
        {
            Definition definition = null;

            if (Tokens.Any(IsDefinition))
            {
                compiler.ParseWhiteSpace();
                compiler.Token.TokenType = TokenType.Definition;
                var name = compiler.Token.Text.Dequote();
                definition = compiler.Words.At(name, () => new Definition($".{name}"), true);
            }

            var token = compiler.Token;

            compiler.Tokens.InsertRange(
                compiler.TokenIndex + 1,
                Tokens.Where(t => !IsDefinition(t) && !IsRedefine(t)).Select(t =>
                    token.Clone(t.Text.Replace("{Label}", definition?.Label ?? "{Label}"), token.MacroLevel + 1, t.TokenType)));
        }
    }

    public class RawOpCode : IDictEntry
    {
        public OpCode OpCode { get; set; }

        public void Process(Compiler compiler)
        {
            compiler.Encode(OpCode);
        }
    }
}