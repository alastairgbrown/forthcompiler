using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static System.Linq.Enumerable;
using static System.StringComparer;

/*
valid opcodes (5 bit)
1xxxx pfx xxxx
00000 nop = no operation (may not be needed)
00001 ldw = load word : top <=mem(top)
00010 stw = store word : mem(top)<=next
00011 psh = push : mem(sp+1)<=next; next<=top; sp++;
00100 pop = pop : top<=next; next<=mem(sp); sp--;
00101 swp = swap : top <=>next;
00110 jnz = jump top not zero : if (top!=0) { pc=top>>3; slot=top&0x7; } pop;
00111 cnz = call top not zero : if (top!=0) { {pc,slot} <=> top; } else pop;

01000 adc = add with carry : next<=next+top+c; (c modified with result)
01001 add = add : next<=next+top; (c modified)
01010 sub = subtract : next<=next+!top+1; (c modified)
01011 and = and : next<=next&top;
01100 xor = xor : next<=next^top;
01101 lsr = logic shift right : next <= top >> 1; (c modified)
01110 zeq = equal zero : next <= (top == 0) ? 0xffffff... : 0x0;
01111 --- not used (yet)

( forth comment )
variable n
10 n !
begin
	n @ 0<>
while
	1 2 + drop
	n @ 1 - n !
repeat

	psh 0xa {macro}
lbl L0
	pfx n {macro}
	ldw
	neq 0 {macro}
	jpz L0R   {macro}
	psh 1 {macro}
	psh 2 {macro}
	add
	pop
	psh n {macro}
	ldw
	psh 1 {macro}
	sub
	pfx n {macro}
	stw
	jp L0 {macro}
lbl L0R

                    compiler.ReadFile(0, "test", y => y + 1, x => x, new[] {
                    "( forth comment )",
                    "variable n",
                    "[ 5 5 + ] n !",
                    "begin",
                    "    n @ 0<>",
                    "while",
                    "    1 2 + drop",
                    "    n @ 1 - n !",
                    "repeat",
                    "1 0 < drop",
                    "0 1 < drop",
                    "1 1 < drop"});

*/

// ReSharper disable UnusedMember.Local

namespace ForthCompiler
{
    internal class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var argMap = Range(0, args.Length)
                             .Where(i => args[i].StartsWith("-"))
                             .ToDictionary(i => args[i], i => i + 1 < args.Length ? args[i + 1] : null, OrdinalIgnoreCase);
            var compiler = new Compiler();
            var error = (string)null;

            if (!argMap.Any())
            {
                argMap["-testcases"] = argMap["-debug"] = null;
            }

            if (argMap.ContainsKey("-nocatch"))
            {
                Compile(compiler, argMap);
            }
            else
            {
                try
                {
                    Compile(compiler, argMap);
                }
                catch (Exception ex)
                {
                    var pos = compiler.ArgToken;

                    if (pos == null)
                    {
                        error = $"Error: {ex.Message}";
                    }
                    else
                    {
                        var line = compiler.Sources[pos.File][pos.Y];
                        error = $"Error: {ex.Message}{Environment.NewLine}" +
                                $"File:  {pos.File}({pos.Y + 1},{pos.X + 1}){Environment.NewLine}" +
                                $"Line:  {line.Substring(0, pos.X)}<<>>{line.Substring(pos.X)}";

                    }

                    Console.WriteLine(error);
                    compiler.Tokens.SkipWhile(t => t != compiler.ArgToken).ForEach(t => t.TokenType = TokenType.Error);
                }
            }

            if (args.Length == 0)
            {
                var name = Assembly.GetExecutingAssembly().GetName().Name;
                Console.WriteLine();
                Console.WriteLine($"Usage:");
                Console.WriteLine($"   {name} [-f filename | -testcases] [-mif mifFilename] [-debug]");
            }

            if (argMap.ContainsKey("-debug"))
            {
                new DebugWindow(compiler, argMap.ContainsKey("-testcases"), error).ShowDialog();
            }
        }

        public static void Compile(Compiler compiler, Dictionary<string, string> argMap)
        {
            compiler.LoadCore();

            if (argMap.ContainsKey("-f"))
            {
                compiler.ReadFile(0, argMap["-f"], y => y, x => x, File.ReadAllLines(argMap["-f"]));
            }
            else if (argMap.ContainsKey("-testcases"))
            {
                compiler.ReadFile(0, "Test Cases", y => y, x => x, compiler.GenerateTestCases().ToArray());
            }

            compiler.Precompile();
            compiler.Compile();
            compiler.Optimize(!argMap.ContainsKey("-nooptimize"));
            compiler.PostCompile();
            ;

            if (argMap.ContainsKey("-testcases"))
            {
                compiler.CoverageReport();
            }

            if (argMap.ContainsKey("-mif"))
            {
                File.WriteAllLines(argMap["-mif"], compiler.GenerateMif());
                Console.WriteLine($"Generated: {argMap["-mif"]}");
            }
        }
    }

    public enum OpCode
    {
        NativeStart = -1,
        _0,
        _1,
        _2,
        _3,
        _4,
        _5,
        _6,
        _7,
        _8,
        _9,
        _A,
        _B,
        _C,
        _D,
        _E,
        _F,
        Ldw,
        Stw,
        Psh,
        Pop,
        Swp,
        Jnz,
        Jsr,
        Add,
        Adc,
        Sub,
        And,
        Ior,
        Xor,
        Mlt,
        Lsr,
        Zeq,
        NativeStop,
        Literal,
        Label,
        Address,
        Org
    }

    public enum TokenType
    {
        Undetermined,
        Excluded,
        Literal,
        Constant,
        Variable,
        Definition,
        Error,
    }

    public interface ISlotRange
    {
        int CodeIndex { get; }
        int CodeCount { get; }
    }
}
