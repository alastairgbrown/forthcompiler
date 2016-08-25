using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using static System.Enum;
using static System.Linq.Enumerable;
using static System.Math;
using static System.StringComparer;
// ReSharper disable ExplicitCallerInfoArgument

namespace ForthCompiler
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    public class Compiler : IEqualityComparer<CodeSlot>
    {
        private static readonly Regex Parser = new Regex(@"""([^""]|"""")*""|\S+|\s+", RegexOptions.Compiled);

        public List<Token> Tokens { get; } = new List<Token>();
        public int TokenIndex { get; set; }
        public Token Token => TokenIndex < Tokens.Count ? Tokens[TokenIndex] : null;
        public Token ArgToken => _argIndex < Tokens.Count ? Tokens[_argIndex] : null;
        public List<CodeSlot> Compilation { get; } = new List<CodeSlot>();
        public List<CodeSlot> CodeSlots { get; } = new List<CodeSlot>();
        public Dictionary<string, string> Doc { get; } = new Dictionary<string, string>(OrdinalIgnoreCase);
        public Dictionary<string, IDictEntry> Words { get; private set; } = new Dictionary<string, IDictEntry>(OrdinalIgnoreCase);
        private Dictionary<string, IDictEntry> PrecompileWords { get; set; } = new Dictionary<string, IDictEntry>(OrdinalIgnoreCase);
        public Dictionary<string, int> Coverage { get; private set; } = new Dictionary<string, int>(OrdinalIgnoreCase);
        public Dictionary<string, string[]> Sources { get; } = new Dictionary<string, string[]>(OrdinalIgnoreCase);
        public Stack<Structure> StructureStack { get; } = new Stack<Structure>(new[] { new Structure { Name = string.Empty } });

        private Token _lastToken;
        private List<Optimization> _optimizations = new List<Optimization>();
        private readonly List<Token[]> _testCases = new List<Token[]>();
        private bool _isPrecompilingCompilerCode;
        private int _heapSize;
        private int _argIndex;
        private int _prerequisiteIndex;
        private int _commentIndex;
        private int _structureSuffix;
        private readonly List<string> _argValues = new List<string>();

        public void LoadCore()
        {
            LoadMethodAttributes();
            LoadCodes();
            LoadCore4th();
        }

        private void LoadMethodAttributes()
        {
            foreach (var method in GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                            .Where(m => m.GetCustomAttribute<InternalMethod>() != null))
            {
                var attribute = method.GetCustomAttribute<InternalMethod>();

                attribute.Name = attribute.Name ?? method.Name;
                attribute.Action = (Action)Delegate.CreateDelegate(typeof(Action), this, method);
                (attribute.IsPrecompile ? PrecompileWords : Words)[attribute.Name] = attribute;
                Doc[method.Name] = attribute.Doc ?? Doc.At(method.Name);
            }
        }

        private void LoadCodes()
        {
            foreach (var opcode in GetValues(typeof(OpCode)).OfType<OpCode>()
                                  .Where(c => (int)c > (int)OpCode.NativeStart && (int)c < (int)OpCode.NativeStop))
            {
                Words.Add($"/{opcode}", new RawOpCode { OpCode = opcode });
                Doc[$"/{opcode}"] = $"Inserts raw assembly code for {opcode}";
            }
        }

        public void LoadCore4th(string code = null)
        {
            ReadFile(0, "Core.4th", y => y, x => x, code ?? "Core.4th".LoadText());
            Compile();
            TokenIndex = _prerequisiteIndex = _argIndex = 0;
            _lastToken = null;
            Compilation.Clear();
            Tokens.Clear();
        }

        public IEnumerable<string> GenerateHex()
        {
            return GenerateMif(false);
        }

        public IEnumerable<string> GenerateMif(bool mif = true)
        {
            var depth = (CodeSlots.Count + 5) / 6;

            if (mif)
            {
                yield return $"DEPTH = {depth}; --The size of memory in words";
                yield return "WIDTH = 32; --The size of data in bits";
                yield return "ADDRESS_RADIX = HEX; --The radix for address values";
                yield return "DATA_RADIX = HEX; --The radix for data values";
                yield return "CONTENT-- start of(address: data pairs)";
                yield return "BEGIN";
                yield return "";
            }

            for (var index = 0; index < depth; index++)
            {
                var code = Range(0, Min(6, CodeSlots.Count - index * 6))
                               .Sum(i => (int)CodeSlots[index * 6 + i].OpCode << (i * 5));

                yield return mif ? $"{index:X4} : {code:X8};" : $"{code:X8}";
            }

            if (mif)
            {
                yield return "";
                yield return "END";
            }
        }

        private Compiler GenerateCompiler(IEnumerable<Token> code, IEnumerable<Token> core = null)
        {
            var compiler = new Compiler
            {
                _optimizations = _optimizations,
                Words = Words.ToDictionary(w => w.Key, w => w.Value, OrdinalIgnoreCase),
                PrecompileWords = PrecompileWords.ToDictionary(w => w.Key, w => w.Value, OrdinalIgnoreCase),
                Coverage = Coverage,
            };

            compiler.Words.Values.OfType<Macro>().ForEach(m => m.Prereqs = null);
            compiler.LoadMethodAttributes();
            compiler.LoadCore4th(core?.ToText() ?? "");
            compiler.ReadFile(0, "TestCase", x => 0, y => 0, code.ToText());
            compiler.Precompile();
            compiler.Compile();
            compiler.Optimize(true);
            compiler.PostCompile();
            return compiler;
        }

        public IEnumerable<string> GenerateTestCases()
        {
            var start = DateTime.Now;
            var template = new Compiler { Coverage = Coverage };

            template.LoadCore();

            foreach (var testcase in _testCases)
            {
                var startTC = DateTime.Now;
                var dict = testcase.ToDict("TestCase", "Produces", "ProducesException", "ProducesCode", "ProducesMif", "WithCore");

                if (dict.ContainsKey("Produces"))
                {
                    yield return $"( TestCase ) {dict["TestCase"].ToText()} ( Produces ) {dict["Produces"].ToText()} ( ) {dict["Produces"].Count(t => !t.IsExcluded)}";
                }
                else if (dict.ContainsKey("ProducesException") || dict.ContainsKey("ProducesCode") || dict.ContainsKey("ProducesMif"))
                {
                    var values = dict.ToDictionary(d => d.Key, d => d.Value.ToText());
                    var actual = string.Empty;
                    var required = dict.At("ProducesException")?.ToText() ?? dict.At("ProducesMif")?.ToText();

                    try
                    {
                        var compiler = template.GenerateCompiler(dict["TestCase"], dict.At("WithCore"));

                        if (dict.ContainsKey("ProducesCode"))
                        {
                            actual = string.Join(null, compiler.CodeSlots);
                            required = string.Join(null, template.GenerateCompiler(dict["ProducesCode"]).CodeSlots);
                        }
                        else if (dict.ContainsKey("ProducesMif"))
                        {
                            actual = compiler.GenerateMif().FirstOrDefault(line => line.IsEqual(required)) ?? string.Empty;
                        }
                    }
                    catch (Exception ex)
                    {
                        actual = ex.Message;
                    }

                    if (actual.IsEqual(required))
                    {
                        yield return
                            $"({string.Join("", values.Select(kvp => $@" {kvp.Key} ""{kvp.Value}"""))} {DateTime.Now - startTC} ) 0";
                    }
                    else
                    {
                        values["Actual"] = actual;
                        values["Required"] = required;
                        yield return
                            $"({string.Join("", values.Select(kvp => $@" {kvp.Key} ""{kvp.Value}"""))} {DateTime.Now - startTC} ) -1";
                    }
                }
                else
                {
                    yield return dict["TestCase"].ToText();
                }
            }

            Console.WriteLine($"Generated {_testCases.Count} test cases in {DateTime.Now - start}");
        }

        public IEnumerable<string> GenerateAllSource()
        {
            return Tokens.Where(t => t.MacroLevel == 0)
                         .GroupBy(t => $"{t.File}({t.Y}): ")
                         .Select(g => $"{g.Key} {string.Join(null, g.Select(t => t.Text))}");
        }

        public void ReadFile(int pos, string file, Func<int, int> y, Func<int, int> x, string input, int macroLevel = 0)
        {
            ReadFile(pos, file, y, x, input.Split(new[] { "\r\n", "\r", "\n" }, 0), macroLevel);
        }

        public void ReadFile(int pos, string file, Func<int, int> y, Func<int, int> x, string[] input, int macroLevel = 0)
        {
            var start = Tokens.Count;
            var tokenIndex = TokenIndex;

            Sources.At(file, () => input);
            Tokens.InsertRange(pos, input.SelectMany(
                (s, i) => Parser.Matches(s)
                                .OfType<Match>()
                                .Select(m => new Token(m.Value, file, y(i), x(m.Index), macroLevel))));

            int inserted = Tokens.Count - start;
            for (TokenIndex = pos, _commentIndex = pos + inserted; TokenIndex < _commentIndex; TokenIndex++)
            {
                if ((Words.At(Token.Text) as InternalMethod)?.IsComment == true)
                {
                    Coverage.Increment(Token.Text);
                    Words.At(Token.Text).Process(this);
                }
            }

            TokenIndex = tokenIndex;
        }

        public void Precompile()
        {
            for (TokenIndex = 0; TokenIndex < Tokens.Count; TokenIndex++)
            {
                if (!Token.IsExcluded && PrecompileWords.ContainsKey(Token.Text))
                {
                    ParseSymbol(PrecompileWords);
                }
            }
        }

        public void Compile(int? from = null)
        {
            for (TokenIndex = from ?? 0; TokenIndex < Tokens.Count; TokenIndex++)
            {
                if (Token.TokenType == TokenType.Literal)
                {
                    _argIndex = TokenIndex;
                    Encode(OpCode.Psh);
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

            StructureStack.Validate(ss => $"Missing {ss.Peek().Close}", ss => ss.Count == 1);
        }

        public void PostCompile(int fromCode = 0, int fromComp = 0, int fromToken = 0)
        {
            int i, index;
            var labels = Compilation.Validate(cs => "No code produced", cs => cs.Count > 0)
                                    .Where(cs => cs.OpCode == OpCode.Label)
                                    .GroupBy(cs => cs.Label, OrdinalIgnoreCase)
                                    .ToDictionary(cs => cs.Key, cs => cs.First(), OrdinalIgnoreCase);
            var addressSize = Compilation.Count.ToPfx().Count();

            CodeSlots.SetCount(fromCode);
            foreach (var codeslot in Compilation.Skip(fromComp))
            {
                codeslot.CodeIndex = CodeSlots.Count;

                switch (codeslot.OpCode)
                {
                    case OpCode.Org:
                        var toAdd = (codeslot.Value - CodeSlots.Count).Validate(
                                        x => $"Org value decreasing from {CodeSlots.Count} to {codeslot.Value}", 
                                        x => x >= 0);
                        CodeSlots.AddRange(Range(0, toAdd).Select(x => (CodeSlot)OpCode._0));
                        break;
                    case OpCode.Label:
                        break;
                    case OpCode.Address:
                        CodeSlots.AddRange(0.ToPfx(addressSize).ToArray());
                        break;
                    case OpCode.Literal:
                        CodeSlots.AddRange(codeslot.Value.ToPfx().ToArray());
                        break;
                    default:
                        CodeSlots.Add(codeslot);
                        break;
                }
            }

            foreach (var address in Compilation.Skip(fromComp).Where(cs => cs.OpCode == OpCode.Address))
            {
                address.Value = labels.At(address.Label).Validate(a => $"Missing label {address.Label}").CodeIndex;

                var pfx = address.Value.ToAddressAndSlot().ToPfx(addressSize).ToArray();

                CodeSlots.Replace(address.CodeIndex, addressSize, pfx);
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
            if (!enabled)
                return;

            OptimizeCompile();
            OptimizePeephole(false);
            OptimizePrerequisites();
            OptimizeJumpSequences();
            OptimizeUnreferencedLabels();
            OptimizeUnreachableCode();
            OptimizeUnreferencedLabels();
            OptimizePeephole(true);

            var currentCodeSlots = new HashSet<CodeSlot>(Compilation);

            Tokens.Where(t => !currentCodeSlots.Contains(t.CodeSlot)).ForEach(t => t.CodeSlot = null);
        }

        public void OptimizeCompile()
        {
            var tokenOrig = TokenIndex = Tokens.Count;
            var compOrig = Compilation.Count;

            foreach (var optimization in _optimizations)
            {
                try
                {
                    var dict = optimization.Tokens.ToDict("Optimization", "OptimizesTo", "IsLastPass");
                    var start = Compilation.Count;

                    Tokens.AddRange(dict["Optimization"]);
                    Compile(Tokens.Count - dict["Optimization"].Count);

                    var mid = Compilation.Count;

                    Tokens.AddRange(dict["OptimizesTo"]);
                    Compile(Tokens.Count - dict["OptimizesTo"].Count);

                    optimization.From = Compilation.Skip(start).Take(mid - start).ToArray();
                    optimization.To = Compilation.Skip(mid).ToArray();
                    optimization.IsLastPass = dict.ContainsKey("IsLastPass");
                }
                catch
                {
                    // if it doesn't compile, we can't use this optimization
                }
            }

            Tokens.SetCount(tokenOrig);
            Compilation.SetCount(compOrig);
        }

        [OptimizationMethod]
        public void OptimizePeephole(bool isLastPass)
        {
            var optimizationSets = _optimizations.Where(o => o.From.Length > 0 && o.IsLastPass == isLastPass)
                                                 .GroupBy(o => GetHashCode(o.From[0]))
                                                 .ToDictionary(g => g.Key, g => g.ToArray());

            for (var i = 0; i < Compilation.Count; i++)
            {
                var optimization = optimizationSets.At(GetHashCode(Compilation[i]))?
                                                   .FirstOrDefault(o => Compilation.Skip(i).Take(o.From.Length).SequenceEqual(o.From, this));

                if (optimization != null)
                {
                    Compilation.Replace(i, optimization.From.Length, optimization.To);
                    Coverage.Increment();
                    Coverage.Increment(optimization.Name);
                }
            }
        }

        [OptimizationMethod]
        public void OptimizePrerequisites()
        {
            var removePrereqs = new HashSet<string>(OrdinalIgnoreCase);
            var definitionsByLabel = Words.Values.OfType<Definition>().ToDictionary(d => d.Label, d => d, OrdinalIgnoreCase);
            var referencedDefinitions = new HashSet<IDictEntry>(Compilation.Where(cs => cs.OpCode == OpCode.Address)
                                                                           .Select(cs => definitionsByLabel.At(cs.Label ?? ""))
                                                                           .Where(l => l != null));
            var prereqs = Words.Where(w => (w.Value as Macro)?.Prereqs != null)
                               .ToDictionary(w => w.Key, w => w.Value as Macro, OrdinalIgnoreCase);

            removePrereqs.UnionWith(prereqs.Where(w => w.Value.Prereqs?[true].Count > 0 &&
                                                       w.Value.Prereqs?[false].Count == 0)
                                           .Select(w => w.Key));
            removePrereqs.UnionWith(prereqs.Where(w => w.Value.Prereqs != null)
                                           .Where(w => w.Value.Prereqs[false].All(i => Words[i] is Definition))
                                           .Where(w => w.Value.Prereqs[false].All(i => !referencedDefinitions.Contains(Words[i])))
                                           .Select(w => w.Key));

            foreach (var name in removePrereqs)
            {
                var startLabel = $".{name}.Start";
                var stopLabel = $".{name}.Stop";
                var start = Range(0, Compilation.Count).First(i => Compilation[i].Label.IsEqual(startLabel));
                var stop = Range(start, Compilation.Count).First(i => Compilation[i].Label.IsEqual(stopLabel));

                Compilation.RemoveRange(start, stop - start + 1);
                Coverage.Increment();
            }
        }

        private Dictionary<string, int[]> GetAddresses()
        {
            return new Dictionary<string, int[]>(
                            Range(0, Max(0, Compilation.Count - 1))
                                .Where(i => Compilation[i].OpCode == OpCode.Psh &&
                                            Compilation[i + 1].OpCode == OpCode.Address)
                                .GroupBy(i => Compilation[i + 1].Label, OrdinalIgnoreCase)
                                .ToDictionary(g => g.Key, g => g.ToArray(), OrdinalIgnoreCase))
                    { { ".Placeholder", new int[0] } };
        }

        /// <summary>
        /// Optimizes the following jumps:
        ///     ╭─────╮╭─────╮╭─────╮
        ///     │     ↓│     ↓│     ↓  
        ///   /jnz   /jnz   /jnz
        /// 
        /// to one jump:
        ///     ╭───────────────────╮
        ///     │                   ↓
        ///   /jnz   /jnz   /jnz
        /// </summary>
        [OptimizationMethod]
        public void OptimizeJumpSequences()
        {
            var labels = Range(0, Compilation.Count)
                                   .Where(i => Compilation[i].OpCode == OpCode.Label)
                                   .GroupBy(i => Compilation[i].Label, OrdinalIgnoreCase)
                                   .ToDictionary(g => g.Key, g => g.First(), OrdinalIgnoreCase);

            foreach (var i in GetAddresses().SelectMany(a => a.Value))
            {
                var next = i;
                var links = new HashSet<int>();

                while (next + 2 < Compilation.Count &&
                       Compilation[next].OpCode == OpCode.Psh &&
                       Compilation[next + 1].OpCode == OpCode.Address &&
                       Compilation[next + 2].OpCode == OpCode.Jnz &&
                       labels.ContainsKey(Compilation[next + 1].Label) &&
                       !links.Contains(next))
                {
                    Compilation[i + 1].Label = Compilation[next + 1].Label;
                    next = labels[Compilation[next + 1].Label];
                    next += Compilation.Skip(next).TakeWhile(x => x.OpCode == OpCode.Label).Count();
                    links.Add(next);
                }

                if (next > i)
                {
                    Coverage.Increment();
                }
            }

        }

        [OptimizationMethod]
        public void OptimizeUnreferencedLabels()
        {
            var addresses = GetAddresses();

            foreach (var i in Range(0, Compilation.Count)
                              .Where(i => Compilation[i].OpCode == OpCode.Label && !addresses.ContainsKey(Compilation[i].Label))
                              .Reverse())
            {
                Compilation.RemoveAt(i);
                Coverage.Increment();
            }
        }

        [OptimizationMethod]
        public void OptimizeUnreachableCode()
        {
            foreach (var section in Range(2, Max(0, Compilation.Count - 2))
                                        .Where(i => Compilation[i - 2].OpCode != OpCode.And &&
                                                    Compilation[i - 1].OpCode == OpCode.Jnz)
                                        .ToDictionary(i => i, i => Compilation.Skip(i).TakeWhile(cs => cs.OpCode != OpCode.Label).Count())
                                        .Where(s => s.Value > 0)
                                        .OrderByDescending(s => s.Key))
            {
                Compilation.RemoveRange(section.Key, section.Value);
                Coverage.Increment();
            }
        }

        public void CoverageReport()
        {
            var optimizMethods = GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                     .Where(m => m.GetCustomAttribute<OptimizationMethod>() != null);
            var items = Words.Select(x => new { Name = x.Key, Refs = Coverage.At(x.Key), Type = x.Value.GetType() }).ToList();

            items.AddRange(_optimizations.Select(x => new { x.Name, Refs = Coverage.At(x.Name), Type = x.GetType()}));
            items.AddRange(optimizMethods.Select(x => new { x.Name, Refs = Coverage.At(x.Name), Type = typeof(OptimizationMethod) }));

            foreach (var type in items.GroupBy(i => i.Type.Name))
            {
                Console.WriteLine($"{type.Key}");
                type.OrderBy(i => i.Refs).ThenBy(i => i.Name).ForEach(i => Console.WriteLine($"  {i.Refs} refs to {i.Name}"));
            }

            var tokenOrig = Tokens.Count;
            var compOrig = Compilation.Count;
            var codeOrig = CodeSlots.Count;
            var report = items.Where(i => !Regex.IsMatch(i.Name, "^/_[0-9A-F]$")).GroupBy(i => i.Type.Name).OrderBy(g => g.Key).Select(g =>
                            $"( TestCase {g.Count()} {g.Key}s ) " +
                            $"{g.Count(i => i.Refs > 0)} ( referenced ) " +
                            $"{g.Count(i => i.Refs == 0)} ( unreferenced ) nip 0 1");

            ReadFile(tokenOrig, "Coverage", x => x, y => y, report.ToArray());
            Compile(tokenOrig);
            PostCompile(codeOrig, compOrig, tokenOrig);
        }

        public void ParseWhiteSpace(Predicate<Token> whitespaceTest = null)
        {
            whitespaceTest = whitespaceTest ?? (t => t.IsExcluded);

            for (TokenIndex++; TokenIndex < Tokens.Count && whitespaceTest(Tokens[TokenIndex]); TokenIndex++)
            {
            }
        }

        private void ParseSymbol(IDictionary<string, IDictEntry> dict)
        {
            _argValues.Clear();
            _argIndex = TokenIndex;

            var dictEntry = dict.At(ArgToken.Text).Validate(de => $"{ArgToken.Text} is not defined");
            var argcount = (dictEntry as InternalMethod)?.Arguments;

            for (int i = 0; i < argcount; i++)
            {
                TokenIndex.Validate(ti => $"{ArgToken.Text} expects {argcount} arguments", ti => ti + 1 < Tokens.Count);
                ParseWhiteSpace();
                _argValues.Add(Token.Text);
            }

            Coverage.Increment(ArgToken.Text);
            dictEntry.Process(this);
        }

        private IEnumerable<Token> ParseBlock(int endIndex = int.MaxValue, string endText = null)
        {
            var start = TokenIndex;

            endIndex = Min(endIndex, Tokens.Count);
            endText = endText ?? $"End{ArgToken.Text}";

            while (TokenIndex < endIndex)
            {
                if (Token.IsEqual(endText))
                    yield break;

                yield return Tokens[TokenIndex++];
            }

            TokenIndex = start;
            throw new Exception($"Missing {endText}");
        }

        private Cpu Evaluate(Token start)
        {
            start = Tokens.SkipWhile(t => t != start).FirstOrDefault(t => t.CodeSlot != null).Validate(t => "Missing code to evaluate");

            var slot = Compilation.IndexOf(start.CodeSlot);
            var cpu = new Cpu(Compilation) { ProgramIndex = slot };

            cpu.Run(() => cpu.ProgramIndex >= Compilation.Count);
            Compilation.SetCount(slot);
            Tokens.SkipWhile(t => t != start).ForEach(t => t.CodeSlot = null);

            return cpu;
        }

        public void Macro(string macro)
        {
            ReadFile(TokenIndex + 1, Token.File, y => Token.Y, y => Token.X, $" {macro}", Token.MacroLevel + 1);
        }

        public void Encode(CodeSlot code)
        {
            ArgToken.CodeSlot = ArgToken.CodeSlot ?? code;
            Compilation.Add(code);
        }

        [InternalMethod(Name = nameof(Include), Arguments = 1, IsPrecompile = true)]
        private void IncludePrecompile()
        {
            var filename = _argValues[0].Dequote();

            ReadFile(TokenIndex + 1, filename, y => y, x => x, filename.LoadText());
        }

        [InternalMethod(Arguments = 1, Doc = "Includes named file")]
        private void Include()
        {
        }

        [InternalMethod(Name = nameof(Org), IsPrecompile = true)]
        private void OrgPrecompile()
        {
            _prerequisiteIndex = TokenIndex + 1;
        }

        [InternalMethod(Doc = "Sets program address")]
        private void Org()
        {
            var cpu = Evaluate(_lastToken).Validate(x => "ORG expects 1 preceding value", x => x.ForthStack.Count() == 1);

            Encode(new CodeSlot {OpCode = OpCode.Org, Value = cpu.ForthStack.First() });
        }

        [InternalMethod(Doc = "Allots X bytes to heap at compile time")]
        private void Allot()
        {
            var cpu = Evaluate(_lastToken).Validate(x => "ALLOT expects 1 preceding value", x => x.ForthStack.Count() == 1);

            _heapSize += cpu.ForthStack.First();
        }

        [InternalMethod(Arguments = 1, Doc = "Defines macro - usage: MACRO MacroName MacroText ENDMACRO")]
        private void Macro()
        {
            var tokens = ParseBlock().Skip(1).ToArray();
            var redefine = tokens.Any(ForthCompiler.Macro.IsRedefine);
            var macro = Words.At(_argValues[0].Dequote(), () => new Macro(), !redefine);

            macro.Tokens = tokens;
            Doc[_argValues[0].Dequote()] = tokens.ToDoc();
        }


        [InternalMethod(Doc = "Defines peephole Optimization - usage: OPTIMIZATION UnoptimisedCode OPTIMIZESTO OptimisedCode [ ISLASTPASS ] OPTIMIZATION")]
        public void Optimization()
        {
            _optimizations.Add(new Optimization { Tokens = ParseBlock().ToArray() });
        }

        [InternalMethod(Doc =
            "Defines TestCase - usage: TESTCASE TestCode                       PRODUCES          ExpectedResult    ENDTESTCASE\r\n" +
            "                       or TESTCASE TestCode [ WITHCORE CoreCode ] PRODUCESEXCEPTION ExpectedException ENDTESTCASE\r\n" +
            "                       or TESTCASE TestCode                       PRODUCESCODE      ExpectedCode      ENDTESTCASE\r\n" +
            "                       or TESTCASE TestCode                       PRODUCESMIF       ExpectedMifLine   ENDTESTCASE\r\n" +
            "                       or TESTCASE TestCode                                                           ENDTESTCASE")]
        public void TestCase()
        {
            _testCases.Add(ParseBlock().ToArray());
        }

        [InternalMethod(Arguments = 2, Doc = "Opens Label and Addr scope - usage: STRUCT ScopeName EndScopeHint")]
        private void Struct()
        {
            StructureStack.Push(new Structure
            {
                Name = _argValues[0].Dequote(),
                Close = _argValues[1].Dequote(),
                Suffix = $"{++_structureSuffix}"
            });

            if (Words.Last().Value is Definition)
            {
                StructureStack.Peek().Definition = Words.Last().Key;
                StructureStack.Peek().Value = TokenIndex;
            }
        }

        [InternalMethod(Arguments = 1, Doc = "Closes Label and Addr scope - usage: ENDSTRUCT ScopeName")]
        private void EndStruct()
        {
            var struc = StructureStack.Pop(_argValues[0].Dequote());

            if (struc.Definition != null)
            {
                Doc[struc.Definition] = Tokens.Skip(struc.Value).Take(TokenIndex - struc.Value).ToDoc();
            }
        }

        [InternalMethod(Arguments = 1, Doc = "Defines Constant - usage: MacroValue CONSTANT MacroName")]
        private void Constant()
        {
            var cpu = Evaluate(_lastToken).Validate(x => "CONSTANT expects 1 preceding value", x => x.ForthStack.Count() == 1);
            var start = TokenIndex;

            Token.TokenType = TokenType.Constant;
            Words.At(_argValues[0], () => new Constant { Value = cpu.ForthStack.First() }, true);

            ParseWhiteSpace(t => t.IsExcluded && !t.IsDocumentation);
            if (Token.IsDocumentation)
            {
                Doc[_argValues[0]] = Tokens.Skip(start).ToDoc();
            }
            TokenIndex = start;
        }

        [InternalMethod(Arguments = 1, Doc = "References Label - usage: ADDR ScopeName.Label")]
        private void Addr()
        {
            var prefix = _argValues[0].Split('.').First();
            var structure = StructureStack.FirstOrDefault(s => s.Name.IsEqual(prefix)).Validate(s => $"Missing {prefix}");

            Encode(OpCode.Psh);
            Encode(new CodeSlot { OpCode = OpCode.Address, Label = _argValues[0] + structure.Suffix });
        }

        [InternalMethod(Arguments = 1, Doc = "Defines Label - usage: LABEL ScopeName.Label")]
        private void Label()
        {
            var prefix = _argValues[0].Split('.').First();
            var structure = StructureStack.FirstOrDefault(s => s.Name.IsEqual(prefix)).Validate(s => $"Missing {prefix}");

            Encode(new CodeSlot { OpCode = OpCode.Label, Label = _argValues[0] + structure.Suffix });
        }

        [InternalMethod(Arguments = 1, Doc = "Defines Variable with value - usage: Value VALUE VariableName")]
        private void Value()
        {
            Macro($"variable {_argValues[0]} {_argValues[0]} !");
        }

        [InternalMethod(Arguments = 1, Doc = "Defines Variable - usage: VARIABLE VariableName")]
        private void Variable()
        {
            var start = TokenIndex;

            Token.TokenType = TokenType.Variable;
            Words.At(_argValues[0], () => new Variable { HeapAddress = _heapSize++ }, true);

            ParseWhiteSpace(t => t.IsExcluded && !t.IsDocumentation);
            if (Token.IsDocumentation)
            {
                Doc[_argValues[0]] = Tokens.Skip(start).ToDoc();
            }
            TokenIndex = start;
        }

        [InternalMethod(Name = "[", IsPrecompile = true)]
        private void CompilerEvalStartPrecompile()
        {
            _isPrecompilingCompilerCode = true;
        }

        [InternalMethod(Name = "]", IsPrecompile = true)]
        private void CompilerEvalStopPrecompile()
        {
            _isPrecompilingCompilerCode = false;
        }

        [InternalMethod(Name = "[", Doc = "Marks the start of Compiler evaluation")]
        private void CompilerEvalStart()
        {
            StructureStack.Push(new Structure { Name = "[", Close = "]", Value = TokenIndex });
        }

        [InternalMethod(Name = "]", Doc = "Marks the end of Compiler evaluation")]
        private void CompilerEvalStop()
        {
            var start = Tokens[StructureStack.Pop("[").Value];
            var cpu = Evaluate(start);

            cpu.ForthStack.Reverse().ForEach(v =>
            {
                Encode(OpCode.Psh);
                Encode(v);
            });
        }

        [InternalMethod(Name = "(", IsComment = true, Doc = "Defines a comment - usage ( comment text )")]
        private void CommentBracket()
        {
            ParseBlock(_commentIndex, ")").ForEach(t => t.TokenType = TokenType.Excluded);

            Token.TokenType = TokenType.Excluded;
        }

        [InternalMethod(Name = "\\", IsComment = true, Doc = @"Defines a line comment - usage \ comment text <end-of-line>")]
        private void CommentBackSlash()
        {
            var start = Token;
            var comment = Tokens.Skip(TokenIndex).TakeWhile(t => t.File == start.File && t.Y == start.Y).ToList();

            comment.ForEach(t => t.TokenType = TokenType.Excluded);

            TokenIndex += comment.Count - 1;
        }

        [InternalMethod(Arguments = 2, Doc = "Defines prerequisite code - usage : PREREQUISITE Word MacroName")]
        public void Prerequisite()
        {
            PrecompileWords.At(_argValues[0].Dequote(), () => new Prerequisite()).References.Add(_argValues[1]);
        }

        public void Prerequisite(string name)
        {
            var macro = (Words.At(name) as Macro).Validate(x => $"{name} is not defined as a Macro");

            if (macro.Prereqs == null)
            {
                var tokens = new[] {
                    macro.Tokens.First().Clone(" ", 0),
                    macro.Tokens.First().Clone(nameof(Label), 1),
                    macro.Tokens.First().Clone($".{name}.Start", 1)
                    }.Concat(macro.Tokens).Concat(new[] {
                    macro.Tokens.Last().Clone(nameof(Label), 1),
                    macro.Tokens.Last().Clone($".{name}.Stop", 1)
                }).ToArray();

                Coverage.Increment(name);
                Tokens.InsertRange(_prerequisiteIndex, tokens);
                _prerequisiteIndex += tokens.Length;
                TokenIndex += tokens.Length;
                macro.Prereqs = new Dictionary<bool, List<string>> { { false, new List<string>() }, { true, new List<string>() } };
            }

            macro.Prereqs[_isPrecompilingCompilerCode].Add(Token.Text);
        }

        public bool Equals(CodeSlot x, CodeSlot y)
        {
            return x?.OpCode == y?.OpCode && x?.Value == y?.Value && x?.Label == y?.Label;
        }

        public int GetHashCode(CodeSlot obj)
        {
            return obj.OpCode.GetHashCode() ^ obj.Value.GetHashCode() ^ (obj.Label?.GetHashCode() ?? 0);
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class OptimizationMethod : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class InternalMethod : Attribute, IDictEntry
    {
        public string Name { get; set; }

        public Action Action { get; set; }

        public int Arguments { get; set; }

        public bool IsComment { get; set; }

        public bool IsPrecompile { get; set; }

        public string Doc { get; set; }

        public void Process(Compiler compiler)
        {
            Action();
        }
    }

    public class Optimization
    {
        public override string ToString()
        {
            return Tokens.ToText();
        }

        public string Name => Tokens.ToText();
        public Token[] Tokens { get; set; }
        public CodeSlot[] From { get; set; } = { };
        public CodeSlot[] To { get; set; } = { };
        public bool IsLastPass { get; set; }
    }
}