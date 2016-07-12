using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace ForthCompiler
{
    public class Compiler
    {
        public List<Token> Tokens { get; } = new List<Token>();

        public List<CodeSlot> CodeSlots { get; } = new List<CodeSlot>();

        public Dictionary<string, IDictEntry> Dict { get; } = new Dictionary<string, IDictEntry>(StringComparer.OrdinalIgnoreCase)
        {
            {TokenType.Math, "+", Code.Add, Code.Pop},            { "1 1 +", "2" },
            {TokenType.Math, "-", Code.Sub, Code.Pop},            { "1 1 -", "0" },
            {TokenType.Math, "*", nameof(NotImplementedException)},
            {TokenType.Math, "/", nameof(NotImplementedException)},

            {TokenType.Math, "=", Code.Sub, Code.Swp, Code.Zeq, Code.Pop},
                                                                    { "1 1 =", "-1" }, { "1 0 =", "0" },
            {TokenType.Math, "<>", Code.Sub, Code.Swp, Code.Zeq, Code.Swp, Code.Zeq, Code.Pop},
                                                                    { "1 1 <>", "0" }, { "1 0 <>", "-1" },
            {TokenType.Math, "0=", Code.Psh, Code.Zeq, Code.Pop},
                                                                    { "0 0=", "-1" },  { "1 0=", "0" },
            {TokenType.Math, "0<>", Code.Psh, Code.Zeq, Code.Swp, Code.Zeq, Code.Pop},
                                                                    { "0 0<>", "0" },  { "1 0<>", "-1" },
            {TokenType.Math, "<", "- drop 0 dup adc drop 0<>"},     { "0 1 <",  "-1" }, { "1 0 <",  "0"  }, { "1 1 <",  "0" },
            {TokenType.Math, ">", "swap <"},                        { "0 1 >",  "0"  }, { "1 0 >",  "-1" }, { "1 1 >",  "0" },
            {TokenType.Math, "<=", "swap >="},                      { "0 1 <=", "-1" }, { "1 0 <=", "0"  }, { "1 1 <=", "-1" },
            {TokenType.Math, ">=", "- drop 0 dup adc drop 0="},     { "0 1 >=", "0"  }, { "1 0 >=", "-1" }, { "1 1 >=", "-1" },
            {TokenType.Math, "And", Code.And, Code.Pop},            { "127 192 and", "64"  },
            {TokenType.Math, "Xor", Code.Xor, Code.Pop},            { "127 192 xor", "191"  },
            {TokenType.Math, "Or", "-1 xor swap -1 xor and -1 xor"},{ "127 192 or", "255"  },
            {TokenType.Math, "Invert", "-1 xor"},                   { "-1 invert", "0" }, { "0 invert", "-1" },

            {TokenType.Math, "MOD", nameof(NotImplementedException)},
            {TokenType.Math, "NEGATE", "0 swap -"},               { "0 NEGATE", "0" }, { "-1 NEGATE", "1" },{ "1 NEGATE", "-1" },
            {TokenType.Math, "ABS", "Dup 0 < IF Negate Then"},    { "-1 ABS", "1" },{ "1 ABS", "1" },
            {TokenType.Math, "MIN", "2Dup > IF Swap Then drop"},  { "0 1 MIN", "0" },{ "1 0 MIN", "0" },
            {TokenType.Math, "MAX", "2Dup < IF Swap Then drop"},  { "0 1 MAX", "1" },{ "1 0 MAX", "1" },
            {TokenType.Math, "LShift", "__t1 ! begin __t1 @ 0<> while dup + __t1 @ 1 - __t1 ! repeat"},
            {TokenType.Math, "RShift", "__t1 ! begin __t1 @ 0<> while dup LSR drop __t1 @ 1 - __t1 ! repeat"},

            {TokenType.Stack, "_Take1_", "__t1 !"},
            {TokenType.Stack, "_Take2_", "__t2 ! __t1 !"},
            {TokenType.Stack, "_Take3_", "__t3 ! __t2 ! __t1 !"},
            {TokenType.Stack, "_Take4_", "__t4 ! __t3 ! __t2 ! __t1 !"},

            {TokenType.Stack, "DUP", Code.Psh},                     { "1 DUP", "1 1" },
            {TokenType.Stack, "?DUP", "DUP DUP 0= IF DROP THEN"},   { "1 ?DUP", "1 1" }, { "0 ?DUP", "0" },
            {TokenType.Stack, "DROP", Code.Pop},                    { "1 2 DROP", "1" },
            {TokenType.Stack, "SWAP", Code.Swp},                    { "0 1 SWAP", "1 0" },
            {TokenType.Stack, "OVER", "_Take2_ __t1 @ __t2 @ __t1 @"},
                                                                    { "1 2 OVER", "1 2 1" },
            {TokenType.Stack, "NIP", Code.Swp, Code.Pop},           { "1 2 NIP", "2" },
            {TokenType.Stack, "TUCK", "swap over"},                 { "1 2 TUCK", "2 1 2" },
            {TokenType.Stack, "ROT", "_take3_ __t2 @ __t3 @ __t1 @"},
                                                                    { "1 2 3 ROT", "2 3 1" },
            {TokenType.Stack, "-ROT", "_take3_ __t3 @ __t1 @ __t2 @"},
                                                                    { "1 2 3 -ROT", "3 1 2" },
            {TokenType.Stack, "PICK", nameof(NotImplementedException)},
            {TokenType.Stack, "2DUP", "_take2_ __t1 @ __t2 @ __t1 @ __t2 @"},
                                                                    { "1 2 2DUP", "1 2 1 2" },
            {TokenType.Stack, "2DROP", Code.Pop, Code.Pop},
                                                                    { "1 2 3 4 2DROP", "1 2" },
            {TokenType.Stack, "2SWAP", "_take4_ __t3 @ __t4 @ __t1 @ __t2 @"},
                                                                    {"1 2 3 4 2SWAP","3 4 1 2" },
            {TokenType.Stack, "2OVER", "_take4_ __t1 @ __t2 @ __t3 @ __t4 @ __t1 @ __t2 @"},
                                                                    {"1 2 3 4 2OVER","1 2 3 4 1 2" },

            {TokenType.Math, "@", Code.Ldw},
            {TokenType.Math, "!", Code.Stw, Code.Pop, Code.Pop},
            {TokenType.Math, "+!", "dup -rot @ + swap !"},
                                                             { "1 __t4 ! 1 __t4 +! __t4 @", "2" },
                                                             { "5 __t4 ! 1 __t4 +! __t4 @", "6" },
            {TokenType.Structure, "Misc tests", string.Empty},
            {"1 if 2 else 3 then",                            "2" },
            {"0 if 2 else 3 then",                            "3" },
            {"5 0 do I loop",                                 "0 1 2 3 4"},
            {"5 1 do I loop",                                 " 1 2 3 4"},
            {"5 0 do I 2 +loop",                              " 0 2 4"},
            {"5 0 do I i 3 = if leave then loop",             " 0 1 2 3"},
            {"5 begin dup 0<> while dup 1 - repeat",          " 5 4 3 2 1 0"},
            {"5 begin dup 1 - dup 0= until",                  " 5 4 3 2 1 0"},
            {"0 case 1 of 10 endof 2 of 20 20 endof endcase", ""},
            {"1 case 1 of 10 endof 2 of 20 20 endof endcase", "10"},
            {"2 case 1 of 10 endof 2 of 20 20 endof endcase", "20 20"},
        };


        public int HeapSize { get; private set; }

        private int _tokenIndex;
        private readonly Stack<Token> _structureStack = new Stack<Token>();

        public Compiler()
        {
            foreach (var code in Enum.GetValues(typeof(Code)).Cast<Code>().Where(c => !Dict.ContainsKey(c.ToString())))
            {
                Dict.Add(TokenType.Math, code.ToString(), code);
            }

            foreach (var method in GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<MethodAttribute>() != null))
            {
                var attr = method.GetCustomAttribute<MethodAttribute>();
                attr.Method = method;
                Dict.Add(attr.Name ?? method.Name, attr);
            }
        }

        T MakeDictEntry<T>(string key, Func<T> createFunc) where T : IDictEntry
        {
            IDictEntry entry;

            if (Dict.TryGetValue(key, out entry) && !(entry is T))
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

        private VariableEntry GetVariable(string key)
        {
            return MakeDictEntry(key, () => new VariableEntry { HeapAddress = HeapSize++ });
        }

        private void Macro(string macro)
        {
            Regex.Matches(macro, @"\b__\w+\b").OfType<Match>().ToList().ForEach(m => GetVariable(m.Value));
            ReadFile(_tokenIndex + 1, Token.File, y => Token.Y, y => Token.X, new[] { $" {macro}" }, true);
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

        public void ReadFile(int pos, string file, Func<int, int> y, Func<int, int> x, string[] input, bool isMacro = false)
        {
            Tokens.InsertRange(pos, input.SelectMany(
                (s, i) => Regex.Matches(s, @"""([^""]|"""")*""|\S+|\s+")
                    .OfType<Match>()
                    .Select(m => new Token(m.Value, file, y(i), x(m.Index), isMacro))));
        }

        public void Parse()
        {
            try
            {
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

        private void ParseSymbol()
        {
            IDictEntry dictEntry;

            if (!Dict.TryGetValue(Token.Text, out dictEntry))
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
            }

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

        public void Definition(DefinitionBase dictEntry)
        {
            Macro($"ADDR {Token} CNZ");
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
        [Method(null, TokenType.Organisation, HasArgument = true)]
        private void Include(MethodAttribute dictEntry)
        {
            var filename = Token.Text.Trim('"');

            ReadFile(_tokenIndex + 1, filename, y => y, x => x, File.ReadAllLines(filename));
        }

        [Method(null, TokenType.Organisation, HasArgument = true)]
        private void Constant(MethodAttribute dictEntry)
        {
            if (Dict.ContainsKey(Token.Text))
            {
                throw new Exception("Already defined");
            }

            MakeDictEntry(Token.Text, () => new ConstantEntry { Value = Tokens[_tokenIndex - 2].Value });

            //Codes.RemoveRange(Tokens[_tokenIndex - 2].CodeSlot, Codes.Count - (Tokens[_tokenIndex - 2].CodeSlot));
        }

        [Method(null, TokenType.Organisation, HasArgument = true)]
        private void Addr(MethodAttribute dictEntry)
        {
            var label = MakeDictEntry(Token.Text, () => new LabelEntry { Patches = new List<int>() });

            Encode(Code.Psh);

            label.Patches?.Add(CodeSlots.Count);

            Encode(Code.Lit, label.CodeSlot / 8);
        }

        [Method(null, TokenType.Organisation, HasArgument = true)]
        private void Label(MethodAttribute dictEntry)
        {
            var label = MakeDictEntry(Token.Text, () => new LabelEntry());

            Encode(Enumerable.Range(0, 8 - CodeSlots.Count % 8).Select(e => Code._).ToArray());

            label.CodeSlot = CodeSlots.Count;

            for (int i = 0; i < label.Patches?.Count; i++)
            {
                CodeSlots[label.Patches[i]].Value = label.CodeSlot / 8;
            }

            label.Patches = null;
        }

        [Method(null, TokenType.Organisation, HasArgument = true)]
        private void Value(MethodAttribute dictEntry)
        {
            Macro("VARIABLE {Token.Text} {Token.Text} !");
        }

        [Method(null, TokenType.Organisation, HasArgument = true)]
        private void Variable(MethodAttribute dictEntry)
        {
            if (!Token.IsMacro && Dict.ContainsKey(Token.Text))
            {
                throw new Exception("Already defined");
            }

            Token.DictEntry = GetVariable(Token.Text);
        }

        [Method(null, TokenType.Organisation)]
        private void NotImplementedException(MethodAttribute dictEntry)
        {
            throw new NotImplementedException();
        }


        #endregion
        #region compilerEval
        [Method("[", TokenType.Math)]
        private void CompilerEvalStart(MethodAttribute dictEntry)
        {
            _structureStack.Push(Token);
        }

        [Method("]", TokenType.Math)]
        private void CompilerEvalStop(MethodAttribute dictEntry)
        {
            NotImplementedException(null);
            var start = _structureStack.Pop(nameof(CompilerEvalStart));
            var cpu = new Cpu(this) { ProgramSlot = start.CodeSlot };

            cpu.Run(i => cpu.ProgramSlot < CodeSlots.Count);
            CodeSlots.RemoveRange(start.CodeSlot, CodeSlots.Count - start.CodeSlot);
            foreach (var value in cpu.ForthStack.Reverse())
            {
                Encode(Code.Psh);
                Encode(Code.Lit, value);
            }

            Tokens.SkipWhile(t => t != start).Skip(1).ToList().ForEach(t => t.CodeSlot = CodeSlots.Count);
        }
        #endregion
        #region Structure
        [Method(null, TokenType.Structure)]
        private void If(MethodAttribute dictEntry)
        {
            Macro($"0= ADDR {Token} AND JNZ");
            _structureStack.Push(Token);
        }

        [Method(null, TokenType.Structure)]
        private void Else(MethodAttribute dictEntry)
        {
            var ifToken = _structureStack.Pop(nameof(If));

            _structureStack.Push(Token);

            Macro($"ADDR {Token} JNZ LABEL {ifToken}");

        }

        [Method(null, TokenType.Structure)]
        private void Then(MethodAttribute dictEntry)
        {
            var ifToken = _structureStack.Pop(nameof(If), nameof(Else));
            Macro($"LABEL {ifToken}");
        }

        [Method(null, TokenType.Structure)]
        private void Exit(MethodAttribute dictEntry)
        {
            var start = _structureStack.SkipWhile(s => s.MethodName != nameof(DefinitionStart)).First();

            Macro($"{start}RA @ code jnz");
        }

        [Method(null, TokenType.Structure)]
        private void Do(MethodAttribute dictEntry)
        {
            _structureStack.Push(Token);

            Macro($@"_{Token}I ! _{Token}LIM !
                     LABEL {Token}START
                     _{Token}I @ _{Token}LIM @ >= ADDR {Token}END AND JNZ");

        }

        [Method(null, TokenType.Structure)]
        private void Loop(MethodAttribute dictEntry)
        {
            var doToken = _structureStack.Pop(nameof(Do));

            Macro($"1 _{doToken}I +! ADDR {doToken}START jnz LABEL {doToken}END");
        }

        [Method("+LOOP", TokenType.Structure)]
        private void PlusLoop(MethodAttribute dictEntry)
        {
            var doToken = _structureStack.Pop(nameof(Do));

            Macro($"_{doToken}I +! ADDR {doToken}START jnz LABEL {doToken}END");
        }

        [Method(null, TokenType.Structure)]
        private void I(MethodAttribute dictEntry)
        {
            var doToken = _structureStack.SkipWhile(s => s.MethodName != nameof(Do)).First();

            Macro($"_{doToken}I @");
        }


        [Method(null, TokenType.Structure)]
        private void Leave(MethodAttribute dictEntry)
        {
            var doToken = _structureStack.SkipWhile(s => s.MethodName != nameof(Do)).First();

            Macro($"ADDR {doToken}END jnz");
        }

        [Method(null, TokenType.Structure)]
        private void Begin(MethodAttribute dictEntry)
        {
            _structureStack.Push(Token);
            Macro($"label {Token}");
        }

        [Method(null, TokenType.Structure)]
        private void Again(MethodAttribute dictEntry)
        {
            var beginToken = _structureStack.Pop(nameof(Begin));

            Macro($"ADDR {beginToken} Jnz");
        }

        [Method(null, TokenType.Structure)]
        private void Until(MethodAttribute dictEntry)
        {
            var beginToken = _structureStack.Pop(nameof(Begin));

            Macro($"0= ADDR {beginToken} and jnz");
        }

        [Method(null, TokenType.Structure)]
        private void While(MethodAttribute dictEntry)
        {
            _structureStack.Push(Token);
            Macro($"0= ADDR {Token} And Jnz");
        }

        [Method(null, TokenType.Structure)]
        private void Repeat(MethodAttribute dictEntry)
        {
            var whileToken = _structureStack.Pop(nameof(While));
            var beginToken = _structureStack.Pop(nameof(Begin));

            Macro($"ADDR {beginToken} Jnz label {whileToken}");

        }

        [Method(null, TokenType.Structure)]
        private void Case(MethodAttribute dictEntry)
        {
            _structureStack.Push(Token);
            Macro($"_{Token} !");
        }

        [Method(null, TokenType.Structure)]
        private void Of(MethodAttribute dictEntry)
        {
            var caseToken = _structureStack.SkipWhile(s => s.MethodName != nameof(Case)).First();

            _structureStack.Push(Token);
            Macro($"_{caseToken} @ <> ADDR {Token} AND JNZ");
        }

        [Method(null, TokenType.Structure)]
        private void EndOf(MethodAttribute dictEntry)
        {
            var caseToken = _structureStack.SkipWhile(s => s.MethodName != nameof(Case)).First();
            var ofToken = _structureStack.Pop(nameof(Of));

            Macro($"ADDR {caseToken} JNZ LABEL {ofToken}");
        }

        [Method(null, TokenType.Structure)]
        private void EndCase(MethodAttribute dictEntry)
        {
            var caseToken = _structureStack.Pop(nameof(Case));

            Macro($"LABEL {caseToken}");
        }

        #endregion
        #region Misc

        [Method("(", TokenType.Excluded)]
        private void CommentBracket(MethodAttribute dictEntry)
        {
            while (Token.Text != ")")
            {
                Tokens[++_tokenIndex].DictEntry = dictEntry;
            }
        }

        [Method("\\", TokenType.Excluded)]
        private void CommentBackSlash(MethodAttribute dictEntry)
        {
            var start = Token;
            while (_tokenIndex < Tokens.Count && Tokens[_tokenIndex].File == start.File && Tokens[_tokenIndex].Y == start.Y)
            {
                Tokens[_tokenIndex++].DictEntry = dictEntry;
            }

            _tokenIndex--;
        }

        [Method(".", TokenType.Definition)]
        private void Emit(MethodAttribute dictEntry)
        {
            throw new NotImplementedException();
        }

        [Method(":", TokenType.Definition, HasArgument = true)]
        public void DefinitionStart(MethodAttribute dictEntry)
        {
            if (Dict.ContainsKey(Token.Text))
            {
                throw new Exception("Already defined");
            }

            _structureStack.Push(Token);
            Macro($"ADDR {Token}SKIP code jnz LABEL {Token} VARIABLE {Token}RA {Token}RA !");
        }

        [Method(";", TokenType.Definition)]
        private void DefinitionEnd(MethodAttribute dictEntry)
        {
            var start = _structureStack.Pop(nameof(DefinitionStart));

            Macro($"{start}RA @ code jnz LABEL {start}SKIP");
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

    public class TestCase : IDictEntry
    {
        public MethodInfo Method => null;
        public TokenType TokenType => TokenType.TestCase;
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