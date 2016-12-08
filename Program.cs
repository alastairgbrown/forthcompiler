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
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var argMap = Range(0, args.Length)
                             .Where(i => args[i].StartsWith("-"))
                             .ToDictionary(i => args[i], i => args.Skip(i+1).TakeWhile(a => !a.StartsWith("-")).ToArray(), OrdinalIgnoreCase);
            var compiler = new Compiler();
            var error = (string)null;

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

                    if (pos == null || !compiler.Sources.ContainsKey(pos.File) || compiler.Sources[pos.File].Length <= pos.Y)
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
                Console.WriteLine($"   {name} [-f filename] [-mif mifFilename] [-hex hexFilename] [-debug]");
            }

            if (argMap.ContainsKey("-debug") || argMap.FileName() == null)
            {
                new DebugWindow(compiler, argMap.FileName() == null, error).ShowDialog();
            }
        }

        public static void Compile(Compiler compiler, Dictionary<string, string[]> argMap)
        {
            compiler.LoadCore();

            if (argMap.FileName() == null)
            {
                compiler.ReadFile(0, "Test Cases", y => y, x => x, compiler.GenerateTestCases().ToArray());
            }
            else
            {
                compiler.ReadFile(0, argMap.FileName(), y => y, x => x, File.ReadAllLines(argMap.FileName()));
            }

            compiler.PreCompile();
            compiler.Compile();

            if (!argMap.ContainsKey("-nooptimize"))
            {
                compiler.Optimize();
            }

            compiler.PostCompile();

            if (argMap.FileName() == null)
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

        static string FileName(this Dictionary<string, string[]> argMap)
        {
            return argMap.At("-f")?.Length == 1 ? argMap.At("-f").Single() : null;
        }
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
        String
    }

    public interface ISlotRange
    {
        long CodeIndex { get; }
        long CodeCount { get; }
    }
}
