using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ForthCompiler
{
    public class Architecture
    {
        public long OpcodeWordSize { get; set; } = 32;
        public long OpcodeInstructionSize { get; set; } = 5;
        public long OpcodeInstructionsPerWord { get; set; } = 6;
        public long OpcodeSubWordSlotBits { get; set; } = 3;
        public long DataPathWordSize { get; set; } = 32;

        public long DataPathMask => DataPathCarryBit - 1;
        public long DataPathCarryBit => 1L << (int)DataPathWordSize;
        public long DataPathMsBit => DataPathCarryBit >> 1;

        public Dictionary<string, PropertyInfo> GetProperties()
        {
            return GetType().GetProperties().Where(p => p.CanWrite).ToDictionary(p => "." + Regex.Replace(p.Name, "([a-z])([A-Z])", "$1_$2"));
        }

        public long ToAddressAndSubWordSlot(long value)
        {
            return ((value / OpcodeInstructionsPerWord) << (int)OpcodeSubWordSlotBits) + (value % OpcodeInstructionsPerWord);
        }

        public long ToCodeIndex(long value)
        {
            return ((value >> (int)OpcodeSubWordSlotBits) * OpcodeInstructionsPerWord) + (value & ((1 << (int)OpcodeSubWordSlotBits) - 1));
        }
    }
}