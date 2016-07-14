using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ForthCompiler
{
    public static class ExtensionMethods
    {
        public static Token Pop(this Stack<Token> stack, params string[] methods)
        {
            if (!stack.Any() || methods.All(n => stack.Peek().MethodName != n))
            {
                throw new Exception("Missing " + methods);
            }

            return stack.Pop();
        }

        public static IEnumerable<Code> ToCodes(this int value)
        {
            var uvalue = unchecked ((uint) value);

            for (int i = 0; i < 8; i++)
            {
                yield return (Code)((uvalue >> (i*4)) & 0xF);
            }
        }

        public static int FromCodes(this IEnumerable<Code> codes)
        {
            return codes.Select((c, i) => ((int) c) << (i*4)).Sum();
        }

        public static bool Contains(this ISlotRange token, int slot)
        {
            return slot >= token.CodeSlot && slot < token.CodeSlot + token.CodeCount;
        }

        public static void Add(this Dictionary<DictType, Dictionary<string, IDictEntry>> dict, TokenType tokenType, string name, params Code[] codes)
        {
            dict[DictType.Dict].Add(name, new MacroCode { TokenType = tokenType, Codes = codes });
        }
        public static void Add(this Dictionary<DictType, Dictionary<string, IDictEntry>> dict, TokenType tokenType, string name, string macrotext)
        {
            dict[DictType.Dict].Add(name, new MacroText { TokenType = tokenType, Text = macrotext });
        }
        public static void Add(this Dictionary<DictType, Dictionary<string, IDictEntry>> dict, TestCase testcase)
        {
            var func = dict[DictType.Dict].Last().Key;

            dict[DictType.TestCase].Add($"{nameof(Compiler.TestCase)} {func} {testcase.Text}", testcase);
        }

        public static void Add(this Dictionary<DictType, Dictionary<string, IDictEntry>> dict, Prerequisite prerequisite)
        {
            var func = dict[DictType.Dict].Last().Key;

            dict[DictType.PreComp].Add(func, prerequisite);
        }
    }
}