using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using ForthCompiler.Annotations;
using static System.Linq.Enumerable;
using static System.Math;
using static System.String;
using static System.StringComparer;

namespace ForthCompiler
{
    public static class ExtensionMethods
    {
        public static bool IsEqual(this string a, string b)
        {
            return Compare(a, b, StringComparison.OrdinalIgnoreCase) == 0;
        }

        public static void Sort<T,TKey>([NotNull]this ObservableCollection<T> collection, Func<T, TKey> keySelector)
        {
            var sortableList = collection.OrderBy(keySelector).ToArray();

            for (int i = 0; i < sortableList.Length; i++)
            {
                collection.Move(collection.IndexOf(sortableList[i]), i);
            }
        }

        public static void AddRange([NotNull]this IList collection, IEnumerable items)
        {
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }

        public static void RemoveRange([NotNull]this IList collection, int index, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                collection.RemoveAt(index + i);
            }
        }

        public static TV At<TK, TV>([NotNull]this IDictionary<TK, TV> dict, TK key)
        {
            TV value;
            return dict.TryGetValue(key, out value) ? value : default(TV);
        }

        public static IEnumerable<T> Skip<T>([NotNull]this IList<T> items, int count)
        {
            for (int i = count; i < items.Count; i++)
            {
                yield return items[i];
            }
        }

        public static string ToText([NotNull] this IEnumerable<Token> tokens)
        {
            return Join(null, tokens.Select(t => t.Text)).Dequote();
        }

        public static string ToScript([NotNull] this IEnumerable<Token> tokens)
        {
            return Join(
                Environment.NewLine,
                tokens.GroupBy(t => new { t.File, t.Y }).Select(g => Join(string.Empty, g.Select(t => t.Text))));
        }

        public static string ToDoc(this IEnumerable<Token> tokens)
        {
            return Join(null,
                tokens.SkipWhile(t => !t.IsDocumentation)
                      .TakeWhile(t => t.IsExcluded)
                      .Select(t => t.Text)).Trim('(', ')', '\\', ' ', '\t', '\r', '\n');
        }


        public static Dictionary<string, List<Token>> ToDict(this IEnumerable<Token> text, params string[] keywords)
        {
            var dict = new Dictionary<string, List<Token>>(OrdinalIgnoreCase);
            var keyword = keywords.First();

            foreach (var token in text)
            {
                if (keywords.Any(k => k.IsEqual(token.Text)))
                {
                    dict[keyword = token.Text] = new List<Token>();
                }
                else
                {
                    dict[keyword].Add(token);
                }
            }

            return dict;
        }

        public static void ForEach<T>([NotNull]this IEnumerable<T> items, Action<T> action)
        {
            foreach (var item in items)
            {
                action(item);
            }
        }

        public static T At<TKey, TVal, T>([NotNull]this IDictionary<TKey, TVal> dict, TKey key, Func<T> createFunc, bool exclusive = false) where T : TVal
        {
            TVal entry;

            if (dict.TryGetValue(key, out entry))
            {
                entry.Validate(e => $"{key} is already defined as a {e.GetType().Name}", e => e is T && !exclusive);
            }

            var t = (T)entry;

            if (t == null)
            {
                dict[key] = t = createFunc();
            }

            return t;
        }

        public static Structure Pop([NotNull]this Stack<Structure> stack, string name)
        {
            stack.Validate(s => $"Missing {name}", s => (stack.FirstOrDefault()?.Name).IsEqual(name));

            return stack.Pop();
        }

        public static bool Contains([NotNull]this ISlotRange token, long slot)
        {
            return slot >= token.CodeIndex && slot < token.CodeIndex + token.CodeCount;
        }

        public static string Dequote(this string text)
        {
            text = text?.Trim(' ', '\t', '\r', '\n') ?? String.Empty;
            var match = Regex.Match(text, @"^[Cc.]?""((?:""""|[^""])*)""$");
            return match.Success ? match.Groups[1].Value.Replace(@"""""", @"""") : text;
        }

        public static T Validate<T>(this T obj, Func<T, string> message, Predicate<T> func = null)
        {
            if (!(func ?? (x => x != null))(obj))
            {
                throw new Exception(message(obj));
            }

            return obj;
        }

        public static string LoadText([NotNull]this string name)
        {
            var bytes = name.LoadBytes();

            foreach (var enc in new[] { Encoding.UTF8 }
                        .Where(e => e.GetPreamble().SequenceEqual(bytes.Take(e.GetPreamble().Length))))
            {
                return enc.GetString(bytes.Skip(enc.GetPreamble().Length).ToArray());
            }

            return Encoding.ASCII.GetString(bytes);
        }

        public static byte[] LoadBytes([NotNull]this string name)
        {
            if (File.Exists(name))
            {
                return File.ReadAllBytes(name);
            }

            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.{name}");
            var bytes = new byte[stream?.Length ?? 0];

            stream?.Read(bytes, 0, bytes.Length);

            return bytes;
        }

        public static void SetCount<T>([NotNull] this List<T> list, int count)
        {
            list.RemoveRange(count, Max(0, list.Count - count));
            list.AddRange(Range(0, Max(0, count - list.Count)).Select(e => default(T)));
        }

        public static void Increment([NotNull] this Dictionary<string, int> dict, [CallerMemberName] string key = null)
        {
            dict[key] = dict.At(key) + 1;
        }

        public static int ToAddressAndSlot(this int value)
        {
            return (value / 6) * 8 + (value % 6);
        }

        public static int ToCodeIndex(this int value)
        {
            return (value / 8) * 6 + (value % 8);
        }

        public static IEnumerable<long> ToPfx(this long value, int? length = null)
        {
            var twosComp = unchecked((ulong)value);
            var codes = Range(0, 8).Select(i => (twosComp >> ((7 - i) * 4)) & 0xF).ToArray();
            var msbit = Range(1, 31).Reverse().FirstOrDefault(i => ((twosComp >> i) & 1) != (value < 0 ? 1ul : 0));
            var needed = length ?? ((msbit + 5) / 4);

            return codes.Skip(codes.Length - needed).Select(c => (long)c);
        }

        public static void Replace([NotNull] this List<CodeSlot> list, int index, int remove, CodeSlot[] add)
        {
            int common = Min(remove, add.Length);
            remove = Max(remove - add.Length, 0);

            for (int x = 0; x < common; x++)
            {
                list[index + x].OpCode = add[x].OpCode;
                list[index + x].Value = add[x].Value;
                list[index + x].Label = add[x].Label;
            }

            list.RemoveRange(index + common, remove);
            list.InsertRange(index + common, add.Skip(common));

        }
    }
}