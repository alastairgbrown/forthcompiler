using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ForthCompiler
{
    public static class ExtensionMethods
    {
        public static bool IsEqual(this string a, string b)
        {
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase) == 0;
        }

        public static T MakeEntry<TD,T>(this Dictionary<string,TD> dict,  string key, Func<T> createFunc, bool exclusive = false) where T : TD
        {
            TD entry;

            if (dict.TryGetValue(key, out entry) && (exclusive || !(entry is T)))
            {
                throw new Exception($"{key} already defined as {entry.GetType().Name}");
            }

            var t = (T)entry;

            if (t == null)
            {
                dict[key] = t = createFunc();
            }

            return t;
        }


        public static Structure Pop(this Stack<Structure> stack, string name)
        {
            if (!stack.Any() || string.Compare(stack.Peek().Name, name, StringComparison.OrdinalIgnoreCase) != 0)
            {
                throw new Exception($"Missing close for {name}");
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
            var func = testcase.For ?? dict[DictType.Dict].Last().Key;

            dict[DictType.TestCase].Add($"( Test Case {func} ) {testcase.Text}", testcase);
        }

        public static void Add(this Dictionary<DictType, Dictionary<string, IDictEntry>> dict, Prerequisite prerequisite)
        {
            var func = dict[DictType.Dict].Last().Key;

            dict[DictType.PreComp].Add(func, prerequisite);
        }
    }
}