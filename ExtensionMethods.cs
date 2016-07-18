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

        public static T Entry<TD,T>(this Dictionary<string,TD> dict,  string key, Func<T> createFunc, bool exclusive = false) where T : TD
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
            if (!stack.Any() || !stack.Peek().Name.IsEqual(name))
            {
                throw new Exception($"Missing close for {name}");
            }

            return stack.Pop();
        }

        public static bool Contains(this ISlotRange token, int slot)
        {
            return slot >= token.CodeSlot && slot < token.CodeSlot + token.CodeCount;
        }

        public static string Dequote(this string text)
        {
            return text.Trim('"');
        }
    }
}