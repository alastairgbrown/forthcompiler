using System.Collections.Generic;

namespace ForthCompiler
{
    public class CodeSlot
    {
        public int CodeIndex { get; set; }
        public Code Code { get; set; }
        public int Value { get; set; }
        public string Label { get; set; }

        public static implicit operator CodeSlot(Code code)
        {
            return new CodeSlot { Code = code };
        }

        public static implicit operator CodeSlot(int value)
        {
            return new CodeSlot { Code = Code.Lit, Value = value };
        }

        public override string ToString()
        {
            return $"{Code}{(Code == Code.Lit || Code == Code.Address || Code == Code.Label ? $" {Value}" : null)} {Label}";
        }
    }
}