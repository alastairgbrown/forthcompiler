using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using static System.Linq.Enumerable;
using static System.Math;
using static System.String;
using static System.StringComparer;

namespace ForthCompiler
{
    public class Cpu
    {
        public long ProgramIndex { get; set; }
        public long StackPointer { get; set; }
        public long ReturnStackPointer { get; set; }

        public SortedDictionary<long, HeapValue> Heap { get; } = new SortedDictionary<long, HeapValue>();
        public Dictionary<long, long> LastHeap { get; set; } = new Dictionary<long, long>();

        public int StackSize => (int)(StackPointer - Architecture.StackStartAddress + 1);
        public int ReturnStackSize => (int)(ReturnStackPointer - Architecture.ReturnStackStartAddress + 1);

        public IEnumerable<long> Stack
            => new[] { _top, _next }.Concat(Range(0, Max(0,StackSize)).Select(i => Heap[StackPointer - i].Value)).Take(StackSize);
        public IEnumerable<long> ReturnStack
            => Range(0, ReturnStackSize).Select(i => Heap[ReturnStackPointer - i].Value);
        public Stack<Structure> CallStack { get; } = new Stack<Structure>();
        public Queue<char> Output { get; } = new Queue<char>();
        public Queue<char> Input { get; } = new Queue<char>();
        public Architecture Architecture { get; }

        private long _top;
        private long _next;
        private int _carry;
        private string _error;
        public object[] LastState { get; private set; }
        private readonly List<CodeSlot> _codeslots = new List<CodeSlot>();
        private readonly Dictionary<long, string> _definitions;
        private readonly Dictionary<string, int> _labels;
        private readonly JavaScriptSerializer _jss = new JavaScriptSerializer();

        private Cpu(Architecture architecture)
        {
            Architecture = architecture;
            if (Architecture.UartInputAddress >= 0)
            {
                Heap[Architecture.UartInputAddress] = new IoValue { Owner = this };
            }

            if (Architecture.UartInputReadyAddress >= 0)
            {
                Heap[Architecture.UartInputReadyAddress] = new IoInputReady { Owner = this };
            }
            ResetStacks();
        }

        public void ResetStacks()
        {
            StackPointer = Architecture.StackStartAddress - 1;
            ReturnStackPointer = Architecture.ReturnStackStartAddress - 1;
            CallStack.Push(new Structure { Name = "..0" });
        }

        public Cpu(List<long> code, Dictionary<long, string> labels, Architecture architecture) : this(architecture)
        {
            _definitions = labels.Where(l => l.Value.StartsWith(".")).ToDictionary(l => l.Key, l => l.Value);

            for (int i = 0; i < code.Count; i++)
            {
                var opcode = Architecture.Opcodes.FirstOrDefault(oc => oc.Value?.SequenceEqual(code.Skip(i).Take(oc.Value.Length)) == true);

                if (opcode.Value == null)
                {
                    long value = _top = code[i] >= 0x8 ? -1 : 0;
                    int count = 0;

                    for (; i + count < code.Count && code[i + count] <= 0xF; count++)
                    {
                        value = (value << 4) | code[i + count];
                    }

                    i += Max(0, count - 1);
                    _codeslots.Add(value);
                }
                else
                {
                    _codeslots.Add(opcode.Key);
                    i += opcode.Value.Length - 1;
                }

                while (_codeslots.Count < i + 1)
                {
                    _codeslots.Add(OpCode.NoOperation);
                }
            }
        }

        public Cpu(List<CodeSlot> codeSlots, Architecture architecture) : this(architecture)
        {
            _codeslots = codeSlots;
            _definitions = _codeslots.Where(cs => cs?.Label?.StartsWith(".") == true)
                                     .GroupBy(cs => cs.CodeIndex)
                                     .ToDictionary(cs => cs.Key, cs => cs.First().Label);
            _labels = Range(0, _codeslots.Count).Where(i => _codeslots[i].OpCode == OpCode.Label)
                                     .GroupBy(i => _codeslots[i].Label, OrdinalIgnoreCase)
                                     .ToDictionary(g => g.Key, g => g.First(), OrdinalIgnoreCase);
        }

        public IEnumerable<object> CurrState => new object[]
        {
            "PS=", ProgramIndex,
            ProgramIndex == 0 ? "(Start)" : ProgramIndex == _codeslots.Count ? "(End)" : "",
            " SP=", StackPointer,
            " RP=", ReturnStackPointer,
            " Top=", _top,
            " Next=", _next,
            " Carry=", _carry,
            " Input=", _jss.Serialize(Join(null, Input)),
            " ",_error,Environment.NewLine,
            "Stack=",
        }.Concat(Stack.Take(30).Reverse().SelectMany(i => new object[] { i, " " }));

        void Step()
        {
            if (ProgramIndex < 0 || ProgramIndex >= _codeslots.Count)
            {
                throw new Exception("Outside executable code");
            }

            var code = _codeslots[(int)(ProgramIndex++)];
            long add;

            switch (code.OpCode)
            {
                case OpCode.Ldw:
                    _top = Heap.At(_top)?.Read() ?? 0;
                    break;
                case OpCode.Stw:
                    Heap.At(_top, () => new HeapValue()).Write(_next);
                    break;
                case OpCode.Psh:
                    Heap.At(++StackPointer, () => new HeapValue()).Write(_next);
                    _next = _top;
                    break;
                case OpCode.Tor:
                    Heap.At(++ReturnStackPointer, () => new HeapValue()).Write(_top);
                    break;
                case OpCode.Pop:
                    _top = _next;
                    _next = Heap.At(StackPointer--)?.Read() ?? 0;
                    break;
                case OpCode.Rto:
                    _top = Heap.At(ReturnStackPointer--)?.Read() ?? 0;
                    break;
                case OpCode.Swp:
                    _next = Interlocked.Exchange(ref _top, _next);
                    break;
                case OpCode.Jnz:
                    if (_top != 0)
                    {
                        ProgramIndex = Architecture.ToCodeIndex(_top);
                    }
                    _top = _next;
                    _next = Heap.At(StackPointer--)?.Read() ?? 0;
                    break;
                case OpCode.Jsr:
                    var temp = Architecture.ToAddressAndSubWordSlot(ProgramIndex);
                    ProgramIndex = Architecture.ToCodeIndex(_top);
                    _top = temp;
                    break;
                case OpCode.Add:
                    _top = _top >= 0 ? _top : Architecture.DataPathCarryBit + _top;
                    _next = _next >= 0 ? _next : Architecture.DataPathCarryBit + _next;
                    add = _next + _top;
                    _top = add & Architecture.DataPathMask;
                    _top = (add & Architecture.DataPathMsBit) == 0 ? _top : -(Architecture.DataPathCarryBit - _top);
                    _carry = (add & Architecture.DataPathCarryBit) == 0 ? 0 : 1;
                    _next = Heap.At(StackPointer--)?.Read() ?? 0;
                    break;
                case OpCode.Adc:
                    _top = _top >= 0 ? _top : Architecture.DataPathCarryBit + _top;
                    _next = _next >= 0 ? _next : Architecture.DataPathCarryBit + _next;
                    add = _next + _top + _carry;
                    _top = add & Architecture.DataPathMask;
                    _top = (add & Architecture.DataPathMsBit) == 0 ? _top : -(Architecture.DataPathCarryBit - _top);
                    _carry = (add & Architecture.DataPathCarryBit) == 0 ? 0 : 1;
                    _next = Heap.At(StackPointer--)?.Read() ?? 0;
                    break;
                case OpCode.Sub:
                    _top = _top >= 0 ? _top : Architecture.DataPathCarryBit + _top;
                    _next = _next >= 0 ? _next : Architecture.DataPathCarryBit + _next;
                    add = _next + ~_top + 1;
                    _top = add & Architecture.DataPathMask;
                    _top = (add & Architecture.DataPathMsBit) == 0 ? _top : -(Architecture.DataPathCarryBit - _top);
                    _carry = (add & Architecture.DataPathCarryBit) == 0 ? 0 : 1;
                    _next = Heap.At(StackPointer--)?.Read() ?? 0;
                    break;
                case OpCode.And:
                    _top &= _next;
                    _next = Heap.At(StackPointer--)?.Read() ?? 0;
                    break;
                case OpCode.Xor:
                    _top ^= _next;
                    _next = Heap.At(StackPointer--)?.Read() ?? 0;
                    break;
                case OpCode.Ior:
                    _top |= _next;
                    _next = Heap.At(StackPointer--)?.Read() ?? 0;
                    break;
                case OpCode.Mlt:
                    _top *= _next;
                    _next = Heap.At(StackPointer--)?.Read() ?? 0;
                    break;
                case OpCode.Lsr:
                    _carry = (int)(_top & 1);
                    _top = _top >> 1;
                    break;
                case OpCode.Zeq:
                    _top = _top == 0 ? -1 : 0;
                    break;
                case OpCode.Literal:
                    _top = code.Value;
                    break;
                case OpCode.Address:
                    _labels.ContainsKey(code.Label).Validate(a => $"Missing {code.Label}", v => v);
                    _top = Architecture.ToAddressAndSubWordSlot(_labels[code.Label]);
                    break;
                case OpCode.Lsp:
                    _top = StackPointer;
                    break;
                case OpCode.Lrp:
                    _top = ReturnStackPointer;
                    break;
                case OpCode.NoOperation:
                case OpCode.Label:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Run(Func<bool> breakCondition)
        {
            _error = null;
            LastState = CurrState.ToArray();
            LastHeap = Heap.ToDictionary(h => h.Key, h => h.Value.Value);

            for (var i = 0; ProgramIndex != _codeslots.Count && (i == 0 || !breakCondition()); i++)
            {
                var lastSlot = ProgramIndex;

                try
                {
                    Step();
                    if (i == 1000000)
                    {
                        throw new Exception("Exceeded execution limit");
                    }
                }
                catch (Exception ex)
                {
                    _error = $"Error={ex.Message}";
                    break;
                }

                if (_codeslots[(int)lastSlot].OpCode == OpCode.Jsr && _definitions.ContainsKey(ProgramIndex))
                {
                    CallStack.Peek().Value = Architecture.ToCodeIndex(_top);
                    CallStack.Push(new Structure { Name = _definitions[ProgramIndex] });
                }
                else if (CallStack.Count >= 2 && ProgramIndex == CallStack.Skip(1).First().Value)
                {
                    CallStack.Pop();
                }

                CallStack.Peek().Value = ProgramIndex;
            }
        }
    }

    public class HeapValue
    {
        public virtual long Value { get; set; }

        public virtual long Read()
        {
            return Value;
        }

        public virtual void Write(long value)
        {
            Value = value;
        }
    }

    public class IoValue : HeapValue
    {
        public Cpu Owner { get; set; }

        public override long Read()
        {
            return Owner.Input.Any() ? Owner.Input.Dequeue() : 0;
        }

        public override void Write(long value)
        {
            Owner.Output.Enqueue((char)value);
        }
    }

    public class IoInputReady : HeapValue
    {
        public Cpu Owner { get; set; }

        public override long Value
        {
            get { return (base.Value & ~(1 << (int)Owner.Architecture.UartInputReadyBit)) | (Owner.Input.Any() ? (1 << (int)Owner.Architecture.UartInputReadyBit) : 0L); }
            set { base.Value = value; }
        }
    }
}