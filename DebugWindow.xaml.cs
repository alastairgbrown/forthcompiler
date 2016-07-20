using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ForthCompiler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class DebugWindow
    {
        public int MacroLevel { get; private set; }
        public Compiler Compiler { get; }

        public Cpu Cpu { get; set; }
        public int ProgramSlot { get; set; }

        private HashSet<int> _breaks;

        private ObservableCollection<SourceItem> SourceItems { get; } = new ObservableCollection<SourceItem>();
        private ObservableCollection<HeapItem> HeapItems { get; } = new ObservableCollection<HeapItem>();
        private ObservableCollection<CallStackItem> CallStackItems { get; } = new ObservableCollection<CallStackItem>();


        public DebugWindow(Compiler compiler, bool test)
        {
            InitializeComponent();

            Compiler = compiler;
            HeapListBox.ItemsSource = HeapItems;
            SourceListBox.ItemsSource = SourceItems;
            CallStackListBox.ItemsSource = CallStackItems;

            for (int i = 0; i < Compiler.Tokens.Count; i++)
            {
                var token = Compiler.Tokens[i];

                if (i == 0 || Compiler.Tokens[i - 1].File != token.File || Compiler.Tokens[i - 1].Y != token.Y)
                {
                    SourceItems.Add(new SourceItem { Parent = this });
                }

                SourceItems.Last().Tokens.Add(token);
            }

            Restart_Click(null, null);

            if (compiler.Error != null)
            {
                var textBlock = new TextBlock();

                Status.Inlines.Clear();
                Status.Inlines.Add(new Run { Text = Compiler.Error, Foreground = Brushes.Red });
            }
            else if (test)
            {
                RunTests_Click(null, null);
            }
        }

        private void Refresh(Func<SourceItem, bool> refreshSource = null, Func<HeapItem, bool> refreshHeap = null)
        {
            var last = Cpu.LastState ?? Cpu.CurrState.ToArray();
            var curr = Cpu.CurrState.ToArray();

            Array.Resize(ref last, Math.Max(last.Length, curr.Length));
            Array.Resize(ref curr, Math.Max(last.Length, curr.Length));

            Status.Inlines.Clear();
            Status.Inlines.AddRange(Enumerable.Range(0, curr.Length).Select(
                    i => new Run
                    {
                        Text = curr[i] == null ? "-" : curr[i] is int ? Formatter((int)curr[i]) : $"{curr[i]}",
                        Foreground = curr[i] == last[i] ? Brushes.Black : Brushes.Red
                    }));

            foreach (var item in SourceItems.Where(si => refreshSource == null || refreshSource(si)))
            {
                item.Refresh();
            }

            var addresses = new HashSet<int>(HeapItems.Select(hi => hi.Address));
            HeapItems.AddRange(Cpu.Heap.Keys.Where(a => !addresses.Contains(a)).Select(a => new HeapItem { Parent = this, Address = a }));
            HeapItems.Sort((a, b) => a.Address.CompareTo(b.Address));

            foreach (var item in HeapItems.Where(hi => hi.WasChanged || refreshHeap == null || refreshHeap(hi)))
            {
                item.Refresh();
                item.WasChanged = item.IsChanged;
            }

            CallStackItems.AddRange(Enumerable.Range(0, Math.Max(0, Cpu.CallStack.Count - CallStackItems.Count))
                                              .Select(i => new CallStackItem { Parent = this }));
            CallStackItems.RemoveRange(0, Math.Max(0, CallStackItems.Count - Cpu.CallStack.Count));

            for (int i = 0; i < CallStackItems.Count; i++)
            {
                CallStackItems[i].Item = Cpu.CallStack.ElementAt(i);
                CallStackItems[i].Refresh();
            }

            if (SourceItems.Any(si => si.Contains(ProgramSlot)))
            {
                SourceListBox.ScrollIntoView(SourceItems.First(si => si.Contains(ProgramSlot)));
            }
        }

        public string Formatter(int i)
        {
            return ShowHex.IsChecked ? $"${i:X}" : $"{i}";
        }

        private void Run(Func<int, bool> breakCondition)
        {
            var start = SourceItems.FirstOrDefault(si => si.Contains(Cpu.ProgramSlot));

            _breaks = new HashSet<int>(SourceItems.Where(i => i.Break).Select(i => i.CodeSlot));
            Cpu.Run(breakCondition);

            ProgramSlot = Cpu.ProgramSlot;
            Refresh(si => si == start || si.Contains(ProgramSlot), hi => hi.IsChanged || hi.WasChanged);
        }


        private void StepAsm_Click(object sender, RoutedEventArgs e)
        {
            Run(i => true);
        }

        private void StepToken_Click(object sender, RoutedEventArgs e)
        {
            var token = Compiler.Tokens.FirstOrDefault(t => t.Contains(Cpu.ProgramSlot));

            Run(i => !token.Contains(Cpu.ProgramSlot));
        }

        private void StepOver_Click(object sender, RoutedEventArgs e)
        {
            var line = SourceItems.FirstOrDefault(si => si.Contains(Cpu.ProgramSlot));
            var callstack = Cpu.CallStack.Count;

            Run(i => line == null || (!line.Contains(Cpu.ProgramSlot) && Cpu.CallStack.Count <= callstack) || _breaks.Contains(Cpu.ProgramSlot));
        }

        private void StepInto_Click(object sender, RoutedEventArgs e)
        {
            var line = SourceItems.FirstOrDefault(si => si.Contains(Cpu.ProgramSlot));

            Run(i => line == null || !line.Contains(Cpu.ProgramSlot) || _breaks.Contains(Cpu.ProgramSlot));
        }

        private void StepOut_Click(object sender, RoutedEventArgs e)
        {
            var callstack = Cpu.CallStack.Count;

            Run(i => Cpu.CallStack.Count < callstack || _breaks.Contains(Cpu.ProgramSlot));
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            Run(i => i > 0 && _breaks.Contains(Cpu.ProgramSlot));
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            Cpu = new Cpu(Compiler);

            HeapItems.Clear();
            foreach (var variable in Compiler.Dict.Select(v => new { v.Key, Value = v.Value as VariableEntry }).Where(v => v.Value != null))
            {
                HeapItems.Add(new HeapItem { Parent = this, Name = variable.Key, Address = variable.Value.HeapAddress });
            }

            SourceItems.ToList().ForEach(si => si.TestResult = null);
            Refresh();
        }

        private void RunTests_Click(object sender, RoutedEventArgs e)
        {
            var tests = SourceItems.Any(si => si.Break) ? SourceItems.Where(si => si.Break).ToArray() : SourceItems.ToArray();
            var results = new Dictionary<string, int> { { "PASS", 0 }, { "FAIL", 0 } };

            foreach (var test in tests.Where(t => t.IsTestCase))
            {
                Cpu = new Cpu(Compiler) { ProgramSlot = test.CodeSlot };
                Cpu.Run(i => Cpu.ProgramSlot >= test.CodeSlot + test.CodeCount);

                var stack = Cpu.ForthStack.ToArray();
                var result = "FAIL";

                if (stack.Length == 0 || stack.Length < 1 + stack.First())
                {
                    test.TestResult = $"{result} stack={string.Join(" ", stack)}";
                }
                else
                {
                    var actual = string.Join(" ", stack.Skip(1 + stack.First()).Reverse());
                    var expected = string.Join(" ", stack.Skip(1).Take(stack.First()).Reverse());

                    result = actual == expected ? "PASS" : "FAIL";
                    test.TestResult = $"{result} expected={expected} actual={actual}";
                }

                results[result]++;
            }

            ProgramSlot = Cpu.ProgramSlot;
            Refresh();

            Status.Inlines.Clear();
            Status.Inlines.Add(new Run { Text = $"Test result - {string.Join(", ", results.Select(r => $"{r.Key}: {r.Value}"))}" });
        }

        private void SetPc_Click(object sender, RoutedEventArgs e)
        {
            var item = SourceListBox.SelectedItem as SourceItem;

            if (item != null)
            {
                Cpu.ProgramSlot = item.CodeSlot;
            }

            Refresh();
        }

        private void Show_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void MacroLevel_Click(object sender, RoutedEventArgs e)
        {
            var menuItems = View.Items.OfType<Control>().TakeWhile(c => c is MenuItem).OfType<MenuItem>().ToList();

            MacroLevel = menuItems.IndexOf((MenuItem)sender);
            menuItems.ForEach(mi => mi.IsChecked = ReferenceEquals(mi, sender));

            Refresh();
        }

        private void CallStackListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CallStackListBox.SelectedItem != null)
            {
                ProgramSlot = ((CallStackItem)CallStackListBox.SelectedItem).Item.Value;
                Refresh(si => Cpu.CallStack.Any(csi => si.Contains(csi.Value)), hi => hi.IsChanged || hi.WasChanged);
            }
        }
    }
}
