using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static System.Linq.Enumerable;
using static System.StringComparer;

namespace ForthCompiler
{
    public class Cpu
    {
        public long ProgramIndex { get; set; }
        public SortedDictionary<long, long> Heap { get; } = new SortedDictionary<long, long>();
        public Dictionary<long, long> LastHeap { get; set; } = new Dictionary<long, long>();
        public Stack<long> Stack { get; } = new Stack<long>();
        public IEnumerable<long> ForthStack => new[] { _top, _next }.Concat(Stack).Take(Stack.Count);
        public Stack<Structure> CallStack { get; } = new Stack<Structure>();

        private long _top;
        private long _next;
        private int _carry;
        private bool _loadingPfx;
        private string _error;
        public object[] LastState { get; private set; }
        private readonly List<CodeSlot> _codeslots;
        private readonly Architecture _architecture;
        private readonly Dictionary<long, string> _definitions;
        private readonly Dictionary<string, int> _labels;

        public Cpu(List<CodeSlot> codeSlots, Architecture architecture)
        {
            _codeslots = codeSlots;
            _architecture = architecture;
            _definitions = _codeslots.Where(cs => cs?.Label?.StartsWith(".") == true)
                                     .GroupBy(cs => cs.CodeIndex)
                                     .ToDictionary(cs => cs.Key, cs => cs.First().Label);
            _labels = Range(0, _codeslots.Count).Where(i => _codeslots[i].OpCode == OpCode.Label)
                                     .GroupBy(i => _codeslots[i].Label, OrdinalIgnoreCase)
                                     .ToDictionary(g => g.Key, g => g.First(), OrdinalIgnoreCase);
            CallStack.Push(new Structure { Name = "..0" });
        }

        public IEnumerable<object> CurrState => new object[]
        {
            "PS=", ProgramIndex,
            ProgramIndex == 0 ? "(Start)" : ProgramIndex == _codeslots.Count ? "(End)" : "",
            " SP=", Stack.Count,
            " Top=", _top,
            " Next=", _next,
            " Carry=", _carry,
            " ",_error,Environment.NewLine,
            "Stack=",
        }.Concat(ForthStack.Take(30).Reverse().SelectMany(i => new object[] { i, " " }));

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
                    _top = Heap.At(_top);
                    break;
                case OpCode.Stw:
                    Heap[_top] = _next;
                    break;
                case OpCode.Psh:
                    Stack.Push(_next);
                    _next = _top;
                    break;
                case OpCode.Pop:
                    _top = _next;
                    _next = Stack.Pop();
                    break;
                case OpCode.Swp:
                    _next = Interlocked.Exchange(ref _top, _next);
                    break;
                case OpCode.Jnz:
                    if (_top != 0)
                    {
                        ProgramIndex = _architecture.ToCodeIndex(_top);
                    }
                    _top = _next;
                    _next = Stack.Pop();
                    break;
                case OpCode.Jsr:
                    var temp = _architecture.ToAddressAndSubWordSlot(ProgramIndex);
                    ProgramIndex = _architecture.ToCodeIndex(_top);
                    _top = temp;
                    break;
                case OpCode.Add:
                    _top = _top >= 0 ? _top : _architecture.DataPathCarryBit + _top;
                    _next = _next >= 0 ? _next : _architecture.DataPathCarryBit + _next;
                    add = _next + _top;
                    _top = add & _architecture.DataPathMask;
                    _top = (add & _architecture.DataPathMsBit) == 0 ? _top : -(_architecture.DataPathCarryBit - _top);
                    _carry = (add & _architecture.DataPathCarryBit) == 0 ? 0 : 1;
                    _next = Stack.Pop();
                    break;
                case OpCode.Adc:
                    _top = _top >= 0 ? _top : _architecture.DataPathCarryBit + _top;
                    _next = _next >= 0 ? _next : _architecture.DataPathCarryBit + _next;
                    add = _next + _top + _carry;
                    _top = add & _architecture.DataPathMask;
                    _top = (add & _architecture.DataPathMsBit) == 0 ? _top : -(_architecture.DataPathCarryBit - _top);
                    _carry = (add & _architecture.DataPathCarryBit) == 0 ? 0 : 1;
                    _next = Stack.Pop();
                    break;
                case OpCode.Sub:
                    _top = _top >= 0 ? _top : _architecture.DataPathCarryBit + _top;
                    _next = _next >= 0 ? _next : _architecture.DataPathCarryBit + _next;
                    add = _next + ~_top + 1;
                    _top = add & _architecture.DataPathMask;
                    _top = (add & _architecture.DataPathMsBit) == 0 ? _top : -(_architecture.DataPathCarryBit - _top);
                    _carry = (add & _architecture.DataPathCarryBit) == 0 ? 0 : 1;
                    _next = Stack.Pop();
                    break;
                case OpCode.And:
                    _top &= _next;
                    _next = Stack.Pop();
                    break;
                case OpCode.Xor:
                    _top ^= _next;
                    _next = Stack.Pop();
                    break;
                case OpCode.Ior:
                    _top |= _next;
                    _next = Stack.Pop();
                    break;
                case OpCode.Mlt:
                    _top *= _next;
                    _next = Stack.Pop();
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
                    _top = _architecture.ToAddressAndSubWordSlot(_labels[code.Label]);
                    break;
                default:
                    if ((int)code.OpCode >= 0x0 && (int)code.OpCode <= 0xF)
                    {
                        if (!_loadingPfx)
                        {
                            _top = (int)code.OpCode >= 0x8 ? -1 : 0;
                        }
                        _top = (_top << 4) | (long)code.OpCode;
                    }
                    break;
            }

            _loadingPfx = (int)code.OpCode >= 0x0 && (int)code.OpCode <= 0xF;
        }

        public void Run(Func<bool> breakCondition)
        {
            _error = null;
            LastState = CurrState.ToArray();
            LastHeap = Heap.ToDictionary(h => h.Key, h => h.Value);

            for (int i = 0; ProgramIndex != _codeslots.Count && (i == 0 || !breakCondition()); i++)
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
                    CallStack.Peek().Value = _architecture.ToCodeIndex(_top);
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
}