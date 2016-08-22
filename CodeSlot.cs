using System.Collections.Generic;

namespace ForthCompiler
{
    public class CodeSlot
    {
        public int CodeIndex { get; set; }
        public OpCode OpCode { get; set; }
        public int Value { get; set; }
        public string Label { get; set; }

        public static implicit operator CodeSlot(OpCode opCode)
        {
            return new CodeSlot { OpCode = opCode };
        }

        public static implicit operator CodeSlot(int value)
        {
            return new CodeSlot { OpCode = OpCode.Literal, Value = value };
        }

        public override string ToString()
        {
            return $"{OpCode}{(OpCode == OpCode.Literal || OpCode == OpCode.Address || OpCode == OpCode.Label ? $" {Value}" : null)} {Label}";
        }
    }
}