using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using static System.Math;
using static System.StringComparer;

namespace ForthCompiler
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    public class Compiler
    {
        public List<Token> Tokens { get; } = new List<Token>();

        public Token Token => _tokenIndex < Tokens.Count ? Tokens[_tokenIndex] : null;
        public Token ArgToken => _argIndex < Tokens.Count ? Tokens[_argIndex] : null;
        private Token _lastToken;
        public List<CodeSlot> Compilation { get; } = new List<CodeSlot>();
        private readonly List<string> _optimizations = new List<string>();
        private readonly List<string> _testCases = new List<string>();
        private static readonly Regex Parser = new Regex(@"""([^""]|"""")*""|\S+|\s+", RegexOptions.Compiled);

        public List<CodeSlot> CodeSlots { get; } = new List<CodeSlot>();

        public Dictionary<string, IDictEntry> Words { get; } = new Dictionary<string, IDictEntry>(OrdinalIgnoreCase);
        private Dictionary<string, IDictEntry> PrecompileWords { get; } = new Dictionary<string, IDictEntry>(OrdinalIgnoreCase);

        public Dictionary<string, string[]> Sources { get; } = new Dictionary<string, string[]>(OrdinalIgnoreCase);

        private int _heapSize;
        private int _tokenIndex;
        private int _argIndex;
        private int _prerequisiteIndex;
        private int _commentIndex;
        private int _structureSuffix;
        private readonly Stack<Structure> _structureStack = new Stack<Structure>(new[] { new Structure { Name = "Global" } });
        private readonly List<string> _argValues = new List<string>();

        public void LoadCore()
        {
            LoadCodes();
            LoadMethodAttributes();
            LoadCore4th();
        }

        private void LoadCodes()
        {
            foreach (Code code in Enum.GetValues(typeof(Code)))
            {
                Words.Add($"/{code}", new MacroCode { Code = code });
            }
        }

        private void LoadMethodAttributes()
        {
            var entries = new Dictionary<bool, Dictionary<string, IDictEntry>> { { true, PrecompileWords }, { false, Words } };

            foreach (var method in GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                            .Where(m => m.GetCustomAttribute<MethodAttribute>() != null))
            {
                var attribute = method.GetCustomAttribute<MethodAttribute>();

                attribute.Name = attribute.Name ?? method.Name;
                attribute.Action = (Action)Delegate.CreateDelegate(typeof(Action), this, method);
                entries[attribute.IsPrecompile][attribute.Name] = attribute;
            }
        }

        public void LoadCore4th(string code = null)
        {
            ReadFile(0, "Core.4th", y => y, x => x, code ?? "Core.4th".LoadFileOrResource());
            Compile();
            _tokenIndex = _prerequisiteIndex = _argIndex = 0;
            _lastToken = null;
            Compilation.Clear();
            Tokens.Clear();
        }

        public IEnumerable<string> GenerateMif()
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

        public IEnumerable<string> GenerateTestCases()
        {
            foreach (var testcase in _testCases)
            {
                var dict = TextToDict(testcase, "TestCase", "Produces", "ProducesException", "WithCore");

                if (dict.ContainsKey("Produces"))
                {
                    yield return $"( TestCase ) {dict["TestCase"]} ( Produces ) {dict["Produces"]} ( ) {Regex.Matches(dict["Produces"], @"\S+").Count}";
                }
                else
                {
                    var compiler = new Compiler();
                    bool success;

                    try
                    {
                        compiler.LoadCore();
                        compiler.LoadCore4th(dict.At("WithCore") ?? "");
                        compiler.ReadFile(0, "TestException", x => 0, y => 0, dict["TestCase"]);
                        compiler.Precompile();
                        compiler.Compile();
                        compiler.PostCompile();
                        success = dict.At("ProducesException").IsEqual("");
                    }
                    catch (Exception ex)
                    {
                        success = dict.At("ProducesException").IsEqual(ex.Message);
                        dict[success ? "ProducesException" : "Exception"] = ex.Message;
                    }

                    yield return "( " + string.Join(" ", dict.Select(kvp => $@"{kvp.Key} : ""{kvp.Value}""")) + $" ) {(success ? 0 : -1)}";
                }
            }
        }

        public void ReadFile(int pos, string file, Func<int, int> y, Func<int, int> x, string input, int macroLevel = 0)
        {
            ReadFile(pos, file, y, x, input.SplitLines(), macroLevel);
        }

        public void ReadFile(int pos, string file, Func<int, int> y, Func<int, int> x, string[] input, int macroLevel = 0)
        {
            var start = Tokens.Count;
            var tokenIndex = _tokenIndex;

            Sources.At(file, () => input);
            Tokens.InsertRange(pos, input.SelectMany(
                (s, i) => Parser.Matches(s)
                                .OfType<Match>()
                                .Select(m => new Token(m.Value, file, y(i), x(m.Index), macroLevel))));

            for (_tokenIndex = pos, _commentIndex = pos + Tokens.Count - start; _tokenIndex < _commentIndex; _tokenIndex++)
            {
                if ((Words.At(Token.Text) as MethodAttribute)?.IsComment == true)
                {
                    Words.At(Token.Text).Process(this);
                }
            }

            _tokenIndex = tokenIndex;
        }

        public void Precompile()
        {
            for (_tokenIndex = 0; _tokenIndex < Tokens.Count; _tokenIndex++)
            {
                if (!Token.IsExcluded && PrecompileWords.ContainsKey(Token.Text))
                {
                    ParseSymbol(PrecompileWords);
                }
            }
        }

        public void Compile(int? from = null)
        {
            for (_tokenIndex = from ?? 0; _tokenIndex < Tokens.Count; _tokenIndex++)
            {
                if (Token.TokenType == TokenType.Literal)
                {
                    _argIndex = _tokenIndex;
                    Encode(Convert.ToInt32(
                        Token.Text.Trim('$', '#', '%'),
                        Token.Text.StartsWith("$") ? 16 : Token.Text.StartsWith("%") ? 2 : 10));
                    _lastToken = Token;
                }
                else if (!Token.IsExcluded)
                {
                    ParseSymbol(Words);
                    _lastToken = Token;
                }
            }

            _structureStack.Validate(ss => $"Missing {ss.Peek().Close}", ss => ss.Count == 1);
        }

        public void PostCompile(int fromCode = 0, int fromComp = 0, int fromToken = 0)
        {
            int i, index, lits = 0;
            var labels = Compilation.Validate(cs => "No code produced", cs => cs.Count > 0)
                                    .Where(cs => cs.Code == Code.Label)
                                    .GroupBy(cs => cs.Label)
                                    .ToDictionary(cs => cs.Key, cs => cs.First(), OrdinalIgnoreCase);

            CodeSlots.SetCount(fromCode);
            foreach (var codeslot in Compilation.Skip(fromComp))
            {
                if (codeslot.Code == Code.Lit || codeslot.Code == Code.Address)
                {
                    lits++;
                }

                if (codeslot.Code != Code.Label)
                {
                    codeslot.CodeIndex = CodeSlots.Count;
                    CodeSlots.Add(codeslot);
                }

                if (codeslot.Code == Code.Label || codeslot == Compilation.Last())
                {
                    CodeSlots.AddRange(Enumerable.Range(0, 8 - CodeSlots.Count % 8).Select(e => (CodeSlot)Code._));
                }

                for (; CodeSlots.Count % 8 == 0 && lits > 0; lits--)
                {
                    CodeSlots.AddRange(Enumerable.Range(0, 8).Select(e => (CodeSlot)null));
                }

                if (codeslot.Code == Code.Label)
                {
                    codeslot.CodeIndex = CodeSlots.Count;
                }
            }

            foreach (var address in Compilation.Skip(fromComp).Where(cs => cs.Code == Code.Address))
            {
                CodeSlots[address.CodeIndex] = labels.At(address.Label).Validate(a => $"Missing label {address.Label}").CodeIndex / 8;
            }

            foreach (var label in labels.Where(l => l.Value.CodeIndex < CodeSlots.Count && CodeSlots[l.Value.CodeIndex] != null))
            {
                CodeSlots[label.Value.CodeIndex].Label = label.Key;
            }

            for (i = Tokens.Count - 1, index = CodeSlots.Count; i >= fromToken; i--)
            {
                Tokens[i].CodeIndex = index = Tokens[i].CodeSlot?.CodeIndex ?? index;
            }

            for (i = Tokens.Count - 1, index = CodeSlots.Count; i >= fromToken; index = Tokens[i--].CodeIndex)
            {
                Tokens[i].CodeCount = index - Tokens[i].CodeIndex;
            }
        }

        public void Optimize(bool enabled)
        {
            var tokenOrig = _tokenIndex = Tokens.Count;
            var compOrig = Compilation.Count;
            var before = new List<CodeSlot[]>();
            var after = new List<CodeSlot[]>();
            var removed = new HashSet<CodeSlot>();
            var count = 0;
            var startTime = DateTime.Now;

            foreach (var optimization in _optimizations.Where(i => enabled))
            {
                try
                {
                    var dict = TextToDict(optimization, "Optimization", "OptimizesTo");
                    var start = Compilation.Count;

                    ReadFile(Tokens.Count, "Optimization", x => 0, y => 0, dict["Optimization"]);
                    Compile(_tokenIndex);

                    var mid = Compilation.Count;

                    ReadFile(Tokens.Count, "Optimization", x => 0, y => 0, dict["OptimizesTo"]);
                    Compile(_tokenIndex);

                    before.Add(Compilation.Skip(start).Take(mid - start).ToArray());
                    after.Add(Compilation.Skip(mid).ToArray());
                }
                catch
                {
                    //
                }
            }

            Tokens.SetCount(tokenOrig);
            Compilation.SetCount(compOrig);

            for (int i = 0; i < Compilation.Count; i++)
            {
                for (int o = 0; o < before.Count; o++)
                {
                    if (Compilation.Skip(i).Take(before[o].Length).SequenceEqual(before[o], before[0][0]))
                    {
                        int common = Min(before[o].Length, after[o].Length);
                        int remove = Max(before[o].Length - after[o].Length, 0);

                        for (int x = 0; x < common; x++)
                        {
                            Compilation[i + x].Code = after[o][x].Code;
                            Compilation[i + x].Value = after[o][x].Value;
                            Compilation[i + x].Label = after[o][x].Label;
                        }

                        removed.UnionWith(Compilation.Skip(i + common).Take(remove));
                        Compilation.RemoveRange(i + common, remove);
                        Compilation.InsertRange(i + common, after[o].Skip(common));
                        count++;
                    }
                }
            }

            Tokens.Where(t => removed.Contains(t.CodeSlot)).ForEach(t => t.CodeSlot = null);
            Console.WriteLine($"{count} optimizations in {DateTime.Now - startTime:g}");
        }

        private static Dictionary<string, string> TextToDict(string text, params string[] keywords)
        {
            var dict = new Dictionary<string, StringBuilder>(OrdinalIgnoreCase);
            var keyword = keywords.First();

            foreach (Match match in Parser.Matches(text))
            {
                if (keywords.Any(k => k.IsEqual(match.Value)))
                {
                    dict[keyword = match.Value] = new StringBuilder();
                }
                else
                {
                    dict[keyword].Append(match.Value);
                }
            }

            return dict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString().Trim().Dequote(), OrdinalIgnoreCase);
        }

        private void ParseWhiteSpace()
        {
            for (_tokenIndex++; _tokenIndex < Tokens.Count && Tokens[_tokenIndex].IsExcluded; _tokenIndex++)
            {
            }
        }

        private void ParseSymbol(Dictionary<string, IDictEntry> dict)
        {
            _argValues.Clear();
            _argIndex = _tokenIndex;

            var dictEntry = dict.At(ArgToken.Text).Validate(de => $"{ArgToken.Text} is not defined");
            var argcount = (dictEntry as MethodAttribute)?.Arguments;

            for (int i = 0; i < argcount; i++)
            {
                _tokenIndex.Validate(ti => $"{ArgToken.Text} expects {argcount} arguments", ti => ti + 1 < Tokens.Count);
                ParseWhiteSpace();
                _argValues.Add(Token.Text);
            }

            dictEntry.Process(this);
        }

        private Cpu Evaluate(Token start)
        {
            start = Tokens.SkipWhile(t => t != start).FirstOrDefault(t => t.CodeSlot != null).Validate(t => "Missing code to evaluate");

            var slot = Compilation.IndexOf(start.CodeSlot);

            if (Compilation.Skip(slot).Any(cs => cs.Code == Code.Label))
            {
                PostCompile();
            }
            else
            {
                CodeSlots.Clear();
                CodeSlots.AddRange(Compilation);
            }

            var cpu = new Cpu(this) { ProgramIndex = CodeSlots.IndexOf(start.CodeSlot) };

            cpu.Run(() => cpu.ProgramIndex >= CodeSlots.Count);
            Compilation.SetCount(slot);
            Tokens.SkipWhile(t => t != start).ForEach(t => t.CodeSlot = null);
            CodeSlots.Clear();

            return cpu;
        }

        public void Macro(string macro)
        {
            ReadFile(_tokenIndex + 1, Token.File, y => Token.Y, y => Token.X, $" {macro}", Token.MacroLevel + 1);
        }

        public void Encode(CodeSlot code)
        {
            ArgToken.CodeSlot = ArgToken.CodeSlot ?? code;
            Compilation.Add(code);
        }

        [Method(Arguments = 1, IsPrecompile = true, Name = nameof(Include))]
        private void IncludePrecompile()
        {
            var filename = _argValues[0].Dequote();

            ReadFile(_tokenIndex + 1, filename, y => y, x => x, File.ReadAllLines(filename));
        }

        [Method(Arguments = 1, IsPrecompile = false)]
        private void Include()
        {
        }

        [Method]
        private void Allot()
        {
            var cpu = Evaluate(_lastToken).Validate(x => "ALLOT expects 1 preceding value", x => x.ForthStack.Count() == 1);

            _heapSize += cpu.ForthStack.First();
        }

        int ParseBlock(int endIndex = int.MaxValue, string endText = null)
        {
            var start = _tokenIndex;

            endIndex = Min(endIndex, Tokens.Count);
            endText = endText ?? $"End{ArgToken.Text}";

            while (_tokenIndex < endIndex && (Token.IsExcluded || !Token.Text.IsEqual(endText)))
            {
                _tokenIndex++;
            }

            if (_tokenIndex >= endIndex)
            {
                _tokenIndex = start - 1;
                throw new Exception($"Missing {endText}");
            }

            return start;
        }

        string ReadBlock(int start, int stop)
        {
            var text = new StringBuilder();

            for (int i = start; i < stop; i++)
            {
                if (i > 0 && (Tokens[i - 1].Y != Tokens[i].Y || Tokens[i - 1].File != Tokens[i].File))
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
            Words.At(_argValues[0].Dequote(), () => new Macro { Text = ReadBlock(ParseBlock() + 1, _tokenIndex) }, true);
        }


        [Method]
        public void Optimization()
        {
            _optimizations.Add(ReadBlock(ParseBlock(), _tokenIndex));
        }

        [Method]
        public void TestCase()
        {
            _testCases.Add(ReadBlock(ParseBlock(), _tokenIndex));
        }

        [Method(Arguments = 2)]
        private void Struct()
        {
            _structureStack.Push(new Structure { Name = _argValues[0].Dequote(), Close = _argValues[1].Dequote(), Suffix = (++_structureSuffix).ToString() });
        }

        [Method(Arguments = 1)]
        private void EndStruct()
        {
            _structureStack.Pop(_argValues[0].Dequote());
        }

        [Method(Arguments = 1)]
        private void Constant()
        {
            var cpu = Evaluate(_lastToken).Validate(x => "CONSTANT expects 1 preceding value", x => x.ForthStack.Count() == 1);

            Token.TokenType = TokenType.Constant;
            Words.At(_argValues[0], () => new Constant { Value = cpu.ForthStack.First() }, true);
        }

        [Method(Arguments = 1)]
        private void Addr()
        {
            var prefix = _argValues[0].Split('.').First();
            var structure = _structureStack.FirstOrDefault(s => s.Name.IsEqual(prefix)).Validate(s => $"Missing {prefix}");

            Encode(new CodeSlot { Code = Code.Address, Label = _argValues[0] + structure.Suffix });
        }

        [Method(Arguments = 1)]
        private void Label()
        {
            var prefix = _argValues[0].Split('.').First();
            var structure = _structureStack.FirstOrDefault(s => s.Name.IsEqual(prefix)).Validate(s => $"Missing {prefix}");

            Encode(new CodeSlot { Code = Code.Label, Label = _argValues[0] + structure.Suffix });
        }

        [Method(Arguments = 1)]
        private void Value()
        {
            Macro($"variable {_argValues[0]} {_argValues[0]} !");
        }

        [Method(Arguments = 1)]
        private void Variable()
        {
            Token.TokenType = TokenType.Variable;
            Words.At(_argValues[0], () => new Variable { HeapAddress = _heapSize++ }, true);
        }

        [Method]
        private void NotImplementedException()
        {
            throw new NotImplementedException();
        }

        [Method(Name = "[")]
        private void CompilerEvalStart()
        {
            _structureStack.Push(new Structure { Name = "[", Close = "]", Value = _tokenIndex });
        }

        [Method(Name = "]")]
        private void CompilerEvalStop()
        {
            var start = Tokens[_structureStack.Pop("[").Value];
            var cpu = Evaluate(start);

            cpu.ForthStack.Reverse().ForEach(v => Encode(v));
        }

        [Method(Name = "(", IsComment = true)]
        private void CommentBracket()
        {
            var pos = _tokenIndex++;

            ParseBlock(_commentIndex, ")");

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
        public void Definition()
        {
            Token.TokenType = TokenType.Definition;
            Words.At(_argValues[0], () => new Definition(), true);

            Macro("Struct Definition \";\"" +
                 $"addr Definition.SKIP /jnz label Global.{Token.Text} _take1_");
        }

        [Method(Name = ";")]
        private void EndDefinition()
        {
            Macro("label Definition.EXIT _R1_ @ _drop1_ /jnz label Definition.SKIP " +
                  "EndStruct Definition");
        }

        [Method(Arguments = 2)]
        public void Prerequisite()
        {
            PrecompileWords.At(_argValues[0].Dequote(), () => new Prerequisite()).References.Add(_argValues[1]);
        }

        public void Prerequisite(string reference)
        {
            if (!Words.ContainsKey($"included {reference}"))
            {
                var macro = (Words.At(reference) as Macro).Validate(x => $"{reference} is not defined as a Macro");
                var count = Tokens.Count;

                Words.Add($"included {reference}", null);
                ReadFile(_prerequisiteIndex, reference, y => y, x => x, macro.Text);

                _prerequisiteIndex += Tokens.Count - count;
                _tokenIndex += Tokens.Count - count;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class MethodAttribute : Attribute, IDictEntry
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

    public class Optimization
    {
        public string Text { get; set; }
        public CodeSlot[] Code { get; set; }
        public CodeSlot[] OptimizesTo { get; set; }
    }
}