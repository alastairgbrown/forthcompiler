using System.Collections;
using System.Collections.Generic;
using ForthCompiler.Annotations;

namespace ForthCompiler
{
    public class CodeSlot : IEqualityComparer<CodeSlot>
    {
        public int CodeIndex { get; set; }
        public Code Code { get; set; }
        public int Value { get; set; }
        public string Label { get; set; }

        public static implicit operator CodeSlot(Code code)
        {
            return new CodeSlot { Code = code };
        }

        public int Size => Code == Code.Lit || Code == Code.Address ? 9 : Code == Code.Label ? 0 : 1;

        public static implicit operator CodeSlot(int value)
        {
            return new CodeSlot { Code = Code.Lit, Value = value };
        }

        public override string ToString()
        {
            return $"{Code}{(Code == Code.Lit ? " " + Value : Code == Code.Address || Code == Code.Label ? " " + Label : "")}";
        }

        public bool Equals(CodeSlot x, CodeSlot y)
        {
            return x.Code == y.Code && x.Value == y.Value && x.Label == y.Label;
        }

        public int GetHashCode(CodeSlot obj)
        {
            return obj.Code.GetHashCode() ^ obj.Value.GetHashCode() ^ (obj.Label?.GetHashCode() ?? 0);
        }
    }
}