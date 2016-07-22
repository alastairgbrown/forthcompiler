using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ForthCompiler.Annotations;

namespace ForthCompiler
{
    public static class ExtensionMethods
    {
        public static bool IsEqual(this string a, string b)
        {
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase) == 0;
        }

        public static void Sort<T>(this ObservableCollection<T> collection, Comparison<T> comparison)
        {
            var sortableList = new List<T>(collection);
            sortableList.Sort(comparison);

            for (int i = 0; i < sortableList.Count; i++)
            {
                collection.Move(collection.IndexOf(sortableList[i]), i);
            }
        }

        public static void AddRange(this IList collection, IEnumerable items)
        {
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }

        public static void RemoveRange(this IList collection, int index, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                collection.RemoveAt(index + i);
            }
        }

        public static TV At<TK, TV>(this IDictionary<TK, TV> dict, TK key)
        {
            TV value;
            return dict.TryGetValue(key, out value) ? value : default(TV);
        }

        public static T At<TD, T>(this Dictionary<string, TD> dict, string key, Func<T> createFunc, bool exclusive = false) where T : TD
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
            return slot >= token.CodeIndex && slot < token.CodeIndex + token.CodeCount;
        }

        public static string Dequote(this string text)
        {
            return text.Trim('"');
        }

        public static T Validate<T>(this T obj, Predicate<T> func, string message)
        {
            if (!(func ?? (x => x != null))(obj))
            {
                throw new Exception(message);
            }

            return obj;
        }

        public static string LoadFileOrResource([NotNull]this string name)
        {
            if (false && File.Exists(name))
            {
                return File.ReadAllText(name);
            }

            var data = Resource.ResourceManager.GetObject(Path.GetFileNameWithoutExtension(name));
            var stringData = data as string;
            var byteData = data as byte[];

            if (stringData != null)
            {
                return stringData;
            }

            if (byteData != null)
            {
                foreach (var enc in new[] {Encoding.UTF8})
                {
                    var preamble = enc.GetPreamble();

                    if (byteData.Length >= preamble.Length &&
                        Enumerable.Range(0, preamble.Length).All(i => byteData[i] == preamble[i]))
                    {
                        return enc.GetString(byteData, preamble.Length, byteData.Length - preamble.Length);
                    }
                }

                return Encoding.ASCII.GetString(byteData);
            }

            throw new Exception($"Can't load {name} resource");
        }

        public static string[] SplitLines([NotNull] this string text)
        {
            return text.Split(new[] {"\r\n", "\r", "\n"}, 0);
        }
    }
}