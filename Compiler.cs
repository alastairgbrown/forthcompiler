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

        public string Error { get; set; }

        public Dictionary<DictType, Dictionary<string, IDictEntry>> Entries { get; } =
            new Dictionary<DictType, Dictionary<string, IDictEntry>> {
                {DictType.PreComp, new Dictionary<string, IDictEntry>(StringComparer.OrdinalIgnoreCase)},
                {DictType.Dict, new Dictionary<string, IDictEntry>(StringComparer.OrdinalIgnoreCase)},
                {DictType.TestCase, new Dictionary<string, IDictEntry>(StringComparer.OrdinalIgnoreCase)},

                //{TokenType.Stack, "_R1_", "_RS_ @"},
                //{TokenType.Stack, "_R2_", "_RS_ @ 1 -"},
                //{TokenType.Stack, "_R3_", "_RS_ @ 2 -"},
                //{TokenType.Stack, "_R4_", "_RS_ @ 3 -"},
                //{TokenType.Stack, "_Take1_", "_RS_ @ 1 + _RS_ ! _R1_ !"},
                //{TokenType.Stack, "_Take2_", "_RS_ @ 2 + _RS_ ! _R2_ ! _R1_ !"},
                //{TokenType.Stack, "_Take3_", "_RS_ @ 3 + _RS_ ! _R3_ ! _R2_ ! _R1_ !"},
                //{TokenType.Stack, "_Take4_", "_RS_ @ 4 + _RS_ ! _R4_ ! _R3_ ! _R2_ ! _R1_ !"},
                //{TokenType.Stack, "_Drop1_", "_RS_ @ 1 - _RS_ !"},
                //{TokenType.Stack, "_Drop2_", "_RS_ @ 2 - _RS_ !"},
                //{TokenType.Stack, "_Drop3_", "_RS_ @ 3 - _RS_ !"},
                //{TokenType.Stack, "_Drop4_", "_RS_ @ 4 - _RS_ !"},

                //{TokenType.Organisation, "Return Stack Code",
                //@"VARIABLE _RS_ 32 ALLOT _RS_ _RS_ ! 
                //    VARIABLE _LOOP_RS_" },
                //{TokenType.Organisation, "Multiplication Code",
                //@": MulDefinition \ a b -- a*b
                //    0 _take3_ \ _R1_ is factor1, _R1_ is factor2, _R3_ is product
                //    begin _R1_ @ 0<> while
                //        _R1_ @ 1 and 0<> _R2_ @ and _R3_ @ + _R3_ ! \ add to the product
                //        _R2_ @ _R2_ @ + _R2_ ! \ LSL factor2
                //        0 _R1_ @ /lsr /pop _R1_ ! \ LSR factor1
                //    repeat
                //    _R3_ @ _drop3_ ;" },
                //{TokenType.Organisation, "Pick Code",
                //@": PickDefinition \  xu .. x0 u -- xu .. x0 xu
                //    1 + dup _RS_ @ + _RS_ !  _Take1_ \ allocate xu+2 items on the return stack
                //    _R1_ @ 0 do \ suck the data stack into the return stack
                //        _RS_ @ 4 - I - !
                //    loop
                //    _R1_ @ 0 do \ restore the data stack
                //        _RS_ @ 3 - _R1_ @ - I + @
                //    loop
                //    _RS_ @ _R1_ @ - @ \ get the item we want
                //    _RS_ @ _R1_ @ - 1 - _RS_ ! ; \ restore the return stack" },

                //{TokenType.Math, "+", Code.Add, Code.Pop},
                //new TestCase("1 1 +", "2"),
                //{TokenType.Math, "-", Code.Sub, Code.Pop},
                //new TestCase("1 1 -", "0"),
                //{TokenType.Math, "*", "MulDefinition"},
                //new Prerequisite("Return Stack Code", "Multiplication Code"),
                //new TestCase("2 5 *","10"),
                //new TestCase("10 100 *","1000"),
                //{TokenType.Math, "/", nameof(NotImplementedException)},

                //{TokenType.Math, "=", Code.Sub, Code.Swp, Code.Zeq, Code.Pop},
                //new TestCase("1 1 =", "-1"),
                //new TestCase("1 0 =", "0"),
                //{TokenType.Math, "<>", Code.Sub, Code.Swp, Code.Zeq, Code.Swp, Code.Zeq, Code.Pop},
                //new TestCase("1 1 <>", "0"),
                //new TestCase("1 0 <>", "-1"),
                //{TokenType.Math, "0=", Code.Psh, Code.Zeq, Code.Pop},
                //new TestCase("0 0=", "-1"),
                //new TestCase("1 0=", "0"),
                //{TokenType.Math, "0<>", Code.Psh, Code.Zeq, Code.Swp, Code.Zeq, Code.Pop},
                //new TestCase("0 0<>", "0"),
                //new TestCase("1 0<>", "-1"),
                //{TokenType.Math, "<", "- drop 0 dup /adc drop 0<>"},
                //new TestCase("0 1 <", "-1"),
                //new TestCase("1 0 <", "0"),
                //new TestCase("1 1 <", "0"),
                //{TokenType.Math, ">", "swap <"},
                //new TestCase("0 1 >", "0"),
                //new TestCase("1 0 >", "-1"),
                //new TestCase("1 1 >", "0"),
                //{TokenType.Math, "<=", "swap >="},
                //new TestCase("0 1 <=", "-1"),
                //new TestCase("1 0 <=", "0"),
                //new TestCase("1 1 <=", "-1"),
                //{TokenType.Math, ">=", "- drop 0 dup /adc drop 0="},
                //new TestCase("0 1 >=", "0"),
                //new TestCase("1 0 >=", "-1"),
                //new TestCase("1 1 >=", "-1"),
                //{TokenType.Math, "And", Code.And, Code.Pop},
                //new TestCase("127 192 and", "64"),
                //{TokenType.Math, "Xor", Code.Xor, Code.Pop},
                //new TestCase("127 192 xor", "191"),
                //{TokenType.Math, "Or", "-1 xor swap -1 xor and -1 xor"},
                //new TestCase("127 192 or", "255"),
                //{TokenType.Math, "Invert", "-1 xor"},
                //new TestCase("-1 invert", "0"),
                //new TestCase("0 invert", "-1"),

                //{TokenType.Math, "MOD", nameof(NotImplementedException)},
                //{TokenType.Math, "NEGATE", "0 swap -"},
                //new TestCase("0 NEGATE", "0"),
                //new TestCase("-1 NEGATE", "1"),
                //new TestCase("1 NEGATE", "-1"),
                //{TokenType.Math, "ABS", "Dup 0 < IF Negate Then"},
                //new TestCase("-1 ABS", "1"),
                //new TestCase("1 ABS", "1"),
                //{TokenType.Math, "MIN", "2Dup > IF Swap Then drop"},
                //new TestCase("0 1 MIN", "0"),
                //new TestCase("1 0 MIN", "0"),
                //new Prerequisite("Return Stack Code"),
                //{TokenType.Math, "MAX", "2Dup < IF Swap Then drop"},
                //new TestCase("0 1 MAX", "1"),
                //new TestCase("1 0 MAX", "1"),
                //new Prerequisite("Return Stack Code"),
                //{TokenType.Math, "LShift", "_take1_ begin _R1_ @ 0<> while dup + _R1_ @ 1 - _R1_ ! repeat _drop1_"},
                //new TestCase("16 4 lshift", "256"),
                //new Prerequisite("Return Stack Code"),
                //{TokenType.Math, "RShift", "_take1_ begin _R1_ @ 0<> while dup /LSR drop _R1_ @ 1 - _R1_ ! repeat _drop1_"},
                //new TestCase("16 4 rshift", "1"),
                //new Prerequisite("Return Stack Code"),

                //{TokenType.Stack, "DUP", Code.Psh},
                //new TestCase("1 DUP", "1 1"),
                //{TokenType.Stack, "?DUP", "DUP DUP 0= IF DROP THEN"},
                //new TestCase("1 ?DUP", "1 1"),
                //new TestCase("0 ?DUP", "0"),
                //{TokenType.Stack, "DROP", Code.Pop},
                //new TestCase("1 2 DROP", "1"),
                //{TokenType.Stack, "SWAP", Code.Swp},
                //new TestCase("0 1 SWAP", "1 0"),
                //{TokenType.Stack, "OVER", "_Take2_ _R1_ @ _R2_ @ _R1_ @ _drop2_"},
                //new TestCase("1 2 OVER", "1 2 1"),
                //new Prerequisite("Return Stack Code"),
                //{TokenType.Stack, "NIP", Code.Swp, Code.Pop},
                //new TestCase("1 2 NIP", "2"),
                //{TokenType.Stack, "TUCK", "swap over"},
                //new TestCase("1 2 TUCK", "2 1 2"),
                //{TokenType.Stack, "ROT", "_take3_ _R2_ @ _R3_ @ _R1_ @ _drop3_"},
                //new TestCase("1 2 3 ROT", "2 3 1"),
                //{TokenType.Stack, "-ROT", "_take3_ _R3_ @ _R1_ @ _R2_ @ _drop3_"},
                //new TestCase("1 2 3 -ROT", "3 1 2"),
                //{TokenType.Stack, "PICK", "PickDefinition"},
                //new TestCase("11 22 33 44 0 PICK", "11 22 33 44 44"),
                //new TestCase("11 22 33 44 3 PICK", "11 22 33 44 11"),
                //new Prerequisite("Return Stack Code", "Pick Code"),
                //{TokenType.Stack, "2DUP", "_take2_ _R1_ @ _R2_ @ _R1_ @ _R2_ @ _drop2_"},
                //new TestCase("1 2 2DUP", "1 2 1 2"),
                //new Prerequisite("Return Stack Code"),
                //{TokenType.Stack, "2DROP", Code.Pop, Code.Pop},
                //new TestCase("1 2 3 4 2DROP", "1 2"),
                //{TokenType.Stack, "2SWAP", "_take4_ _R3_ @ _R4_ @ _R1_ @ _R2_ @ _drop4_"},
                //new TestCase("1 2 3 4 2SWAP", "3 4 1 2"),
                //new Prerequisite("Return Stack Code"),
                //{TokenType.Stack, "2OVER", "_take4_ _R1_ @ _R2_ @ _R3_ @ _R4_ @ _R1_ @ _R2_ @ _drop4_"},
                //new TestCase("1 2 3 4 2OVER", "1 2 3 4 1 2"),
                //new Prerequisite("Return Stack Code"),

                //{TokenType.Math, "@", Code.Ldw},
                //{TokenType.Math, "!", Code.Stw, Code.Pop, Code.Pop},
                //{TokenType.Math, "+!", "dup -rot @ + swap !"},
                //new TestCase("Variable Test_+!", ""),
                //new TestCase("1 Test_+! ! 1 Test_+! +! Test_+! @", "2"),
                //new TestCase("5 Test_+! ! 2 Test_+! +! Test_+! @", "7"),

                //new TestCase("Variable", "VARIABLE TestVariable 1 TestVariable ! TestVariable @", "1"),
                //new TestCase("CONSTANT","2 CONSTANT TestConstant TestConstant", "2"),
                //new TestCase("CONSTANT","[ 2 20 + ] CONSTANT TestConstant22 TestConstant22", "22"),
                //new TestCase("VALUE","3 VALUE TestValue TestValue @", "3"),
                //new TestCase("$","$F $B -", "4"),
                //new TestCase("%","%101", "5"),
                //new TestCase("#","#6", "6"),
                //new TestCase("[]","[ 2 3 * 1 + ]", "7"),
                //new TestCase("if","1 if 8 else 9 then", "8"),
                //new TestCase("if","0 if 8 else 9 then", "9"),
                //new TestCase("do","5 0 do I loop", "0 1 2 3 4"),
                //new TestCase("do","5 1 do I loop", "1 2 3 4"),
                //new TestCase("do","5 0 do I 2 +loop", "0 2 4"),
                //new TestCase("do","2 0 do 12 10 do J I loop loop", "0 10 0 11 1 10 1 11"),
                //new TestCase("begin","5 begin dup 0<> while dup 1 - repeat", "5 4 3 2 1 0"),
                //new TestCase("begin","5 begin dup 1 - dup 0= until", "5 4 3 2 1 0"),
                //new TestCase("case","0 case 1 of 10 endof 2 of 20 20 endof endcase", " "),
                //new TestCase("case","1 case 1 of 10 endof 2 of 20 20 endof endcase", "10"),
                //new TestCase("case","2 case 1 of 10 endof 2 of 20 20 endof endcase", "20 20"),
                //new TestCase("defintion",": def dup + ; 123 def", "246"),
            };

        public Dictionary<string, IDictEntry> Dict => Entries[DictType.Dict];

        public int HeapSize { get; private set; }

        private int _tokenIndex;
        private readonly Stack<Structure> _structureStack = new Stack<Structure>(new[] { new Structure { Name = "Global" }, });
        private readonly Dictionary<string, Label> _labels = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        private int _prerequisiteIndex;
        private TokenType _macroClass;

        public Compiler()
        {
            foreach (Code code in Enum.GetValues(typeof(Code)))
            {
                Entries.Add(TokenType.Math, $"/{code}", code);
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
                        Encode(Code.Psh, Convert.ToInt32(
                                            Token.Text.Trim('$', '#', '%'),
                                            Token.Text.StartsWith("$") ? 16 : Token.Text.StartsWith("%") ? 2 : 10));
                    }
                    else if (Token.TokenType != TokenType.Excluded)
                    {
                        ParseSymbol(DictType.Dict);
                    }
                }

                foreach (var label in _labels.Where(t => t.Value.Patches != null))
                {
                    throw new Exception("Unpatched label " + label.Key);
                }

            }
            catch (NotImplementedException ex)
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
            var count = Enumerable.Range(0, Math.Max(0,compressed.Length - codes.Length + 1))
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

            cpu.Run(i => cpu.ProgramSlot < CodeSlots.Count);
            CodeSlots.RemoveRange(start.CodeSlot, CodeSlots.Count - start.CodeSlot);
            Tokens.SkipWhile(t => t != start).ToList().ForEach(t => t.CodeSlot = CodeSlots.Count);

            return cpu;
        }

        #region Declarations
        [Method(null, TokenType.TestCase, Arguments = 3)]
        public void TestCase()
        {
            var target = Token.Arguments[0].Text.Trim('"');
            var testcode = Token.Arguments[1].Text.Trim('"');
            var expectedresult = Token.Arguments[2].Text.Trim('"');
            var key = $"( Test Case {target} ) {testcode} ( = ) {expectedresult} ( ) {Regex.Matches(expectedresult, "\\S+").Count}";
            Entries[DictType.TestCase].MakeEntry<IDictEntry,IDictEntry>(key, () => null);
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
            _structureStack.Push(new Structure { Name = Token.Text, Id = _tokenIndex });
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
            var label = _labels.MakeEntry(Token.Text + structure.Id, () => new Label { Patches = new List<int>() });

            Encode(Code.Psh);

            label.Patches?.Add(CodeSlots.Count);

            Encode(label.CodeSlot / 8);
        }

        [Method(null, TokenType.Organisation, Arguments = 1)]
        private void Label()
        {
            var prefix = Token.Text.Split('.').First();
            var structure = _structureStack.First(s => s.Name.IsEqual(prefix));
            var label = _labels.MakeEntry(Token.Text + structure.Id, () => new Label());

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


        #endregion
        #region compilerEval
        [Method("[", TokenType.Math)]
        private void CompilerEvalStart()
        {
            _structureStack.Push(new Structure { Name = nameof(CompilerEvalStart), Id = _tokenIndex });
        }

        [Method("]", TokenType.Math)]
        private void CompilerEvalStop()
        {
            var start = Tokens[_structureStack.Pop(nameof(CompilerEvalStart)).Id];
            var cpu = Evaluate(start);

            foreach (var value in cpu.ForthStack.Reverse())
            {
                Encode(Code.Psh, value);
            }
        }
        #endregion
        //#region Structure
        //[Method(null, TokenType.Structure)]
        //private void If()
        //{
        //    _structureStack.Push(new Structure { Name = Token.MethodName, Id = CodeSlots.Count });

        //    Macro("0= addr if and /jnz");
        //}

        //[Method(null, TokenType.Structure)]
        //private void Else()
        //{
        //    Macro($"addr if.end /jnz label if");

        //}

        //[Method(null, TokenType.Structure)]
        //private void Then()
        //{
        //    Macro("LABEL if LABEL if.end STRUCTEND if");
        //}

        //[Method(null, TokenType.Structure)]
        //private void Exit()
        //{
        //    Macro("ADDR Definition.EXIT @ code /jnz");
        //}

        //[Method(null, TokenType.Structure), Prerequisite("Return Stack Code")]
        //private void Do()
        //{
        //    _structureStack.Push(new Structure { Name = Token.MethodName, Id = CodeSlots.Count });

        //    Macro($@"_LOOP_RS_ @ _take3_ _RS_ @ _LOOP_RS_ !
        //             label do.START
        //             _R2_ @ _R1_ @ >= addr do.END and /jnz");
        //}

        //[Method(null, TokenType.Structure)]
        //private void Loop()
        //{
        //    Macro($@"1 _R2_ @ + _R2_ ! addr do.START /jnz
        //             label do.END _R3_ @ _LOOP_RS_ ! _drop3_ 
        //             STRUCTEND do");
        //}

        //[Method("+LOOP", TokenType.Structure)]
        //private void PlusLoop()
        //{
        //    Macro($@"_R2_ @ + _R2_ ! addr do.START /jnz 
        //             label do.END _R3_ @ _LOOP_RS_ ! _drop3_ 
        //             STRUCTEND do");
        //}

        //[Method(null, TokenType.Structure)]
        //private void I()
        //{
        //    Macro("_LOOP_RS_ @ 1 - @");
        //}

        //[Method(null, TokenType.Structure)]
        //private void J()
        //{
        //    Macro("_LOOP_RS_ @ 2 - @ 1 - @");
        //}


        //[Method(null, TokenType.Structure)]
        //private void Leave()
        //{
        //    Macro($"addr do.END /jnz");
        //}

        //[Method(null, TokenType.Structure)]
        //private void Begin()
        //{
        //    _structureStack.Push(new Structure { Name = Token.MethodName, Id = CodeSlots.Count });
        //    Macro($"label begin");
        //}

        //[Method(null, TokenType.Structure)]
        //private void Again()
        //{
        //    Macro($"addr begin /jnz structend begin");
        //}

        //[Method(null, TokenType.Structure)]
        //private void Until()
        //{
        //    Macro($"0= addr begin and /jnz structend begin");
        //}

        //[Method(null, TokenType.Structure)]
        //private void While()
        //{
        //    _structureStack.Push(new Structure { Name = Token.MethodName, Id = CodeSlots.Count });
        //    Macro($"0= addr while and /jnz");
        //}

        //[Method(null, TokenType.Structure)]
        //private void Repeat()
        //{
        //    Macro($"addr begin /jnz label while structend while structend begin");

        //}

        //[Method(null, TokenType.Structure), Prerequisite("Return Stack Code")]
        //private void Case()
        //{
        //    _structureStack.Push(new Structure { Name = Token.MethodName, Id = CodeSlots.Count });
        //    Macro("_Take1_");
        //}

        //[Method(null, TokenType.Structure)]
        //private void Of()
        //{
        //    _structureStack.Push(new Structure { Name = Token.MethodName, Id = CodeSlots.Count });
        //    Macro($"_R1_ @ <> addr of.END and /jnz");
        //}

        //[Method(null, TokenType.Structure)]
        //private void EndOf()
        //{
        //    Macro($"addr case.END /jnz label of.END structend of");
        //}

        //[Method(null, TokenType.Structure)]
        //private void EndCase()
        //{
        //    Macro($"label case.END _drop1_ structend case");
        //}

        //#endregion
        #region Misc

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

        [Method(".", TokenType.Definition)]
        private void Emit()
        {
            NotImplementedException();
        }

        [Method(":", TokenType.Definition, Arguments = 1)]
        public void DefinitionStart()
        {
            Dict.MakeEntry(Token.Text, () => new DefinitionEntry(), true);

            _structureStack.Push(new Structure { Name = Token.MethodName, Id = CodeSlots.Count });
            Macro($"addr DefinitionStart.SKIP /jnz label Global.{Token.Text} _take1_");
        }

        [Method(";", TokenType.Definition)]
        private void DefinitionEnd()
        {
            Macro($"label DefinitionStart.EXIT _R1_ @ _drop1_ /jnz label DefinitionStart.SKIP structend DefinitionStart");
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

        #endregion
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
            compiler.Macro($"addr Global.{compiler.Token.Text} /CNZ label Global.{compiler.CodeSlots.Count}");
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
        public int Id { get; set; }
    }

    public enum DictType
    {
        PreComp,
        Dict,
        TestCase
    }
}