using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ForthCompiler
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    public class Compiler
    {
        public List<Token> Tokens { get; } = new List<Token>();

        public Token Token => _tokenIndex < Tokens.Count ? Tokens[_tokenIndex] : null;

        public List<CodeSlot> CodeSlots { get; } = new List<CodeSlot>();

        public string Error { get; private set; }

        public Dictionary<string, IDictEntry> Dict { get; } = new Dictionary<string, IDictEntry>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, IDictEntry> PreComp { get; } = new Dictionary<string, IDictEntry>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Label> Labels { get; } = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> TestCases { get; } = new Dictionary<string, string>();

        private int _heapSize;
        private int _tokenIndex;
        private int _prerequisiteIndex;
        private readonly Stack<Structure> _structureStack = new Stack<Structure>(new[] { new Structure { Name = "Global" } });

        public Compiler()
        {
            var entries = new Dictionary<DictType, Dictionary<string, IDictEntry>> { { DictType.PreComp, PreComp }, { DictType.Dict, Dict } };

            foreach (Code code in Enum.GetValues(typeof(Code)))
            {
                Dict.Add($"/{code}", new MacroCode { Code = code });
            }

            foreach (var method in GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<Method>() != null))
            {
                var attribute = method.GetCustomAttribute<Method>();

                attribute.Name = attribute.Name ?? method.Name;
                attribute.MethodName = method.Name;
                attribute.Action = (Action)Delegate.CreateDelegate(typeof(Action), this, method);
                entries[attribute.DictType].Add(attribute.Name, attribute);
            }

            ReadFile(0, "core.4th", y => y, x => x, File.ReadAllLines("core.4th"));
            Parse();
            _tokenIndex = _prerequisiteIndex = 0;
            CodeSlots.Clear();
            Tokens.Clear();
        }

        public IEnumerable<string> MakeMif()
        {
            var depth = CodeSlots.Count / 8;

            yield return $"DEPTH = {depth}; --The size of memory in words";
            yield return "WIDTH = 32; --The size of data in bits";
            yield return "ADDRESS_RADIX = HEX; --The radix for address values";
            yield return "DATA_RADIX = HEX; --The radix for data values";
            yield return "CONTENT-- start of(address: data pairs)";
            yield return "BEGIN";
            yield return "";

            for (var index = 0; index < depth; index++)
            {
                var code = Enumerable.Range(index * 8, 8).Sum(i => (int)CodeSlots[i].Code << ((i - index * 8) * 4));

                yield return $"{index:X4} : {code:X8};";

                foreach (var i in Enumerable.Range(index * 8, 8).Where(i => CodeSlots[i].Code == Code.Lit))
                {
                    yield return $"{++index:X4} : {CodeSlots[i].Value:X8};";
                }
            }

            yield return "";
            yield return "END";
        }

        public void Macro(string macro)
        {
            ReadFile(_tokenIndex + 1, Token.File, y => Token.Y, y => Token.X, new[] { $" {macro}" }, Token.MacroLevel + 1);
        }

        public void Encode(params CodeSlot[] codes)
        {
            foreach (var code in codes)
            {
                CodeSlots.Add(code);

                if (CodeSlots.Count % 8 == 0)
                {
                    var blanks = CodeSlots.Skip(CodeSlots.Count - 8).Count(cs => cs.Code == Code.Lit) * 8;
                    CodeSlots.AddRange(Enumerable.Range(0, blanks).Select(i => (CodeSlot)null));
                }
            }
        }

        public void ReadFile(int pos, string file, Func<int, int> y, Func<int, int> x, IEnumerable<string> input, int macroLevel = 0)
        {
            Tokens.InsertRange(pos, input.SelectMany(
                (s, i) => Regex.Matches(s, @"""([^""]|"""")*""|\S+|\s+")
                               .OfType<Match>()
                               .Select(m => new Token(m.Value, file, y(i), x(m.Index), macroLevel))));
        }

        public void Parse()
        {
            try
            {
                for (_tokenIndex = 0; _tokenIndex < Tokens.Count; _tokenIndex++)
                {
                    if (PreComp.ContainsKey(Token.Text))
                    {
                        ParseSymbol(PreComp);
                    }
                }

                for (_tokenIndex = 0; _tokenIndex < Tokens.Count; _tokenIndex++)
                {
                    Token.CodeSlot = CodeSlots.Count;

                    if (Token.TokenType == TokenType.Literal)
                    {
                        Encode(Convert.ToInt32(
                                        Token.Text.Trim('$', '#', '%'),
                                        Token.Text.StartsWith("$") ? 16 :
                                        Token.Text.StartsWith("%") ? 2 : 10));
                    }
                    else if (Token.TokenType != TokenType.Excluded)
                    {
                        ParseSymbol(Dict);
                    }
                }

                foreach (var label in Labels.Where(t => t.Value.Patches != null))
                {
                    throw new Exception("Unpatched label " + label.Key);
                }

            }
            catch (NotSupportedException ex)
            {
                Error = $"Error: {ex.Message}{Environment.NewLine}File: {Token?.File}({Token?.Y + 1},{Token?.X + 1})";
                Console.WriteLine(Error);
                Tokens.Skip(_tokenIndex).ToList().ForEach(t => t.SetError());
            }

            while (CodeSlots.Count % 8 != 0)
            {
                Encode(Code._);
            }

            for (int i = 0, slot = 0; i < Tokens.Count; i++)
            {
                Tokens[i].CodeSlot = slot = Math.Max(Tokens[i].CodeSlot, slot);
            }

            for (int i = Tokens.Count - 1, slot = CodeSlots.Count; i >= 0; slot = Tokens[i--].CodeSlot)
            {
                Tokens[i].CodeCount = slot - Tokens[i].CodeSlot;
            }
        }

        public void CheckSequences()
        {
            CheckSequence(Code.Psh, Code.Zeq, Code.Swp, Code.Zeq, Code.Pop);
            CheckSequence(Code.Psh, Code.Zeq, Code.Pop);
            CheckSequence(Code.Psh, Code.Pop);
            CheckSequence(0);
            CheckSequence(1);
            CheckSequence(2);
            CheckSequence(-1);
        }

        private void CheckSequence(params CodeSlot[] codes)
        {
            var compressed = CodeSlots.Where(c => c != null).ToArray();
            var count = Enumerable.Range(0, Math.Max(0, compressed.Length - codes.Length + 1))
                .Count(i => Enumerable.Range(0, codes.Length).All(j => codes[j].Code == compressed[i + j].Code && codes[j].Value == compressed[i + j].Value));

            Console.WriteLine($"{string.Join(" ", codes.OfType<object>())} occurences: {count}");
        }

        private void ParseSymbol(Dictionary<string, IDictEntry> dict)
        {
            IDictEntry dictEntry;
            List<Token> arguments = null;

            if (!dict.TryGetValue(Token.Text, out dictEntry))
            {
                throw new Exception("Undefined symbol - " + Token.Text);
            }

            Token.CodeSlot = CodeSlots.Count;
            Token.DictEntry = dictEntry;
            for (int i = 0; i < (dictEntry as Method)?.Arguments; i++)
            {
                for (_tokenIndex++; Tokens[_tokenIndex].TokenType == TokenType.Excluded; _tokenIndex++)
                {
                }

                (arguments = arguments ?? new List<Token>()).Add(Token);
                Token.CodeSlot = CodeSlots.Count;
                Token.DictEntry = dictEntry;
            }

            Token.Arguments = arguments;
            dictEntry.Process(this);
        }

        private Cpu Evaluate(Token start)
        {
            var cpu = new Cpu(this) { ProgramSlot = start.CodeSlot };

            cpu.Run(i => cpu.ProgramSlot >= CodeSlots.Count);
            CodeSlots.RemoveRange(start.CodeSlot, CodeSlots.Count - start.CodeSlot);
            Tokens.SkipWhile(t => t != start).ToList().ForEach(t => t.CodeSlot = CodeSlots.Count);

            return cpu;
        }

        [Method(nameof(Include), Arguments = 1, DictType = DictType.PreComp)]
        private void IncludePrecompile()
        {
            var filename = Token.Text.Dequote();

            ReadFile(_tokenIndex + 1, filename, y => y, x => x, File.ReadAllLines(filename));
        }

        [Method(null, Arguments = 1, DictType = DictType.Dict)]
        private void Include()
        {
        }

        [Method(null)]
        private void Allot()
        {
            var cpu = Evaluate(Tokens[_tokenIndex - 2]);

            _heapSize += cpu.ForthStack.First();
        }

        string ParseBlock(string end)
        {
            var start = ++_tokenIndex;
            var text = new StringBuilder();

            for (; !Token.Text.IsEqual(end); _tokenIndex++)
            {
                IDictEntry comment;

                if (Dict.TryGetValue(Token.Text, out comment) &&
                    ((comment as Method)?.MethodName == nameof(CommentBracket) || (comment as Method)?.MethodName == nameof(CommentBackSlash)))
                {
                    comment.Process(this);
                }
            }

            for (int i = start; i < _tokenIndex; i++)
            {
                if (i > start && Tokens[i - 1].Y != Tokens[i].Y)
                {
                    text.AppendLine();
                }
                text.Append(Tokens[i].Text);
            }

            return text.ToString().Trim();
        }

        [Method(null, Arguments = 1)]
        private void Macro()
        {
            var macro = Dict.Entry(Token.Text.Dequote(), () => new MacroText(), true);

            macro.Text = ParseBlock(nameof(EndMacro));
        }

        [Method(null)]
        private void EndMacro()
        {
        }

        [Method(null, Arguments = 2)]
        public void TestCase()
        {
            var target = Token.Arguments[0].Text.Dequote();
            var expectedresult = Token.Arguments[1].Text.Dequote();
            var testcode = ParseBlock(nameof(EndTestCase));
            var count = Regex.Matches(expectedresult, "\\S+").Count;

            TestCases.Entry(testcode, () => $"( Test Case {target} ) {testcode} ( = ) {expectedresult} ( ) {count}");
        }

        [Method(null, Arguments = 2)]
        public void EndTestCase()
        {
        }

        [Method(null, Arguments = 1)]
        private void Struct()
        {
            _structureStack.Push(new Structure { Name = Token.Text.Dequote(), Value = _tokenIndex });
        }

        [Method(null, Arguments = 1)]
        private void EndStruct()
        {
            _structureStack.Pop(Token.Text.Dequote());
        }

        [Method(null, Arguments = 1)]
        private void Constant()
        {
            var cpu = Evaluate(Tokens[_tokenIndex - 4]);

            Token.TokenType = TokenType.Constant;
            Dict.Entry(Token.Text, () => new ConstantEntry { Value = cpu.ForthStack.First() }, true);
        }

        [Method(null, Arguments = 1)]
        private void Addr()
        {
            var prefix = Token.Text.Split('.').First();
            var structure = _structureStack.First(s => s.Name.IsEqual(prefix));
            var label = Labels.Entry(Token.Text + structure.Value, () => new Label { Patches = new List<int>() });

            label.Patches?.Add(CodeSlots.Count);

            Encode(label.CodeSlot / 8);
        }

        [Method(null, Arguments = 1)]
        private void Label()
        {
            var prefix = Token.Text.Split('.').First();
            var structure = _structureStack.First(s => s.Name.IsEqual(prefix));
            var label = Labels.Entry(Token.Text + structure.Value, () => new Label());

            while (CodeSlots.Count % 8 != 0)
            {
                Encode(Code._);
            }

            foreach (var patch in label.Patches ?? Enumerable.Empty<int>())
            {
                CodeSlots[patch].Value = CodeSlots.Count / 8;
            }

            label.CodeSlot = CodeSlots.Count;
            label.Patches = null;
        }

        [Method(null, Arguments = 1)]
        private void Value()
        {
            Macro($"variable {Token.Text} {Token.Text} !");
        }

        [Method(null, Arguments = 1)]
        private void Variable()
        {
            Token.TokenType = TokenType.Variable;
            Token.DictEntry = Dict.Entry(Token.Text, () => new VariableEntry { HeapAddress = _heapSize++ }, true);
        }

        [Method(null)]
        private void NotImplementedException()
        {
            throw new NotImplementedException();
        }

        [Method("[")]
        private void CompilerEvalStart()
        {
            _structureStack.Push(new Structure { Name = nameof(CompilerEvalStart), Value = _tokenIndex });
        }

        [Method("]")]
        private void CompilerEvalStop()
        {
            var start = Tokens[_structureStack.Pop(nameof(CompilerEvalStart)).Value];
            var cpu = Evaluate(start);

            cpu.ForthStack.Reverse().ToList().ForEach(v => Encode(v));
        }

        [Method("(")]
        private void CommentBracket()
        {
            var start = Token;
            for  (;Token.Text != ")"; ++_tokenIndex)
            {
                Token.TokenType = TokenType.Excluded;
                Token.DictEntry = start.DictEntry;
            }
            Token.TokenType = TokenType.Excluded;
            Token.DictEntry = start.DictEntry;
        }

        [Method("\\")]
        private void CommentBackSlash()
        {
            var start = Token;
            for (; _tokenIndex < Tokens.Count && Tokens[_tokenIndex].File == start.File && Tokens[_tokenIndex].Y == start.Y; _tokenIndex++)
            {
                Token.TokenType = TokenType.Excluded;
                Token.DictEntry = start.DictEntry;
            }

            _tokenIndex--;
        }

        [Method(":", Arguments = 1)]
        public void DefinitionStart()
        {
            Token.TokenType = TokenType.Definition;
            Dict.Entry(Token.Text, () => new DefinitionEntry(), true);

            Macro("Struct Definition " +
                 $"addr Definition.SKIP /jnz label Global.{Token.Text}.Label _take1_");
        }

        [Method(";")]
        private void DefinitionEnd()
        {
            Macro("label Definition.EXIT _R1_ @ _drop1_ /jnz label Definition.SKIP " +
                  "EndStruct Definition");
        }

        [Method(null, Arguments = 2)]
        public void Prerequisite()
        {
            var prerequisite = PreComp.Entry(Token.Arguments[0].Text, () => new Prerequisite());

            prerequisite.References.Add(Token.Arguments[1].Text);
        }

        public void Prerequisite(string reference)
        {
            if (!Dict.ContainsKey($"included {reference}"))
            {
                var macro = Dict.Entry<IDictEntry, MacroText>(reference, () => { throw new Exception($"{reference} is not defined"); });
                var count = Tokens.Count;

                Dict.Add($"included {reference}", null);
                ReadFile(_prerequisiteIndex, reference, y => y, x => x, macro.Text.Split(new[] { "\r\n", "\r", "\n" }, 0));

                _prerequisiteIndex += Tokens.Count - count;
                _tokenIndex += Tokens.Count - count;
            }
        }
    }

    public class Method : Attribute, IDictEntry
    {
        public string Name { get; set; }

        public string MethodName { get; set; }

        public Action Action { get; set; }

        public DictType DictType { get; set; } = DictType.Dict;

        public int Arguments { get; set; }

        public Method(string name)
        {
            Name = name;
        }

        public void Process(Compiler compiler)
        {
            Action();
        }
    }

    public class VariableEntry : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            compiler.Token.TokenType = TokenType.Variable;
            compiler.Encode(HeapAddress);
        }

        public int HeapAddress { get; set; }
    }

    public class ConstantEntry : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            compiler.Token.TokenType = TokenType.Constant;
            compiler.Encode(Value);
        }
        public int Value { get; set; }
    }

    public class DefinitionEntry : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            compiler.Token.TokenType = TokenType.Definition;
            compiler.Macro($"addr Global.{compiler.Token.Text}.Label /jsr label Global.Placeholder");
        }
    }

    public class TestCase : IDictEntry
    {
        public void Process(Compiler compiler)
        {
        }

        public TokenType TokenType => TokenType.Undetermined;
        public string Text { get; }
        public string For { get; }

        public TestCase(string @for, string testcode, string expectedresult)
        {
            Text = $"( Test Case {@for} ) {testcode} ( = ) {expectedresult} ( ) {Regex.Matches(expectedresult, "\\S+").Count}";
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
        public TokenType TokenType => TokenType.Undetermined;
        public List<string> References { get; }

        public Prerequisite(params string[] references)
        {
            References = references.ToList();
        }
    }

    public class CodeSlot
    {
        public Code Code { get; set; }
        public int Value { get; set; }

        public static implicit operator CodeSlot(Code code)
        {
            return new CodeSlot { Code = code };
        }

        public static implicit operator CodeSlot(int value)
        {
            return new CodeSlot { Code = Code.Lit, Value = value };
        }

        public override string ToString()
        {
            return $"{Code}{(Code == Code.Lit ? " " + Value : "")}";
        }
    }

    public class Structure
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }

    public enum DictType
    {
        PreComp,
        Dict,
    }
}