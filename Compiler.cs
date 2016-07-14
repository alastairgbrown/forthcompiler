using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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

        public Dictionary<DictType, Dictionary<string, IDictEntry>> Entries { get; } =
            new Dictionary<DictType, Dictionary<string, IDictEntry>> {
                {DictType.PreComp, new Dictionary<string, IDictEntry>(StringComparer.OrdinalIgnoreCase)},
                {DictType.Dict, new Dictionary<string, IDictEntry>(StringComparer.OrdinalIgnoreCase)},
                {DictType.TestCase, new Dictionary<string, IDictEntry>(StringComparer.OrdinalIgnoreCase)},

                {TokenType.Organisation, "_ReturnStackCode_", "VARIABLE _RS_ 32 ALLOT _RS_ _RS_ ! VARIABLE _LOOP_RS_"},
                {TokenType.Organisation, "_MulCode_", @": MulDefinition ( a b -- a*b )
                                               0 _take3_
                                               begin _R1_ @ 0<> while
                                                 _R1_ @ 1 and 0<> if _R2_ @ _R3_ @ + _R3_ ! then
                                                 _R2_ @ _R2_ @ + _R2_ !
                                                 0 _R1_ @ /lsr /pop _R1_ !
                                               repeat
                                               _R3_ @ _drop3_ ;"},
                {TokenType.Stack, "_R1_", "_RS_ @"},
                {TokenType.Stack, "_R2_", "_RS_ @ 1 -"},
                {TokenType.Stack, "_R3_", "_RS_ @ 2 -"},
                {TokenType.Stack, "_R4_", "_RS_ @ 3 -"},
                {TokenType.Stack, "_Take1_", "_RS_ @ 1 + _RS_ ! _R1_ !"},
                {TokenType.Stack, "_Take2_", "_RS_ @ 2 + _RS_ ! _R2_ ! _R1_ !"},
                {TokenType.Stack, "_Take3_", "_RS_ @ 3 + _RS_ ! _R3_ ! _R2_ ! _R1_ !"},
                {TokenType.Stack, "_Take4_", "_RS_ @ 4 + _RS_ ! _R4_ ! _R3_ ! _R2_ ! _R1_ !"},
                {TokenType.Stack, "_Drop1_", "_RS_ @ 1 - _RS_ !"},
                {TokenType.Stack, "_Drop2_", "_RS_ @ 2 - _RS_ !"},
                {TokenType.Stack, "_Drop3_", "_RS_ @ 3 - _RS_ !"},
                {TokenType.Stack, "_Drop4_", "_RS_ @ 4 - _RS_ !"},


                {TokenType.Math, "+", Code.Add, Code.Pop},
                new TestCase("1 1 +", "2"),
                {TokenType.Math, "-", Code.Sub, Code.Pop},
                new TestCase("1 1 -", "0"),
                {TokenType.Math, "*", "MulDefinition"},
                new Prerequisite("_MulCode_", "_ReturnStackCode_"),
                new TestCase("2 5 *","10"),
                new TestCase("10 100 *","1000"),
                {TokenType.Math, "/", nameof(NotImplementedException)},

                {TokenType.Math, "=", Code.Sub, Code.Swp, Code.Zeq, Code.Pop},
                new TestCase("1 1 =", "-1"),
                new TestCase("1 0 =", "0"),
                {TokenType.Math, "<>", Code.Sub, Code.Swp, Code.Zeq, Code.Swp, Code.Zeq, Code.Pop},
                new TestCase("1 1 <>", "0"),
                new TestCase("1 0 <>", "-1"),
                {TokenType.Math, "0=", Code.Psh, Code.Zeq, Code.Pop},
                new TestCase("0 0=", "-1"),
                new TestCase("1 0=", "0"),
                {TokenType.Math, "0<>", Code.Psh, Code.Zeq, Code.Swp, Code.Zeq, Code.Pop},
                new TestCase("0 0<>", "0"),
                new TestCase("1 0<>", "-1"),
                {TokenType.Math, "<", "- drop 0 dup /adc drop 0<>"},
                new TestCase("0 1 <", "-1"),
                new TestCase("1 0 <", "0"),
                new TestCase("1 1 <", "0"),
                {TokenType.Math, ">", "swap <"},
                new TestCase("0 1 >", "0"),
                new TestCase("1 0 >", "-1"),
                new TestCase("1 1 >", "0"),
                {TokenType.Math, "<=", "swap >="},
                new TestCase("0 1 <=", "-1"),
                new TestCase("1 0 <=", "0"),
                new TestCase("1 1 <=", "-1"),
                {TokenType.Math, ">=", "- drop 0 dup /adc drop 0="},
                new TestCase("0 1 >=", "0"),
                new TestCase("1 0 >=", "-1"),
                new TestCase("1 1 >=", "-1"),
                {TokenType.Math, "And", Code.And, Code.Pop},
                new TestCase("127 192 and", "64"),
                {TokenType.Math, "Xor", Code.Xor, Code.Pop},
                new TestCase("127 192 xor", "191"),
                {TokenType.Math, "Or", "-1 xor swap -1 xor and -1 xor"},
                new TestCase("127 192 or", "255"),
                {TokenType.Math, "Invert", "-1 xor"},
                new TestCase("-1 invert", "0"),
                new TestCase("0 invert", "-1"),

                {TokenType.Math, "MOD", nameof(NotImplementedException)},
                {TokenType.Math, "NEGATE", "0 swap -"},
                new TestCase("0 NEGATE", "0"),
                new TestCase("-1 NEGATE", "1"),
                new TestCase("1 NEGATE", "-1"),
                {TokenType.Math, "ABS", "Dup 0 < IF Negate Then"},
                new TestCase("-1 ABS", "1"),
                new TestCase("1 ABS", "1"),
                {TokenType.Math, "MIN", "2Dup > IF Swap Then drop"},
                new TestCase("0 1 MIN", "0"),
                new TestCase("1 0 MIN", "0"),
                new Prerequisite("_ReturnStackCode_"),
                {TokenType.Math, "MAX", "2Dup < IF Swap Then drop"},
                new TestCase("0 1 MAX", "1"),
                new TestCase("1 0 MAX", "1"),
                new Prerequisite("_ReturnStackCode_"),
                {TokenType.Math, "LShift", "_take1_ begin _R1_ @ 0<> while dup + _R1_ @ 1 - _R1_ ! repeat _drop1_"},
                new TestCase("16 4 lshift", "256"),
                new Prerequisite("_ReturnStackCode_"),
                {TokenType.Math, "RShift", "_take1_ begin _R1_ @ 0<> while dup /LSR drop _R1_ @ 1 - _R1_ ! repeat _drop1_"},
                new TestCase("16 4 rshift", "1"),
                new Prerequisite("_ReturnStackCode_"),

                {TokenType.Stack, "DUP", Code.Psh},
                new TestCase("1 DUP", "1 1"),
                {TokenType.Stack, "?DUP", "DUP DUP 0= IF DROP THEN"},
                new TestCase("1 ?DUP", "1 1"),
                new TestCase("0 ?DUP", "0"),
                {TokenType.Stack, "DROP", Code.Pop},
                new TestCase("1 2 DROP", "1"),
                {TokenType.Stack, "SWAP", Code.Swp},
                new TestCase("0 1 SWAP", "1 0"),
                {TokenType.Stack, "OVER", "_Take2_ _R1_ @ _R2_ @ _R1_ @ _drop2_"},
                new TestCase("1 2 OVER", "1 2 1"),
                new Prerequisite("_ReturnStackCode_"),
                {TokenType.Stack, "NIP", Code.Swp, Code.Pop},
                new TestCase("1 2 NIP", "2"),
                {TokenType.Stack, "TUCK", "swap over"},
                new TestCase("1 2 TUCK", "2 1 2"),
                {TokenType.Stack, "ROT", "_take3_ _R2_ @ _R3_ @ _R1_ @ _drop3_"},
                new TestCase("1 2 3 ROT", "2 3 1"),
                {TokenType.Stack, "-ROT", "_take3_ _R3_ @ _R1_ @ _R2_ @ _drop3_"},
                new TestCase("1 2 3 -ROT", "3 1 2"),
                {TokenType.Stack, "PICK", nameof(NotImplementedException)},
                {TokenType.Stack, "2DUP", "_take2_ _R1_ @ _R2_ @ _R1_ @ _R2_ @ _drop2_"},
                new TestCase("1 2 2DUP", "1 2 1 2"),
                new Prerequisite("_ReturnStackCode_"),
                {TokenType.Stack, "2DROP", Code.Pop, Code.Pop},
                new TestCase("1 2 3 4 2DROP", "1 2"),
                {TokenType.Stack, "2SWAP", "_take4_ _R3_ @ _R4_ @ _R1_ @ _R2_ @ _drop4_"},
                new TestCase("1 2 3 4 2SWAP", "3 4 1 2"),
                new Prerequisite("_ReturnStackCode_"),
                {TokenType.Stack, "2OVER", "_take4_ _R1_ @ _R2_ @ _R3_ @ _R4_ @ _R1_ @ _R2_ @ _drop4_"},
                new TestCase("1 2 3 4 2OVER", "1 2 3 4 1 2"),
                new Prerequisite("_ReturnStackCode_"),

                {TokenType.Math, "@", Code.Ldw},
                {TokenType.Math, "!", Code.Stw, Code.Pop, Code.Pop},
                {TokenType.Math, "+!", "dup -rot @ + swap !"},
                new TestCase("Variable Test_+!", ""),
                new TestCase("1 Test_+! ! 1 Test_+! +! Test_+! @", "2"),
                new TestCase("5 Test_+! ! 2 Test_+! +! Test_+! @", "7"),

                {TokenType.Structure, "_Misc_tests_", " "},
                new TestCase("VARIABLE TestVariable 1 TestVariable ! TestVariable @", "1"),
                new TestCase("2 CONSTANT TestConstant TestConstant", "2"),
                new TestCase("3 VALUE TestValue TestValue @", "3"),
                new TestCase("$F $B -", "4"),
                new TestCase("%101", "5"),
                new TestCase("#6", "6"),
                new TestCase("[ 1 1 2 3 + + + ]", "7"),
                new TestCase("1 if 8 else 9 then", "8"),
                new TestCase("0 if 8 else 9 then", "9"),
                new TestCase("5 0 do I loop", "0 1 2 3 4"),
                new TestCase("5 1 do I loop", "1 2 3 4"),
                new TestCase("5 0 do I 2 +loop", "0 2 4"),
                new TestCase("2 0 do 12 10 do J I loop loop", "0 10 0 11 1 10 1 11"),
                new TestCase("5 begin dup 0<> while dup 1 - repeat", "5 4 3 2 1 0"),
                new TestCase("5 begin dup 1 - dup 0= until", "5 4 3 2 1 0"),
                new TestCase("0 case 1 of 10 endof 2 of 20 20 endof endcase", " "),
                new TestCase("1 case 1 of 10 endof 2 of 20 20 endof endcase", "10"),
                new TestCase("2 case 1 of 10 endof 2 of 20 20 endof endcase", "20 20"),
                new TestCase(": def dup + ; 123 def", "246"),
            };

        public Dictionary<string, IDictEntry> Dict => Entries[DictType.Dict];

        public int HeapSize { get; private set; }

        private int _tokenIndex;
        private readonly Stack<Token> _structureStack = new Stack<Token>();

        public Compiler()
        {
            foreach (Code code in Enum.GetValues(typeof(Code)))
            {
                Entries.Add(TokenType.Math, $"/{code}", code);
            }

            foreach (var method in GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<MethodAttribute>() != null))
            {
                var attribute = method.GetCustomAttribute<MethodAttribute>();
                var prerequisite = method.GetCustomAttribute<Prerequisite>();

                attribute.Method = method;
                Entries[attribute.DictType].Add(attribute.Name ?? method.Name, attribute);

                if (prerequisite != null)
                    Entries[DictType.PreComp].Add(attribute.Name ?? method.Name, prerequisite);
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

        public void Macro(string macro)
        {
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
                var blanks = CodeSlots.Skip(CodeSlots.Count - 8).Count(cs => cs.Code == Code.Lit) * 8;
                CodeSlots.AddRange(Enumerable.Range(0, blanks).Select(i => (CodeSlot)null));
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
                        Encode(Code.Psh);
                        Encode(Code.Lit, Convert.ToInt32(
                                            Token.Text.Trim('$','#','%'), 
                                            Token.Text.StartsWith("$") ? 16 : Token.Text.StartsWith("%") ? 2 : 10));
                    }
                    else if (Token.TokenType != TokenType.Excluded)
                    {
                        ParseSymbol(DictType.Dict);
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

        private void ParseSymbol(DictType dictType)
        {
            IDictEntry dictEntry;

            if (!Entries[dictType].TryGetValue(Token.Text, out dictEntry))
            {
                throw new Exception("Undefined symbol - " + Token.Text);
            }

            Token.DictEntry = dictEntry;

            if ((dictEntry as MethodAttribute)?.HasArgument ?? false)
            {
                for (_tokenIndex++; Tokens[_tokenIndex].TokenType == TokenType.Excluded; _tokenIndex++)
                {
                }

                Token.CodeSlot = CodeSlots.Count;
                Token.DictEntry = dictEntry;
            }

            dictEntry.Process(this);
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
        [Method(null, TokenType.TestCase, HasArgument = true)]
        public void TestCase(object dictEntry)
        {
        }

        [Method(nameof(Include), TokenType.Organisation, HasArgument = true, DictType = DictType.PreComp)]
        private void IncludePrecompile(object dictEntry)
        {
            var filename = Token.Text.Trim('"');

            ReadFile(_tokenIndex + 1, filename, y => y, x => x, File.ReadAllLines(filename));
        }

        [Method(null, TokenType.Organisation, HasArgument = true, DictType = DictType.Dict)]
        private void Include(object dictEntry)
        {
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
            Token.DictEntry = MakeDictEntry(Token.Text, () => new VariableEntry {HeapAddress = HeapSize++}, Token.MacroLevel == 0);
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

        [Method(null, TokenType.Structure), Prerequisite("_ReturnStackCode_")]
        private void Do(object dictEntry)
        {
            _structureStack.Push(Token);

            Macro($@"_LOOP_RS_ @ _take3_ _RS_ @ _LOOP_RS_ !
                     label {Token}START
                     _R2_ @ _R1_ @ >= addr {Token}END and /jnz");
        }

        [Method(null, TokenType.Structure)]
        private void Loop(object dictEntry)
        {
            var doToken = _structureStack.Pop(nameof(Do));

            Macro($"1 _R2_ @ + _R2_ ! addr {doToken}START /jnz label {doToken}END _R3_ @ _LOOP_RS_ ! _drop3_");
        }

        [Method("+LOOP", TokenType.Structure)]
        private void PlusLoop(object dictEntry)
        {
            var doToken = _structureStack.Pop(nameof(Do));

            Macro($"_R2_ @ + _R2_ ! addr {doToken}START /jnz label {doToken}END _R3_ @ _LOOP_RS_ ! _drop3_");
        }

        [Method(null, TokenType.Structure)]
        private void I(object dictEntry)
        {
            Macro("_LOOP_RS_ @ 1 - @");
        }

        [Method(null, TokenType.Structure)]
        private void J(object dictEntry)
        {
            Macro("_LOOP_RS_ @ 2 - @ 1 - @");
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
            Macro($"_take1_");
        }

        [Method(null, TokenType.Structure)]
        private void Of(object dictEntry)
        {
            var caseToken = _structureStack.SkipWhile(s => s.MethodName != nameof(Case)).First();

            _structureStack.Push(Token);
            Macro($"_R1_ @ <> addr {Token} and /jnz");
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

            Macro($"label {caseToken} _drop1_");
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

        [Method(":", TokenType.Definition, HasArgument = true), Prerequisite("_ReturnStackCode_")]
        public void DefinitionStart(object dictEntry)
        {
            MakeDictEntry(Token.Text, () => new DefinitionEntry(), true);

            _structureStack.Push(Token);
            Macro($"addr {Token}SKIP /jnz label {Token.Text}LABEL _take1_");
        }

        [Method(";", TokenType.Definition)]
        private void DefinitionEnd(object dictEntry)
        {
            var start = _structureStack.Pop(nameof(DefinitionStart));

            Macro($"_R1_ @ _drop1_ /jnz label {start}SKIP");
        }

        public void Prerequiste(Prerequisite prerequisite)
        {
            foreach (var reference in prerequisite.References.Where(r => !Dict.ContainsKey($"included {r}")))
            {
                Dict.Add($"included {reference}", null);
                Tokens.Insert(0, new Token(reference, reference, 0, 0, 0));
                _tokenIndex++;
            }
        }

        #endregion
    }

    public class MethodAttribute : Attribute, IDictEntry
    {
        public string Name { get; }

        public bool HasArgument { get; set; }

        public void Process(Compiler compiler)
        {
            Method.Invoke(compiler, new object[] { this });
        }

        public TokenType TokenType { get; }

        public MethodInfo Method { get; set; }

        public DictType DictType { get; set; } = DictType.Dict;

        public MethodAttribute(string name, TokenType tokenType)
        {
            Name = name;
            TokenType = tokenType;
        }
    }

    public class VariableEntry : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            compiler.VariableEntry(this);
        }

        public TokenType TokenType => TokenType.Variable;
        public int HeapAddress { get; set; }
    }

    public class ConstantEntry : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            compiler.ConstantEntry(this);
        }
        public TokenType TokenType => TokenType.Constant;
        public int Value { get; set; }
    }

    public class DefinitionEntry : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            compiler.DefinitionEntry(this);
        }
        public TokenType TokenType => TokenType.Definition;
    }

    public class TestCase : IDictEntry
    {
        public void Process(Compiler compiler)
        {
            throw new NotImplementedException();
        }

        public TokenType TokenType => TokenType.Undetermined;
        public string Text { get; }

        public TestCase(string testcode, string expectedresult)
        {
            Text = $"{testcode} ( = ) {expectedresult} ( ) {Regex.Matches(expectedresult, "\\S+").Count}";
        }
    }

    public class Prerequisite : Attribute, IDictEntry
    {
        public void Process(Compiler compiler)
        {
            compiler.Prerequiste(this);
        }
        public TokenType TokenType => TokenType.Undetermined;
        public string[] References { get; }

        public Prerequisite(params string[] references)
        {
            References = references;
        }
    }

    public class CodeSlot
    {
        public Code Code { get; set; }
        public int Value { get; set; }
    }

    public enum DictType
    {
        PreComp,
        Dict,
        TestCase
    }
}