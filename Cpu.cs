using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static System.Linq.Enumerable;

namespace ForthCompiler
{
    public class Cpu
    {
        public int ProgramIndex { get; set; }
        public SortedDictionary<int, int> Heap { get; } = new SortedDictionary<int, int>();
        public Dictionary<int, int> LastHeap { get; set; } = new Dictionary<int, int>();
        public Stack<int> Stack { get; } = new Stack<int>();
        public IEnumerable<int> ForthStack => new[] { _top, _next }.Concat(Stack).Take(Stack.Count);
        public Stack<Structure> CallStack { get; } = new Stack<Structure>();

        private int _top;
        private int _next;
        private int _carry;
        private bool _loadingPfx;
        private string _error;
        public object[] LastState { get; private set; }
        private readonly List<CodeSlot> _codeslots;
        private readonly Dictionary<int, string> _definitions;

        public Cpu(Compiler compiler)
        {
            _codeslots = compiler.CodeSlots;
            _definitions = _codeslots.Where(cs => cs?.Label?.StartsWith("Global.") == true)
                                     .GroupBy(cs => cs.CodeIndex)
                                     .ToDictionary(cs => cs.Key, cs => cs.First().Label);
            CallStack.Push(new Structure { Name = "Global.Global.0" });
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

            var code = _codeslots[ProgramIndex++];
            ulong add;

            switch (code.Code)
            {
                case Code.Ldw:
                    _top = Heap.At(_top);
                    break;
                case Code.Stw:
                    Heap[_top] = _next;
                    _top = _next;
                    _next = Stack.Pop();
                    break;
                case Code.Psh:
                    Stack.Push(_next);
                    _next = _top;
                    break;
                case Code.Pop:
                    _top = _next;
                    _next = Stack.Pop();
                    break;
                case Code.Swp:
                    _next = Interlocked.Exchange(ref _top, _next);
                    break;
                case Code.Jnz:
                    if (_top != 0)
                    {
                        ProgramIndex = _top.ToCodeIndex();
                    }
                    _top = _next;
                    _next = Stack.Pop();
                    break;
                case Code.Jsr:
                    var temp = ProgramIndex.ToAddressAndSlot();
                    ProgramIndex = _top.ToCodeIndex();
                    _top = temp;
                    break;
                case Code.Add:
                    add = (ulong)unchecked((uint)_next) + unchecked((uint)_top);
                    _carry = (int)(add >> 32) & 1;
                    _top = unchecked((int)add);
                    _next = Stack.Pop();
                    break;
                case Code.Adc:
                    add = (ulong)unchecked((uint)_next) + unchecked((uint)_top) + (uint)_carry;
                    _carry = (int)(add >> 32) & 1;
                    _top = unchecked((int)add);
                    _next = Stack.Pop();
                    break;
                case Code.Sub:
                    add = (ulong)unchecked((uint)_next) + ~unchecked((uint)_top) + 1;
                    _carry = (int)(add >> 32) & 1;
                    _top = unchecked((int)add);
                    _next = Stack.Pop();
                    break;
                case Code.And:
                    _top &= _next;
                    _next = Stack.Pop();
                    break;
                case Code.Xor:
                    _top ^= _next;
                    _next = Stack.Pop();
                    break;
                case Code.Ior:
                    _top |= _next;
                    _next = Stack.Pop();
                    break;
                case Code.Mlt:
                    _top *= _next;
                    _next = Stack.Pop();
                    break;
                case Code.Lsr:
                    _carry = _top & 1;
                    _top = _top >> 1;
                    break;
                case Code.Zeq:
                    _top = _top == 0 ? -1 : 0;
                    break;
                default:
                    if (!_loadingPfx)
                    {
                        _top = (int)code.Code >= (int)Code._8 ? -1 : 0;
                    }
                    _top = (_top << 4) | (int)code.Code;
                    break;
            }

            _loadingPfx = (int)code.Code <= (int)Code._F;

            while (ProgramIndex >= 0 && ProgramIndex < _codeslots.Count && _codeslots[ProgramIndex] == null)
            {
                ProgramIndex++;
            }
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

                if (_codeslots[lastSlot].Code == Code.Jsr && _definitions.ContainsKey(ProgramIndex))
                {
                    CallStack.Peek().Value = _top.ToCodeIndex();
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