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

        public static void Add(this Dictionary<string, IDictEntry> dict, TokenType tokenType, string name, params Code[] codes)
        {
            dict.Add(name, new MacroCode { TokenType = tokenType, Codes = codes });
        }
        public static void Add(this Dictionary<string, IDictEntry> dict, TokenType tokenType, string name, string macrotext)
        {
            dict.Add(name, new MacroText { TokenType = tokenType, Text = macrotext });
        }
        public static void Add(this Dictionary<string, IDictEntry> dict, string testcode, string expectedvalues)
        {
            var func = dict.Last(d => !(d.Value is TestCase)).Key;
            var count = Regex.Matches(expectedvalues, @"\S+").Count;

            dict.Add($"( {func} ) {testcode} ( = ) {expectedvalues} ( ) {count}", new TestCase());
        }

        public static void Add(this Dictionary<string, IDictEntry> dict, string prerequisite)
        {
            var func = dict.Last(d => !(d.Value is TestCase)).Key;

            dict.Add($"{prerequisite} {func}", new Prerequisite {Text = prerequisite, For = func});
        }
    }
}