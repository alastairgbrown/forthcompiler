using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ForthCompiler
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    public class Compiler
    {
        public List<Token> Tokens { get; } = new List<Token>();

        public List<CodeSlot> CodeSlots { get; } = new List<CodeSlot>();

        public Dictionary<string, IDictEntry> Dict { get; } =
            new Dictionary<string, IDictEntry>(StringComparer.OrdinalIgnoreCase)
            {
                {TokenType.Stack, "_Take1_", "__t1 !"},
                {TokenType.Stack, "_Take2_", "__t2 ! __t1 !"},
                {TokenType.Stack, "_Take3_", "__t3 ! __t2 ! __t1 !"},
                {TokenType.Stack, "_Take4_", "__t4 ! __t3 ! __t2 ! __t1 !"},

                {TokenType.Math, "+", Code.Add, Code.Pop},
                {"1 1 +", "2"},
                {TokenType.Math, "-", Code.Sub, Code.Pop},
                {"1 1 -", "0"},
                {TokenType.Math, "*", "_mul_"},
                {"Mul Code"},
                {TokenType.Math, "/", nameof(NotImplementedException)},

                {TokenType.Math, "=", Code.Sub, Code.Swp, Code.Zeq, Code.Pop},
                {"1 1 =", "-1"},
                {"1 0 =", "0"},
                {TokenType.Math, "<>", Code.Sub, Code.Swp, Code.Zeq, Code.Swp, Code.Zeq, Code.Pop},
                {"1 1 <>", "0"},
                {"1 0 <>", "-1"},
                {TokenType.Math, "0=", Code.Psh, Code.Zeq, Code.Pop},
                {"0 0=", "-1"},
                {"1 0=", "0"},
                {TokenType.Math, "0<>", Code.Psh, Code.Zeq, Code.Swp, Code.Zeq, Code.Pop},
                {"0 0<>", "0"},
                {"1 0<>", "-1"},
                {TokenType.Math, "<", "- drop 0 dup /adc drop 0<>"},
                {"0 1 <", "-1"},
                {"1 0 <", "0"},
                {"1 1 <", "0"},
                {TokenType.Math, ">", "swap <"},
                {"0 1 >", "0"},
                {"1 0 >", "-1"},
                {"1 1 >", "0"},
                {TokenType.Math, "<=", "swap >="},
                {"0 1 <=", "-1"},
                {"1 0 <=", "0"},
                {"1 1 <=", "-1"},
                {TokenType.Math, ">=", "- drop 0 dup /adc drop 0="},
                {"0 1 >=", "0"},
                {"1 0 >=", "-1"},
                {"1 1 >=", "-1"},
                {TokenType.Math, "And", Code.And, Code.Pop},
                {"127 192 and", "64"},
                {TokenType.Math, "Xor", Code.Xor, Code.Pop},
                {"127 192 xor", "191"},
                {TokenType.Math, "Or", "-1 xor swap -1 xor and -1 xor"},
                {"127 192 or", "255"},
                {TokenType.Math, "Invert", "-1 xor"},
                {"-1 invert", "0"},
                {"0 invert", "-1"},

                {TokenType.Math, "MOD", nameof(NotImplementedException)},
                {TokenType.Math, "NEGATE", "0 swap -"},
                {"0 NEGATE", "0"},
                {"-1 NEGATE", "1"},
                {"1 NEGATE", "-1"},
                {TokenType.Math, "ABS", "Dup 0 < IF Negate Then"},
                {"-1 ABS", "1"},
                {"1 ABS", "1"},
                {TokenType.Math, "MIN", "2Dup > IF Swap Then drop"},
                {"0 1 MIN", "0"},
                {"1 0 MIN", "0"},
                {TokenType.Math, "MAX", "2Dup < IF Swap Then drop"},
                {"0 1 MAX", "1"},
                {"1 0 MAX", "1"},
                {TokenType.Math, "LShift", "_take1_ begin __t1 @ 0<> while dup + __t1 @ 1 - __t1 ! repeat"},
                {"16 4 lshift", "256"},
                {TokenType.Math, "RShift", "_take1_ begin __t1 @ 0<> while dup /LSR drop __t1 @ 1 - __t1 ! repeat"},
                {"16 4 rshift", "1"},

                {TokenType.Stack, "DUP", Code.Psh},
                {"1 DUP", "1 1"},
                {TokenType.Stack, "?DUP", "DUP DUP 0= IF DROP THEN"},
                {"1 ?DUP", "1 1"},
                {"0 ?DUP", "0"},
                {TokenType.Stack, "DROP", Code.Pop},
                {"1 2 DROP", "1"},
                {TokenType.Stack, "SWAP", Code.Swp},
                {"0 1 SWAP", "1 0"},
                {TokenType.Stack, "OVER", "_Take2_ __t1 @ __t2 @ __t1 @"},
                {"1 2 OVER", "1 2 1"},
                {TokenType.Stack, "NIP", Code.Swp, Code.Pop},
                {"1 2 NIP", "2"},
                {TokenType.Stack, "TUCK", "swap over"},
                {"1 2 TUCK", "2 1 2"},
                {TokenType.Stack, "ROT", "_take3_ __t2 @ __t3 @ __t1 @"},
                {"1 2 3 ROT", "2 3 1"},
                {TokenType.Stack, "-ROT", "_take3_ __t3 @ __t1 @ __t2 @"},
                {"1 2 3 -ROT", "3 1 2"},
                {TokenType.Stack, "PICK", nameof(NotImplementedException)},
                {TokenType.Stack, "2DUP", "_take2_ __t1 @ __t2 @ __t1 @ __t2 @"},
                {"1 2 2DUP", "1 2 1 2"},
                {TokenType.Stack, "2DROP", Code.Pop, Code.Pop},
                {"1 2 3 4 2DROP", "1 2"},
                {TokenType.Stack, "2SWAP", "_take4_ __t3 @ __t4 @ __t1 @ __t2 @"},
                {"1 2 3 4 2SWAP", "3 4 1 2"},
                {TokenType.Stack, "2OVER", "_take4_ __t1 @ __t2 @ __t3 @ __t4 @ __t1 @ __t2 @"},
                {"1 2 3 4 2OVER", "1 2 3 4 1 2"},

                {TokenType.Math, "@", Code.Ldw},
                {TokenType.Math, "!", Code.Stw, Code.Pop, Code.Pop},
                {TokenType.Math, "+!", "dup -rot @ + swap !"},
                {"1 __t4 ! 1 __t4 +! __t4 @", "2"},
                {"5 __t4 ! 2 __t4 +! __t4 @", "7"},

                {TokenType.Structure, "Misc tests", " "},
                {"5 CONSTANT TestConstant TestConstant", "5"},
                {"6 VALUE TestValue TestValue @", "6"},
                {"VARIABLE TestVariable 7 TestVariable ! TestVariable @", "7"},
                {"[ 1 2 3 2 + + + ]", "8"},
                {"1 if 9 else 10 then", "9"},
                {"0 if 9 else 10 then", "10"},
                {"5 0 do I loop", "0 1 2 3 4"},
                {"5 1 do I loop", "1 2 3 4"},
                {"5 0 do I 2 +loop", "0 2 4"},
                {"2 0 do 12 10 do J I loop loop", "0 10 0 11 1 10 1 11"},
                {"5 begin dup 0<> while dup 1 - repeat", "5 4 3 2 1 0"},
                {"5 begin dup 1 - dup 0= until", "5 4 3 2 1 0"},
                {"0 case 1 of 10 endof 2 of 20 20 endof endcase", " "},
                {"1 case 1 of 10 endof 2 of 20 20 endof endcase", "10"},
                {"2 case 1 of 10 endof 2 of 20 20 endof endcase", "20 20"},
                {": def dup + ; 123 def", "246"},
            };

        public Dictionary<string, IDictEntry> Precompile { get; } =
            new Dictionary<string, IDictEntry>(StringComparer.OrdinalIgnoreCase)
            {
                {"Return Stack", new PrecompilationResource {Text = ""}},
                {"Mul Code", new PrecompilationResource {Text = ""}},
            };

        public int HeapSize { get; private set; }

        private int _tokenIndex;
        private readonly Stack<Token> _structureStack = new Stack<Token>();

        public Compiler()
        {
            foreach (Code code in Enum.GetValues(typeof(Code)))
            {
                Dict.Add(TokenType.Math, $"/{code}", code);
            }

            foreach (var method in GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<MethodAttribute>() != null))
            {
                var attr = method.GetCustomAttribute<MethodAttribute>();
                attr.Method = method;

                if (attr is MethodPrecompileAttribute)
                    Precompile.Add(attr.Name ?? method.Name, attr);
                else
                    Dict.Add(attr.Name ?? method.Name, attr);
            }

            foreach (var precompile in Dict.Values.OfType<Prerequisite>())
            {
                Precompile.Add(precompile.For, Precompile[precompile.Text]);
            }
        }

        T MakeDictEntry<T>(string key, Func<T> createFunc, bool exclusive = false) where T : IDictEntry
        {
            IDictEntry entry;

            if (Dict.TryGetValue(key, out entry) && (exclusive || !(entry is T)))
            {
                throw new Exception($"{key} already defined as {entry.GetType().Name}");
            }

            var t = (T)entry;

            if (t == null)
            {
                Dict[key] = t = createFunc();
            }

            return t;
        }

        private void Macro(string macro)
        {
            Regex.Matches(macro, @"\b__\w+\b").OfType<Match>().ToList().ForEach(
                m => MakeDictEntry(m.Value, () => new VariableEntry { HeapAddress = HeapSize++ }));
            ReadFile(_tokenIndex + 1, Token.File, y => Token.Y, y => Token.X, new[] { $" {macro}" }, Token.MacroLevel + 1);
        }

        public Token Token => _tokenIndex < Tokens.Count ? Tokens[_tokenIndex] : null;


        public void Encode(params Code[] codes)
        {
            Array.ForEach(codes, c => Encode(c, 0));
        }

        public void Encode(Code code, int value)
        {
            CodeSlots.Add(new CodeSlot { Code = code, Value = value });

            if (CodeSlots.Count % 8 == 0)
            {
                var wordstart = (CodeSlots.Count - 1) / 8 * 8;
                CodeSlots.AddRange(Enumerable.Range(0, CodeSlots.Skip(wordstart).Count(cs => cs.Code == Code.Lit) * 8).Select(i => (CodeSlot)null));
            }
        }

        public void ReadFile(int pos, string file, Func<int, int> y, Func<int, int> x, string[] input, int macroLevel = 0)
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
                    IDictEntry dictEntry;

                    if (Precompile.TryGetValue(Token.Text, out dictEntry))
                    {
                        ParseArgument(dictEntry);
                    }
                }

                for (_tokenIndex = 0; _tokenIndex < Tokens.Count; _tokenIndex++)
                {
                    Token.CodeSlot = CodeSlots.Count;

                    if (Token.TokenType == TokenType.Literal)
                    {
                        Encode(Code.Psh);
                        Encode(Code.Lit, int.Parse(Token.Text));
                    }
                    else if (Token.TokenType != TokenType.Excluded)
                    {
                        ParseSymbol();
                    }
                }

                foreach (var label in Dict.Values.OfType<LabelEntry>().Where(t => t.Patches != null))
                {
                    throw new Exception("Unpatched label " + Dict.First(d => d.Value == label).Key);
                }

            }
            catch (Exception ex)
            {
                Error = $"Error: {ex.Message}{Environment.NewLine}File: {Token?.File}({Token?.Y + 1},{Token?.X + 1})";
                Console.WriteLine(Error);
                Tokens.Skip(_tokenIndex).ToList().ForEach(t => t.SetError());
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

        public string Error { get; set; }

        private void ParseArgument(IDictEntry dictEntry)
        {
            Token.DictEntry = dictEntry;

            if ((dictEntry as MethodAttribute)?.HasArgument ?? false)
            {
                for (_tokenIndex++; Tokens[_tokenIndex].TokenType == TokenType.Excluded; _tokenIndex++)
                {
                }

                Token.CodeSlot = CodeSlots.Count;
                Token.DictEntry = dictEntry;
            }
        }

        private void ParseSymbol()
        {
            IDictEntry dictEntry;

            if (!Dict.TryGetValue(Token.Text, out dictEntry))
            {
                throw new Exception("Undefined symbol - " + Token.Text);
            }

            ParseArgument(dictEntry);
            dictEntry.Method.Invoke(this, new object[] { dictEntry });
        }

        public void MacroText(MacroText dictEntry)
        {
            Macro(dictEntry.Text);
        }

        public void MacroCode(MacroCode dictEntry)
        {
            Encode(dictEntry.Codes);
        }

        public void DefinitionEntry(DefinitionEntry dictEntry)
        {
            Macro($"addr {Token.Text}LABEL /CNZ label {Token}");
        }

        public void VariableEntry(VariableEntry dictEntry)
        {
            Encode(Code.Psh);
            Encode(Code.Lit, dictEntry.HeapAddress);
        }

        public void ConstantEntry(ConstantEntry dictEntry)
        {
            Encode(Code.Psh);
            Encode(Code.Lit, dictEntry.Value);
        }

        #region Declarations
        [MethodPrecompile(null, TokenType.Organisation, HasArgument = true)]
        private void Include(object dictEntry)
        {
            var filename = Token.Text.Trim('"');

            ReadFile(_tokenIndex + 1, filename, y => y, x => x, File.ReadAllLines(filename));
        }

        private Cpu Evaluate(Token start)
        {
            var cpu = new Cpu(this) { ProgramSlot = start.CodeSlot };

            cpu.Run(i => cpu.ProgramSlot < CodeSlots.Count);
            CodeSlots.RemoveRange(start.CodeSlot, CodeSlots.Count - start.CodeSlot);

            Tokens.SkipWhile(t => t != start).ToList().ForEach(t => t.CodeSlot = CodeSlots.Count);

            return cpu;
        }

        [Method(null, TokenType.Organisation)]
        private void Allot(object dictEntry)
        {
            var cpu = Evaluate(Tokens[_tokenIndex - 2]);

            HeapSize += cpu.ForthStack.First();
        }

        [Method(null, TokenType.Organisation, HasArgument = true)]
        private void Constant(object dictEntry)
        {
            var cpu = Evaluate(Tokens[_tokenIndex - 4]);

            MakeDictEntry(Token.Text, () => new ConstantEntry { Value = cpu.ForthStack.First() }, true);
        }

        [Method(null, TokenType.Organisation, HasArgument = true)]
        private void Addr(object dictEntry)
        {
            var label = MakeDictEntry(Token.Text, () => new LabelEntry { Patches = new List<int>() });

            Encode(Code.Psh);

            label.Patches?.Add(CodeSlots.Count);

            Encode(Code.Lit, label.CodeSlot / 8);
        }

        [Method(null, TokenType.Organisation, HasArgument = true)]
        private void Label(object dictEntry)
        {
            var label = MakeDictEntry(Token.Text, () => new LabelEntry());

            while (CodeSlots.Count % 8 > 0)
            {
                Encode(Code._);
            }

            label.CodeSlot = CodeSlots.Count;

            foreach (var patch in label.Patches ?? Enumerable.Empty<int>())
            {
                CodeSlots[patch].Value = label.CodeSlot / 8;
            }

            label.Patches = null;
        }

        [Method(null, TokenType.Organisation, HasArgument = true)]
        private void Value(object dictEntry)
        {
            Macro($"variable {Token.Text} {Token.Text} !");
        }

        [Method(null, TokenType.Organisation, HasArgument = true)]
        private void Variable(object dictEntry)
        {
            Token.DictEntry = MakeDictEntry(Token.Text, () => new VariableEntry(), Token.MacroLevel == 0);
        }

        [Method(null, TokenType.Organisation)]
        private void NotImplementedException(object dictEntry)
        {
            throw new NotImplementedException();
        }


        #endregion
        #region compilerEval
        [Method("[", TokenType.Math)]
        private void CompilerEvalStart(object dictEntry)
        {
            _structureStack.Push(Token);
        }

        [Method("]", TokenType.Math)]
        private void CompilerEvalStop(object dictEntry)
        {
            var start = _structureStack.Pop(nameof(CompilerEvalStart));
            var cpu = Evaluate(start);

            foreach (var value in cpu.ForthStack.Reverse())
            {
                Encode(Code.Psh);
                Encode(Code.Lit, value);
            }
        }
        #endregion
        #region Structure
        [Method(null, TokenType.Structure)]
        private void If(object dictEntry)
        {
            Macro($"0= addr {Token} and /jnz");
            _structureStack.Push(Token);
        }

        [Method(null, TokenType.Structure)]
        private void Else(object dictEntry)
        {
            var ifToken = _structureStack.Pop(nameof(If));

            _structureStack.Push(Token);

            Macro($"addr {Token} /jnz label {ifToken}");

        }

        [Method(null, TokenType.Structure)]
        private void Then(object dictEntry)
        {
            var ifToken = _structureStack.Pop(nameof(If), nameof(Else));
            Macro($"LABEL {ifToken}");
        }

        [Method(null, TokenType.Structure)]
        private void Exit(object dictEntry)
        {
            var start = _structureStack.SkipWhile(s => s.MethodName != nameof(DefinitionStart)).First();

            Macro($"{start}RA @ code /jnz");
        }

        [Method(null, TokenType.Structure)]
        private void Do(object dictEntry)
        {
            _structureStack.Push(Token);

            Macro($@"_{Token}I ! _{Token}LIM !
                     label {Token}START
                     _{Token}I @ _{Token}LIM @ >= addr {Token}END and /jnz");
        }

        [Method(null, TokenType.Structure)]
        private void Loop(object dictEntry)
        {
            var doToken = _structureStack.Pop(nameof(Do));

            Macro($"1 _{doToken}I @ + _{doToken}I ! addr {doToken}START /jnz label {doToken}END");
        }

        [Method("+LOOP", TokenType.Structure)]
        private void PlusLoop(object dictEntry)
        {
            var doToken = _structureStack.Pop(nameof(Do));

            Macro($"_{doToken}I @ + _{doToken}I ! addr {doToken}START /jnz label {doToken}END");
        }

        [Method(null, TokenType.Structure)]
        private void I(object dictEntry)
        {
            var doToken = _structureStack.SkipWhile(s => s.MethodName != nameof(Do)).First();

            Macro($"_{doToken}I @");
        }

        [Method(null, TokenType.Structure)]
        private void J(object dictEntry)
        {
            var doToken = _structureStack.SkipWhile(s => s.MethodName != nameof(Do)).Skip(1)
                                         .SkipWhile(s => s.MethodName != nameof(Do)).First();

            Macro($"_{doToken}I @");
        }


        [Method(null, TokenType.Structure)]
        private void Leave(object dictEntry)
        {
            var doToken = _structureStack.SkipWhile(s => s.MethodName != nameof(Do)).First();

            Macro($"addr {doToken}END /jnz");
        }

        [Method(null, TokenType.Structure)]
        private void Begin(object dictEntry)
        {
            _structureStack.Push(Token);
            Macro($"label {Token}");
        }

        [Method(null, TokenType.Structure)]
        private void Again(object dictEntry)
        {
            var beginToken = _structureStack.Pop(nameof(Begin));

            Macro($"addr {beginToken} /jnz");
        }

        [Method(null, TokenType.Structure)]
        private void Until(object dictEntry)
        {
            var beginToken = _structureStack.Pop(nameof(Begin));

            Macro($"0= addr {beginToken} and /jnz");
        }

        [Method(null, TokenType.Structure)]
        private void While(object dictEntry)
        {
            _structureStack.Push(Token);
            Macro($"0= addr {Token} and /jnz");
        }

        [Method(null, TokenType.Structure)]
        private void Repeat(object dictEntry)
        {
            var whileToken = _structureStack.Pop(nameof(While));
            var beginToken = _structureStack.Pop(nameof(Begin));

            Macro($"addr {beginToken} /jnz label {whileToken}");

        }

        [Method(null, TokenType.Structure)]
        private void Case(object dictEntry)
        {
            _structureStack.Push(Token);
            Macro($"_{Token} !");
        }

        [Method(null, TokenType.Structure)]
        private void Of(object dictEntry)
        {
            var caseToken = _structureStack.SkipWhile(s => s.MethodName != nameof(Case)).First();

            _structureStack.Push(Token);
            Macro($"_{caseToken} @ <> addr {Token} and /jnz");
        }

        [Method(null, TokenType.Structure)]
        private void EndOf(object dictEntry)
        {
            var caseToken = _structureStack.SkipWhile(s => s.MethodName != nameof(Case)).First();
            var ofToken = _structureStack.Pop(nameof(Of));

            Macro($"addr {caseToken} /jnz label {ofToken}");
        }

        [Method(null, TokenType.Structure)]
        private void EndCase(object dictEntry)
        {
            var caseToken = _structureStack.Pop(nameof(Case));

            Macro($"label {caseToken}");
        }

        #endregion
        #region Misc

        [Method("(", TokenType.Excluded)]
        private void CommentBracket(IDictEntry dictEntry)
        {
            while (Token.Text != ")")
            {
                Tokens[++_tokenIndex].DictEntry = dictEntry;
            }
        }

        [Method("\\", TokenType.Excluded)]
        private void CommentBackSlash(IDictEntry dictEntry)
        {
            var start = Token;
            while (_tokenIndex < Tokens.Count && Tokens[_tokenIndex].File == start.File && Tokens[_tokenIndex].Y == start.Y)
            {
                Tokens[_tokenIndex++].DictEntry = dictEntry;
            }

            _tokenIndex--;
        }

        [Method(".", TokenType.Definition)]
        private void Emit(object dictEntry)
        {
            NotImplementedException(null);
        }

        [Method(":", TokenType.Definition, HasArgument = true)]
        public void DefinitionStart(object dictEntry)
        {
            MakeDictEntry(Token.Text, () => new DefinitionEntry(), true);

            _structureStack.Push(Token);
            Macro($"addr {Token}SKIP /jnz label {Token.Text}LABEL _{Token}RA !");
        }

        [Method(";", TokenType.Definition)]
        private void DefinitionEnd(object dictEntry)
        {
            var start = _structureStack.Pop(nameof(DefinitionStart));

            Macro($"_{start}RA @ /jnz label {start}SKIP");
        }
        #endregion
    }

    public class MethodAttribute : Attribute, IDictEntry
    {
        public string Name { get; }

        public bool HasArgument { get; set; }

        public TokenType TokenType { get; }

        public MethodInfo Method { get; set; }

        public MethodAttribute(string name, TokenType tokenType)
        {
            Name = name;
            TokenType = tokenType;
        }
    }

    public class MethodPrecompileAttribute : MethodAttribute
    {
        public MethodPrecompileAttribute(string name, TokenType tokenType) : base(name, tokenType)
        {
        }
    }

    public class VariableEntry : IDictEntry
    {
        public MethodInfo Method => typeof(Compiler).GetMethod(nameof(Compiler.VariableEntry));
        public TokenType TokenType => TokenType.Variable;
        public int HeapAddress { get; set; }
    }
    public class ConstantEntry : IDictEntry
    {
        public MethodInfo Method => typeof(Compiler).GetMethod(nameof(Compiler.ConstantEntry));
        public TokenType TokenType => TokenType.Constant;
        public int Value { get; set; }
    }

    public class DefinitionEntry : IDictEntry
    {
        public MethodInfo Method => typeof(Compiler).GetMethod(nameof(Compiler.DefinitionEntry));
        public TokenType TokenType => TokenType.Definition;
    }

    public abstract class AuxEntry : IDictEntry
    {
        public MethodInfo Method => null;
        public TokenType TokenType => TokenType.Undetermined;
        public string Text { get; set; }
    }

    public class TestCase : AuxEntry
    {
    }

    public class PrecompilationResource : AuxEntry
    {
    }
    public class Prerequisite : AuxEntry
    {
        public string For { get; set; }
    }

    public class CodeSlot
    {
        public Code Code { get; set; }
        public int Value { get; set; }

        public override string ToString()
        {
            return Code + (Code == Code.Lit ? " " + Value : null);
        }
    }
}