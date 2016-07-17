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

        public Dictionary<DictType, Dictionary<string, IDictEntry>> Entries { get; } =
            new Dictionary<DictType, Dictionary<string, IDictEntry>> {
                {DictType.PreComp, new Dictionary<string, IDictEntry>(StringComparer.OrdinalIgnoreCase)},
                {DictType.Dict, new Dictionary<string, IDictEntry>(StringComparer.OrdinalIgnoreCase)},
            };

        public Dictionary<string, IDictEntry> Dict => Entries[DictType.Dict];
        public Dictionary<string, Label> Labels { get; } = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> TestCases { get; } = new Dictionary<string, string>();

        public int HeapSize { get; private set; }

        private int _tokenIndex;
        private readonly Stack<Structure> _structureStack = new Stack<Structure>{ new Structure { Name = "Global" }};
        private int _prerequisiteIndex;
        private TokenType _macroClass;

        public Compiler()
        {
            foreach (Code code in Enum.GetValues(typeof(Code)))
            {
                Dict.Add($"/{code}", new MacroCode { TokenType = TokenType.Math, Code = code });
            }

            foreach (var method in GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<Method>() != null))
            {
                var attribute = method.GetCustomAttribute<Method>();

                attribute.Name = attribute.Name ?? method.Name;
                attribute.MethodName = method.Name;
                attribute.Action = (Action)Delegate.CreateDelegate(typeof(Action), this, method);
                Entries[attribute.DictType].Add(attribute.Name, attribute);
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
                    if (Entries[DictType.PreComp].ContainsKey(Token.Text))
                    {
                        ParseSymbol(DictType.PreComp);
                    }
                }

                for (_tokenIndex = 0; _tokenIndex < Tokens.Count; _tokenIndex++)
                {
                    Token.CodeSlot = CodeSlots.Count;

                    if (Token.TokenType == TokenType.Literal)
                    {
                        Encode(Code.Psh, Convert.ToInt32(
                                            Token.Text.Trim('$', '#', '%'),
                                            Token.Text.StartsWith("$") ? 16 : 
                                            Token.Text.StartsWith("%") ? 2 : 10));
                    }
                    else if (Token.TokenType != TokenType.Excluded)
                    {
                        ParseSymbol(DictType.Dict);
                    }
                }

                foreach (var label in Labels.Where(t => t.Value.Patches != null))
                {
                    throw new Exception("Unpatched label " + label.Key);
                }

            }
            catch (Exception ex)
            {
                Error = $"Error: {ex.Message}{Environment.NewLine}File: {Token?.File}({Token?.Y + 1},{Token?.X + 1})";
                Console.WriteLine(Error);
                Tokens.Skip(_tokenIndex).ToList().ForEach(t => t.SetError());
            }

            CheckSequence(Code.Psh, Code.Zeq, Code.Swp, Code.Zeq, Code.Pop);
            CheckSequence(Code.Psh, Code.Zeq, Code.Pop);
            CheckSequence(Code.Psh, Code.Pop);
            CheckSequence(0);
            CheckSequence(1);
            CheckSequence(2);
            CheckSequence(-1);

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

        private void CheckSequence(params CodeSlot[] codes)
        {
            var compressed = CodeSlots.Where(c => c != null).ToArray();
            var count = Enumerable.Range(0, Math.Max(0, compressed.Length - codes.Length + 1))
                .Count(i => Enumerable.Range(0, codes.Length).All(j => codes[j].Code == compressed[i + j].Code && codes[j].Value == compressed[i + j].Value));

            Console.WriteLine($"{string.Join(" ", codes.OfType<object>())} occurences: {count}");
        }

        private void ParseSymbol(DictType dictType)
        {
            IDictEntry dictEntry;
            List<Token> arguments = null;

            if (!Entries[dictType].TryGetValue(Token.Text, out dictEntry))
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

        [Method(null, TokenType.Organisation, Arguments = 3)]
        public void TestCase()
        {
            var target = Token.Arguments[0].Text.Trim('"');
            var testcode = Token.Arguments[1].Text.Trim('"');
            var expectedresult = Token.Arguments[2].Text.Trim('"');
            var count = Regex.Matches(expectedresult, "\\S+").Count;
            
            TestCases.MakeEntry(testcode, () => $"( Test Case {target} ) {testcode} ( = ) {expectedresult} ( ) {count}");
        }

        [Method(nameof(Include), TokenType.Organisation, Arguments = 1, DictType = DictType.PreComp)]
        private void IncludePrecompile()
        {
            var filename = Token.Text.Trim('"');

            ReadFile(_tokenIndex + 1, filename, y => y, x => x, File.ReadAllLines(filename));
        }

        [Method(null, TokenType.Organisation, Arguments = 1, DictType = DictType.Dict)]
        private void Include()
        {
        }

        [Method(null, TokenType.Organisation)]
        private void Allot()
        {
            var cpu = Evaluate(Tokens[_tokenIndex - 2]);

            HeapSize += cpu.ForthStack.First();
        }

        [Method(null, TokenType.Organisation, Arguments = 1)]
        private void MacroClass()
        {
            _macroClass = (TokenType)Enum.Parse(typeof(TokenType), Token.Text, true);
        }

        [Method(null, TokenType.Organisation, Arguments = 1)]
        private void Macro()
        {
            var macro = Dict.MakeEntry(Token.Text, () => new MacroText { TokenType = _macroClass }, true);
            var text = new StringBuilder();
            var lastLine = (int?)null;

            for (_tokenIndex++; !Token.Text.IsEqual(nameof(MacroEnd)); _tokenIndex++)
            {
                IDictEntry comment;

                if (Dict.TryGetValue(Token.Text, out comment) && comment.TokenType == TokenType.Excluded)
                {
                    comment.Process(this);
                }
                else
                {
                    if (lastLine != null && lastLine != Token.Y)
                    {
                        text.AppendLine();
                    }
                    text.Append(Token.Text);
                    lastLine = Token.Y;
                }
            }

            macro.Text = text.ToString().Trim();
        }

        [Method(null, TokenType.Organisation)]
        private void MacroEnd()
        {
        }

        [Method(null, TokenType.Organisation, Arguments = 1)]
        private void Struct()
        {
            _structureStack.Push(new Structure { Name = Token.Text, Value = _tokenIndex });
        }

        [Method(null, TokenType.Organisation, Arguments = 1)]
        private void StructEnd()
        {
            _structureStack.Pop(Token.Text);
        }

        [Method(null, TokenType.Organisation, Arguments = 1)]
        private void Constant()
        {
            var cpu = Evaluate(Tokens[_tokenIndex - 4]);

            Dict.MakeEntry(Token.Text, () => new ConstantEntry { Value = cpu.ForthStack.First() }, true);
        }

        [Method(null, TokenType.Organisation, Arguments = 1)]
        private void Addr()
        {
            var prefix = Token.Text.Split('.').First();
            var structure = _structureStack.First(s => s.Name.IsEqual(prefix));
            var label = Labels.MakeEntry(Token.Text + structure.Value, () => new Label { Patches = new List<int>() });

            Encode(Code.Psh);

            label.Patches?.Add(CodeSlots.Count);

            Encode(label.CodeSlot / 8);
        }

        [Method(null, TokenType.Organisation, Arguments = 1)]
        private void Label()
        {
            var prefix = Token.Text.Split('.').First();
            var structure = _structureStack.First(s => s.Name.IsEqual(prefix));
            var label = Labels.MakeEntry(Token.Text + structure.Value, () => new Label());

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

        [Method(null, TokenType.Organisation, Arguments = 1)]
        private void Value()
        {
            Macro($"variable {Token.Text} {Token.Text} !");
        }

        [Method(null, TokenType.Organisation, Arguments = 1)]
        private void Variable()
        {
            Token.DictEntry = Dict.MakeEntry(Token.Text, () => new VariableEntry { HeapAddress = HeapSize++ }, true);
        }

        [Method(null, TokenType.Organisation)]
        private void NotImplementedException()
        {
            throw new NotImplementedException();
        }


        [Method("[", TokenType.Math)]
        private void CompilerEvalStart()
        {
            _structureStack.Push(new Structure { Name = nameof(CompilerEvalStart), Value = _tokenIndex });
        }

        [Method("]", TokenType.Math)]
        private void CompilerEvalStop()
        {
            var start = Tokens[_structureStack.Pop(nameof(CompilerEvalStart)).Value];
            var cpu = Evaluate(start);

            foreach (var value in cpu.ForthStack.Reverse())
            {
                Encode(Code.Psh, value);
            }
        }

        [Method("(", TokenType.Excluded)]
        private void CommentBracket()
        {
            var start = Token;
            while (Token.Text != ")")
            {
                Tokens[++_tokenIndex].DictEntry = start.DictEntry;
            }
        }

        [Method("\\", TokenType.Excluded)]
        private void CommentBackSlash()
        {
            var start = Token;
            while (_tokenIndex < Tokens.Count && Tokens[_tokenIndex].File == start.File && Tokens[_tokenIndex].Y == start.Y)
            {
                Tokens[_tokenIndex++].DictEntry = start.DictEntry;
            }

            _tokenIndex--;
        }

        [Method(":", TokenType.Definition, Arguments = 1)]
        public void DefinitionStart()
        {
            Dict.MakeEntry(Token.Text, () => new DefinitionEntry(), true);

            Macro("struct Definition " +
                 $"addr Definition.SKIP /jnz label Global.{Token.Text}. _take1_");
        }

        [Method(";", TokenType.Definition)]
        private void DefinitionEnd()
        {
            Macro("label Definition.EXIT _R1_ @ _drop1_ /jnz label Definition.SKIP " +
                  "structend Definition");
        }

        [Method(null, TokenType.Organisation, Arguments = 2)]
        public void Prerequisite()
        {
            var prerequisite = Entries[DictType.PreComp].MakeEntry(Token.Arguments[0].Text, () => new Prerequisite());

            prerequisite.References.Add(Token.Arguments[1].Text);
        }

        public void Prerequisite(string reference)
        {
            if (!Dict.ContainsKey($"included {reference}"))
            {
                var macro = Dict.MakeEntry<IDictEntry, MacroText>(reference, () => { throw new Exception($"{reference} is not defined"); });
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

        public TokenType TokenType { get; }

        public Action Action { get; set; }

        public DictType DictType { get; set; } = DictType.Dict;

        public int Arguments { get; set; }

        public Method(string name, TokenType tokenType)
        {
            Name = name;
            TokenType = tokenType;
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
            compiler.Encode(Code.Psh, HeapAddress);
        }

        public TokenType TokenType => TokenType.Variable;
        public int HeapAddress { get; set; }
    }

    public class ConstantEntry : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            compiler.Encode(Code.Psh, Value);
        }
        public TokenType TokenType => TokenType.Constant;
        public int Value { get; set; }
    }

    public class DefinitionEntry : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            compiler.Macro($"addr Global.{compiler.Token.Text}. /CNZ label Global.Placeholder");
        }
        public TokenType TokenType => TokenType.Definition;
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
        public int Next { get; set; }
    }

    public enum DictType
    {
        PreComp,
        Dict,
    }
}