using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static System.Linq.Enumerable;
using static System.StringComparer;


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
                             .ToDictionary(i => args[i], i => args.Skip(i+1).TakeWhile(a => !a.StartsWith("-")).ToArray(), OrdinalIgnoreCase);
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
                Console.WriteLine($"   {name} [-f filename | -testcases] [-mif mifFilename] [-hex hexFilename] [-debug]");
            }

            if (argMap.ContainsKey("-debug"))
            {
                new DebugWindow(compiler, argMap.ContainsKey("-testcases"), error).ShowDialog();
            }
        }

        public static void Compile(Compiler compiler, Dictionary<string, string[]> argMap)
        {
            compiler.LoadCore();

            if (argMap.At("-f")?.Length == 1)
            {
                compiler.ReadFile(0, argMap["-f"].Single(), y => y, x => x, File.ReadAllLines(argMap["-f"].Single()));
            }
            else if (argMap.ContainsKey("-testcases"))
            {
                compiler.ReadFile(0, "Test Cases", y => y, x => x, compiler.GenerateTestCases().ToArray());
            }

            compiler.PreCompile();
            compiler.Compile();

            if (!argMap.ContainsKey("-nooptimize"))
            {
                compiler.Optimize();
            }

            compiler.PostCompile();

            if (argMap.ContainsKey("-testcases"))
            {
                compiler.GenerateCoverageTestCases();
            }

            if (argMap.At("-mif")?.Length == 1)
            {
                File.WriteAllLines(argMap["-mif"].Single(), compiler.GenerateMif());
                Console.WriteLine($"Generated: {argMap["-mif"].Single()}");
            }

            if (argMap.At("-hex")?.Length == 1)
            {
                File.WriteAllLines(argMap["-hex"].Single(), compiler.GenerateHex());
                Console.WriteLine($"Generated: {argMap["-hex"].Single()}");
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
        long CodeIndex { get; }
        long CodeCount { get; }
    }
}
