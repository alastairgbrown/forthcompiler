using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ForthCompiler
{
    public class Architecture
    {
        [Doc("The size of the opcode word in bits")]
        public long OpcodeWordSize { get; set; } = 32;

        [Doc("The size of the opcode instruction in bits")]
        public long OpcodeInstructionSize { get; set; } = 5;

        [Doc("The number of instructions per word")]
        public long OpcodeInstructionsPerWord { get; set; } = 6;

        [Doc("The number of bits used to address an instruction within a word")]
        public long OpcodeSubWordSlotBits { get; set; } = 3;

        [Doc("The number of bits used in data words")]
        public long DataPathWordSize { get; set; } = 32;

        [Doc("The address of the UART output register")]
        public long UartOutputAddress { get; set; } = -1;

        [Doc("The address of the UART input register")]
        public long UartInputAddress { get; set; } = -1;

        [Doc("The address of the UART input ready bit")]
        public long UartInputReadyAddress { get; set; } = -1;

        [Doc("The bit index of the UART input ready bit")]
        public long UartInputReadyBit { get; set; } = -1;

        [Doc("The Stack Start Address")]
        public long StackStartAddress { get; set; } = 1024;

        [Doc("The Return Stack Start Address")]
        public long ReturnStackStartAddress { get; set; } = 1024 + 32;

        public long DataPathMask => DataPathCarryBit - 1;
        public long DataPathCarryBit => 1L << (int)DataPathWordSize;
        public long DataPathMsBit => DataPathCarryBit >> 1;

        public Dictionary<OpCode, long[]> Opcodes { get; } =
            Enum.GetValues(typeof(OpCode)).OfType<OpCode>().ToDictionary(c => c, c => (long[])null);

        public Dictionary<string, Property> GetProperties()
        {
            var props = GetType().GetProperties()
                .Where(p => p.CanWrite)
                .Select(p => new Property
                {
                    Name = "." + Regex.Replace(p.Name, "([a-z])([A-Z])", "$1_$2"),
                    Set = v => p.SetValue(this, v[0]),
                    Get = () => new[] { (long)p.GetValue(this) },
                    Doc = p.GetCustomAttribute<DocAttribute>()?.Doc
                })
                .Concat(Opcodes.Keys.Select(c => new Property
                {
                    Name = $".{c}",
                    Set = v => Opcodes[c] = v,
                    Get = () => Opcodes[c],
                }
                ));

            return props.ToDictionary(p => p.Name);
        }

        public long ToAddressAndSubWordSlot(long value)
        {
            return ((value / OpcodeInstructionsPerWord) << (int)OpcodeSubWordSlotBits) + (value % OpcodeInstructionsPerWord);
        }

        public long ToCodeIndex(long value)
        {
            return ((value >> (int)OpcodeSubWordSlotBits) * OpcodeInstructionsPerWord) + (value & ((1 << (int)OpcodeSubWordSlotBits) - 1));
        }

        public class Property
        {
            public string Name { get; set; }
            public Action<long[]> Set { get; set; }
            public Func<long[]> Get { get; set; }
            public string Doc { get; set; }
        }
    }
}