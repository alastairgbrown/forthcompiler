using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ForthCompiler
{
    public class Cpu
    {
        public int ProgramSlot { get; set; }
        public SortedDictionary<int,int> Heap { get; } = new SortedDictionary<int, int>();
        public Dictionary<int, int> LastHeap { get; set; } = new Dictionary<int, int>();
        public Stack<int> Stack { get; } = new Stack<int>();
        public IEnumerable<int> ForthStack => new[] { _top, _next }.Concat(Stack).Take(Stack.Count);
        public Stack<Structure> CallStack { get; } = new Stack<Structure>();

        private int _top;
        private int _next;
        private bool _carry;
        private string _error;
        public string[] LastState { get; private set; }
        private readonly CodeSlot[] _codeslots;
        public Func<int, string> Formatter { get; set; } = i => $"{i}";
        private Dictionary<int, string> _definitions;

        public Cpu(Compiler compiler)
        {
            _codeslots = compiler.CodeSlots.ToArray();
            _definitions = compiler.Labels.Where(l => l.Key.StartsWith("Global."))
                                          .ToDictionary(l => l.Value.CodeSlot, l => l.Key);
            CallStack.Push(new Structure { Name = "Global.Global.0" });
        }

        public IEnumerable<string> ThisState => new[]
        {
            $"PS={Formatter(ProgramSlot)} ",
            $"SP={Formatter(Stack.Count)} ",
            $"Top={Formatter(_top)} ",
            $"Next={Formatter(_next)} ",
            $"Carry={_carry} ",
            $"{_error}",
            Environment.NewLine,
            "Stack=",
        }.Concat(ForthStack.Reverse().Select(i => $"{Formatter(i)} "));

        void Step()
        {
            if (ProgramSlot < 0 || ProgramSlot >= _codeslots.Length)
            {
                throw new Exception($"Outside executable code {Formatter(ProgramSlot)}");
            }

            var code = _codeslots[ProgramSlot++];
            ulong add;

            switch (code.Code)
            {
                case Code._:
                    break;
                case Code.Ldw:
                    _top = Heap.Entry(_top);
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
                        ProgramSlot = _top * 8;
                    }
                    _top = _next;
                    _next = Stack.Pop();
                    break;
                case Code.Jsr:
                    var temp = (ProgramSlot + 7) / 8;
                    ProgramSlot = _top * 8;
                    _top = temp;
                    break;
                case Code.Add:
                    add = (ulong)unchecked((uint)_next) + unchecked((uint)_top);
                    _carry = (add & (1ul << 32)) != 0;
                    _top = unchecked((int)add);
                    _next = Stack.Pop();
                    break;
                case Code.Adc:
                    add = (ulong)unchecked((uint)_next) + unchecked((uint)_top) + (_carry ? 1u : 0);
                    _carry = (add & (1ul << 32)) != 0;
                    _top = unchecked((int)add);
                    _next = Stack.Pop();
                    break;
                case Code.Sub:
                    add = (ulong)unchecked((uint)_next) + ~unchecked((uint)_top) + 1;
                    _carry = (add & (1ul << 32)) != 0;
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
                case Code.Lsr:
                    _carry = (_top & 1) == 1;
                    _top = _top >> 1;
                    break;
                case Code.Zeq:
                    _top = _top == 0 ? -1 : 0;
                    break;
                case Code.Lit:
                    Stack.Push(_next);
                    _next = _top;
                    _top = code.Value;
                    break;
            }

            while (ProgramSlot >= 0 && ProgramSlot < _codeslots.Length && _codeslots[ProgramSlot] == null)
            {
                ProgramSlot++;
            }
        }

        public void Run(Func<int, bool> breakCondition)
        {
            _error = null;
            LastState = ThisState.ToArray();
            LastHeap = Heap.ToDictionary(h => h.Key, h => h.Value);

            for (int i = 0; i == 0 || !breakCondition(i); i++)
            {
                var lastSlot = ProgramSlot;

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

                if (_codeslots[lastSlot].Code == Code.Jsr && _definitions.ContainsKey(ProgramSlot))
                {
                    var next = Enumerable.Range(0, _codeslots.Length).Skip(_top * 8).First(cs => _codeslots[cs] != null);
                    CallStack.Peek().Value = next;
                    CallStack.Push(new Structure { Name = _definitions[ProgramSlot] });
                }
                else if (CallStack.Count >= 2 && ProgramSlot == CallStack.Skip(1).First().Value)
                {
                    CallStack.Pop();
                }

                CallStack.Peek().Value = ProgramSlot;
            }
        }
    }
}