using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ForthCompiler
{
    public class Cpu
    {
        public int ProgramSlot { get; set; }
        public int[] Heap { get; }
        public int[] LastHeap { get; }
        public Stack<int> Stack { get; } = new Stack<int>();
        public IEnumerable<int> ForthStack => new[] { _top, _next }.Concat(Stack).Take(Stack.Count);

        private int _top;
        private int _next;
        private bool _carry;
        private string _error;
        public string[] LastState { get; private set; }

        private CodeSlot[] _codeslots;

        public Cpu(Compiler compiler)
        {
            _codeslots = compiler.CodeSlots.ToArray();
            Heap = new int[compiler.HeapSize];
            LastHeap = new int[compiler.HeapSize];
        }

        public IEnumerable<string> ThisState => new[]
        {
            $"PS={ProgramSlot:X} ",
            $"SP={Stack.Count} ",
            $"Top={_top} ",
            $"Next={_next} ",
            $"Carry={_carry} ",
            $"{_error}",
            Environment.NewLine,
            "Stack=",
        }.Concat(ForthStack.Reverse().Select(i => $"{i} "));

        void Step()
        {
            if (ProgramSlot < 0 || ProgramSlot >= _codeslots.Length)
            {
                throw new Exception($"Outside executable code {ProgramSlot}");
            }

            var code = _codeslots[ProgramSlot++];

            switch (code.Code)
            {
                case Code._:
                    break;
                case Code.Ldw:
                    _top = Heap[_top];
                    break;
                case Code.Stw:
                    Heap[_top] = _next;
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
                case Code.Cnz:
                    if (_top != 0)
                    {
                        var temp = (ProgramSlot + 7) / 8;
                        ProgramSlot = _top * 8;
                        _top = temp;
                    }
                    else
                    {
                        _top = _next;
                        _next = Stack.Pop();
                    }
                    break;
                case Code.Adc:
                    var adc = (long)_next + _top + (_carry ? 1 : 0);
                    _carry = adc > int.MaxValue || adc < int.MinValue;
                    _next = unchecked((int)adc);
                    break;
                case Code.Add:
                    var add = (long)_next + _top;
                    _carry = add > int.MaxValue || add < int.MinValue;
                    _next = unchecked((int)add);
                    break;
                case Code.Sub:
                    _next -= _top;
                    _carry = _next < 0;
                    break;
                case Code.And:
                    _next &= _top;
                    break;
                case Code.Xor:
                    _next ^= _top;
                    break;
                case Code.Lsr:
                    _carry = (_top & 1) == 1;
                    _next = _top >> 1;
                    break;
                case Code.Zeq:
                    _next = _top == 0 ? -1 : 0;
                    break;
                case Code.Lit:
                    _top = code.Value;
                    break;
            }

            while (ProgramSlot >= 0 && ProgramSlot < _codeslots.Length && _codeslots[ProgramSlot] == null)
            {
                ProgramSlot++;
            }
        }

        public void Run(Func<int, bool?> continueCondition)
        {
            _error = null;
            LastState = ThisState.ToArray();
            Array.Copy(Heap, LastHeap, Heap.Length);

            for (int i = 0; continueCondition(i) ?? true; i++)
            {
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
            }
        }
    }
}