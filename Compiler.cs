using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using static System.StringComparer;

namespace ForthCompiler
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    public class Compiler
    {
        public List<Token> Tokens { get; } = new List<Token>();

        public Token Token => _tokenIndex < Tokens.Count ? Tokens[_tokenIndex] : null;
        public Token ArgumentToken { get; set; }
        private Token _lastToken;
        private List<CodeSlot> _compilation = new List<CodeSlot>();

        public List<CodeSlot> CodeSlots { get; set; }

        public Dictionary<string, IDictEntry> Dict { get; } = new Dictionary<string, IDictEntry>(OrdinalIgnoreCase);
        private Dictionary<string, IDictEntry> PreComp { get; } = new Dictionary<string, IDictEntry>(OrdinalIgnoreCase);
        public Dictionary<string, string> TestCases { get; } = new Dictionary<string, string>();

        public Dictionary<string, string[]> Sources { get; } = new Dictionary<string, string[]>(OrdinalIgnoreCase);

        private int _heapSize;
        private int _tokenIndex;
        private int _prerequisiteIndex;
        private readonly Stack<Structure> _structureStack = new Stack<Structure>(new[] { new Structure { Name = "Global" } });
        private readonly List<string> _arguments = new List<string>();

        public void LoadCore()
        {
            var entries = new Dictionary<bool, Dictionary<string, IDictEntry>> { { true, PreComp }, { false, Dict } };

            foreach (Code code in Enum.GetValues(typeof(Code)))
            {
                Dict.Add($"/{code}", new MacroCode { Code = code });
            }

            foreach (var method in GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<Method>() != null))
            {
                var attribute = method.GetCustomAttribute<Method>();

                attribute.Name = attribute.Name ?? method.Name;
                attribute.Action = (Action)Delegate.CreateDelegate(typeof(Action), this, method);
                entries[attribute.IsPrecompile].Add(attribute.Name, attribute);

                if (attribute.IsComment)
                {
                    entries[true].Add(attribute.Name, attribute);
                }
            }

            ReadFile(0, "core.4th", y => y, x => x, "Core.4th".LoadFileOrResource().SplitLines());
            Compile();
            _tokenIndex = _prerequisiteIndex = 0;
            _compilation.Clear();
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

        public void ReadFile(int pos, string file, Func<int, int> y, Func<int, int> x, string[] input, int macroLevel = 0)
        {
            Sources.At(file, () => input);
            Tokens.InsertRange(pos, input.SelectMany(
                (s, i) => Regex.Matches(s, @"""([^""]|"""")*""|\S+|\s+")
                               .OfType<Match>()
                               .Select(m => new Token(m.Value, file, y(i), x(m.Index), macroLevel))));
        }

        public void Precompile()
        {
            for (_tokenIndex = 0; _tokenIndex < Tokens.Count; _tokenIndex++)
            {
                if (PreComp.ContainsKey(Token.Text))
                {
                    ParseSymbol(PreComp);
                }
            }
        }

        public void Compile()
        {
            for (_tokenIndex = 0; _tokenIndex < Tokens.Count; _tokenIndex++)
            {
                if (Token.TokenType == TokenType.Literal)
                {
                    ArgumentToken = Token;
                    Encode(Convert.ToInt32(Token.Text.Trim('$', '#', '%'),
                        Token.Text.StartsWith("$")
                            ? 16
                            : Token.Text.StartsWith("%") ? 2 : 10));
                    _lastToken = Token;
                }
                else if (Token.TokenType != TokenType.Excluded)
                {
                    ParseSymbol(Dict);
                    _lastToken = Token;
                }
            }
        }

        public void PostCompile()
        {
            var labels = _compilation.Where(cs => cs.Code == Code.Label)
                                  .GroupBy(cs => cs.Label)
                                  .ToDictionary(cs => cs.Key, cs => cs.First(), OrdinalIgnoreCase);
            int i, index = 0, lits = 0;

            CodeSlots = new List<CodeSlot>();
            foreach (var codeslot in _compilation)
            {
                if (codeslot.Code == Code.Lit || codeslot.Code == Code.Address)
                {
                    lits += 8;
                }

                if (codeslot.Code != Code.Label)
                {
                    codeslot.CodeIndex = CodeSlots.Count;
                    CodeSlots.Add(codeslot);
                }

                if (codeslot.Code == Code.Label || codeslot == _compilation.Last())
                {
                    CodeSlots.AddRange(Enumerable.Range(0, 8 - CodeSlots.Count % 8).Select(e => (CodeSlot)Code._));
                }

                if (CodeSlots.Count % 8 == 0)
                {
                    CodeSlots.AddRange(Enumerable.Range(0, lits).Select(e => (CodeSlot)null));
                    lits = 0;
                }

                if (codeslot.Code == Code.Label)
                {
                    codeslot.CodeIndex = CodeSlots.Count;
                }
            }

            foreach (var address in _compilation.Where(cs => cs.Code == Code.Address))
            {
                CodeSlots[address.CodeIndex] = labels[address.Label].CodeIndex / 8;
            }

            foreach (var label in labels.Where(l => CodeSlots[l.Value.CodeIndex] != null))
            {
                CodeSlots[label.Value.CodeIndex].Label = label.Key;
            }


            for (i = Tokens.Count - 1, index = CodeSlots.Count; i >= 0; i--)
            {
                Tokens[i].CodeIndex = index = Tokens[i].CodeSlot?.CodeIndex ?? index;
            }

            for (i = Tokens.Count - 1, index = CodeSlots.Count; i >= 0; index = Tokens[i--].CodeIndex)
            {
                Tokens[i].CodeCount = index - Tokens[i].CodeIndex;
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
            _arguments.Clear();
            ArgumentToken = Token;

            var dictEntry = dict.At(ArgumentToken.Text).Validate(null, $"{ArgumentToken.Text} is undefined");

            for (int i = 0; i < (dictEntry as Method)?.Arguments; i++)
            {
                for (_tokenIndex++; Tokens[_tokenIndex].TokenType == TokenType.Excluded; _tokenIndex++)
                {
                }

                _arguments.Add(Token.Text);
            }

            dictEntry.Process(this);
        }

        private Cpu Evaluate(Token start)
        {
            start = Tokens.SkipWhile(t => t != start).FirstOrDefault(t => t.CodeSlot != null).Validate(null, "Missing code to evaluate");

            var slot = _compilation.IndexOf(start.CodeSlot);

            if (_compilation.Skip(slot).Any(cs => cs.Code == Code.Label))
            {
                PostCompile();
            }
            else
            {
                CodeSlots = _compilation;
            }

            var cpu = new Cpu(this) { ProgramSlot = CodeSlots.IndexOf(start.CodeSlot) };

            cpu.Run(i => cpu.ProgramSlot >= CodeSlots.Count);
            _compilation.RemoveRange(slot, _compilation.Count - slot);
            Tokens.SkipWhile(t => t != start).ToList().ForEach(t => t.CodeSlot = null);
            CodeSlots = null;

            return cpu;
        }

        public void Macro(string macro)
        {
            ReadFile(_tokenIndex + 1, Token.File, y => Token.Y, y => Token.X, new[] { $" {macro}" }, Token.MacroLevel + 1);
        }

        public void Encode(CodeSlot code)
        {
            ArgumentToken.CodeSlot = code;
            _compilation.Add(code);
        }

        [Method(Name = nameof(Include), Arguments = 1, IsPrecompile = true)]
        private void IncludePrecompile()
        {
            var filename = _arguments[0].Dequote();

            ReadFile(_tokenIndex + 1, filename, y => y, x => x, File.ReadAllLines(filename));
        }

        [Method(Arguments = 1, IsPrecompile = false)]
        private void Include()
        {
        }

        [Method]
        private void Allot()
        {
            var cpu = Evaluate(_lastToken).Validate(x => x.ForthStack.Count() == 1, "ALLOT expects 1 preceding value(s)");

            _heapSize += cpu.ForthStack.First();
        }

        int ParseBlock(string end)
        {
            var start = ++_tokenIndex;

            for (; !Token.Text.IsEqual(end); _tokenIndex++)
            {
                if (_tokenIndex >= Tokens.Count)
                {
                    _tokenIndex = start - 1;
                    throw new Exception($"Expecting {end}");
                }

                if ((Dict.At(Token.Text) as Method)?.IsComment == true)
                {
                    Dict.At(Token.Text).Process(this);
                }
            }

            return start;
        }

        string ReadBlock(int start)
        {
            var text = new StringBuilder();

            for (int i = start; i < _tokenIndex; i++)
            {
                if (Tokens[i - 1].Y != Tokens[i].Y || Tokens[i - 1].File != Tokens[i].File)
                {
                    text.AppendLine();
                }
                text.Append(Tokens[i].Text);
            }

            return text.ToString().Trim();
        }

        [Method(Arguments = 1)]
        private void Macro()
        {
            Dict.At(_arguments[0].Dequote(), () => new MacroText(), true).Text = ReadBlock(ParseBlock(nameof(EndMacro)));
        }

        [Method]
        private void EndMacro()
        {
        }

        [Method(Arguments = 2)]
        public void TestCase()
        {
            var target = _arguments[0].Dequote();
            var expectedresult = _arguments[1].Dequote();
            var testcode = ReadBlock(ParseBlock(nameof(EndTestCase)));
            var count = Regex.Matches(expectedresult, "\\S+").Count;

            TestCases.At(testcode, () => $"( Test Case {target} ) {testcode} ( = ) {expectedresult} ( ) {count}", true);
        }

        [Method]
        public void EndTestCase()
        {
        }

        [Method(Arguments = 1)]
        private void Struct()
        {
            _structureStack.Push(new Structure { Name = _arguments[0].Dequote(), Value = _tokenIndex });
        }

        [Method(Arguments = 1)]
        private void EndStruct()
        {
            _structureStack.Pop(_arguments[0].Dequote());
        }

        [Method(Arguments = 1)]
        private void Constant()
        {
            var cpu = Evaluate(_lastToken).Validate(x => x.ForthStack.Count() == 1, "CONSTANT expects 1 preceding value(s)");

            Token.TokenType = TokenType.Constant;
            Dict.At(_arguments[0], () => new ConstantEntry { Value = cpu.ForthStack.First() }, true);
        }

        [Method(Arguments = 1)]
        private void Addr()
        {
            var prefix = _arguments[0].Split('.').First();
            var structure = _structureStack.First(s => s.Name.IsEqual(prefix));

            Encode(new CodeSlot { Code = Code.Address, Label = _arguments[0] + structure.Value });
        }

        [Method(Arguments = 1)]
        private void Label()
        {
            var prefix = _arguments[0].Split('.').First();
            var structure = _structureStack.First(s => s.Name.IsEqual(prefix));

            Encode(new CodeSlot { Code = Code.Label, Label = _arguments[0] + structure.Value });
        }

        [Method(Arguments = 1)]
        private void Value()
        {
            Macro($"variable {_arguments[0]} {_arguments[0]} !");
        }

        [Method(Arguments = 1)]
        private void Variable()
        {
            Token.TokenType = TokenType.Variable;
            Dict.At(_arguments[0], () => new VariableEntry { HeapAddress = _heapSize++ }, true);
        }

        [Method]
        private void NotImplementedException()
        {
            throw new NotImplementedException();
        }

        [Method(Name = "[")]
        private void CompilerEvalStart()
        {
            _structureStack.Push(new Structure { Name = nameof(CompilerEvalStart), Value = _tokenIndex });
        }

        [Method(Name = "]")]
        private void CompilerEvalStop()
        {
            var start = Tokens[_structureStack.Pop(nameof(CompilerEvalStart)).Value];
            var cpu = Evaluate(start);

            cpu.ForthStack.Reverse().ToList().ForEach(v => Encode(v));
        }

        [Method(Name = "(", IsComment = true)]
        private void CommentBracket()
        {
            var pos = _tokenIndex++;

            ParseBlock(")");

            while (pos <= _tokenIndex)
            {
                Tokens[pos++].TokenType = TokenType.Excluded;
            }
        }

        [Method(Name = "\\", IsComment = true)]
        private void CommentBackSlash()
        {
            var start = Token;
            for (; _tokenIndex < Tokens.Count && Tokens[_tokenIndex].File == start.File && Tokens[_tokenIndex].Y == start.Y; _tokenIndex++)
            {
                Token.TokenType = TokenType.Excluded;
            }

            _tokenIndex--;
        }

        [Method(Name = ":", Arguments = 1)]
        public void DefinitionStart()
        {
            Token.TokenType = TokenType.Definition;
            Dict.At(_arguments[0], () => new DefinitionEntry(), true);

            Macro("Struct Definition " +
                 $"addr Definition.SKIP /jnz label Global.{Token.Text}.Label _take1_");
        }

        [Method(Name = ";")]
        private void DefinitionEnd()
        {
            Macro("label Definition.EXIT _R1_ @ _drop1_ /jnz label Definition.SKIP " +
                  "EndStruct Definition");
        }

        [Method(Arguments = 2)]
        public void Prerequisite()
        {
            PreComp.At(_arguments[0].Dequote(), () => new Prerequisite()).References.Add(_arguments[1]);
        }

        public void Prerequisite(string reference)
        {
            if (!Dict.ContainsKey($"included {reference}"))
            {
                var macro = (Dict.At(reference) as MacroText).Validate(null, $"{reference} is not defined");
                var count = Tokens.Count;

                Dict.Add($"included {reference}", null);
                ReadFile(_prerequisiteIndex, reference, y => y, x => x, macro.Text.SplitLines());

                _prerequisiteIndex += Tokens.Count - count;
                _tokenIndex += Tokens.Count - count;
            }
        }
    }

    public class Method : Attribute, IDictEntry
    {
        public string Name { get; set; }

        public Action Action { get; set; }

        public int Arguments { get; set; }

        public bool IsComment { get; set; }

        public bool IsPrecompile { get; set; }

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

    public class Structure
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }
}