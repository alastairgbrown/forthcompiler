using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace ForthCompiler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class DebugWindow
    {
        private readonly bool _test;

        public int MacroLevel { get; private set; }
        public Compiler Compiler { get; }

        public Cpu Cpu { get; set; }

        private HashSet<int> _breaks;

        private ObservableCollection<SourceItem> SourceItems { get; } = new ObservableCollection<SourceItem>();
        private ObservableCollection<HeapItem> HeapItems { get; } = new ObservableCollection<HeapItem>();
        private ObservableCollection<CallStackItem> CallStackItems { get; } = new ObservableCollection<CallStackItem>();

        public DebugWindow(Compiler compiler, bool test)
        {
            InitializeComponent();

            _test = test;

            Compiler = compiler;

            for (int i = 0; i < Compiler.HeapSize; i++)
            {
                HeapItems.Add(new HeapItem { Address = i, Parent = this, Name = compiler.Dict.FirstOrDefault(t => (t.Value as VariableEntry)?.HeapAddress == i).Key });
            }

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

            RestartButton_Click(null, null);

            if (compiler.Error != null)
            {
                var textBlock = new TextBlock();

                textBlock.Inlines.Add(new Run { Text = Compiler.Error, Foreground = Brushes.Red });

                CpuLabel.Content = textBlock;
            }
            else if (test)
            {
                RunTestsButton_Click(null, null);
            }
        }

        private void Refresh(Func<SourceItem, bool> refreshSource, Func<HeapItem, bool> refreshHeap)
        {
            var lastState = Cpu.LastState ?? Cpu.ThisState.ToArray();
            var thisState = Cpu.ThisState.ToArray();
            var textBlock = new TextBlock();

            Array.Resize(ref lastState, Math.Max(lastState.Length, thisState.Length));
            Array.Resize(ref thisState, Math.Max(lastState.Length, thisState.Length));

            for (var i = 0; i < thisState.Length; i++)
            {
                textBlock.Inlines.Add(new Run { Text = thisState[i] ?? "-", Foreground = thisState[i] == lastState[i] ? Brushes.Black : Brushes.Red });
            }

            CpuLabel.Content = textBlock;

            foreach (var item in SourceItems.Where(refreshSource))
            {
                item.Refresh();
            }

            foreach (var item in HeapItems.Where(refreshHeap))
            {
                item.Refresh();
            }

            CallStackItems.Clear();
            foreach (var item in Cpu.CallStack)
            {
                CallStackItems.Add(new CallStackItem {Parent = this, Item = item});
                CallStackItems.Last().Refresh();
            }

        }

        public string Formatter(int i)
        {
            return ShowHex.IsChecked ? $"{i:X}" : $"{i}";
        }

        private void Run(Func<int, bool> breakCondition)
        {
            var start = SourceItems.FirstOrDefault(t => t.Contains(Cpu.ProgramSlot));

            _breaks = new HashSet<int>(SourceItems.Where(i => i.Break).Select(i => i.CodeSlot));
            Cpu.Run(breakCondition);

            Refresh(si => si == start || si.Contains(Cpu.ProgramSlot), hi => hi.IsChanged);
        }


        private void StepAsmButton_Click(object sender, RoutedEventArgs e)
        {
            Run(i => true);
        }

        private void StepTokenButton_Click(object sender, RoutedEventArgs e)
        {
            var token = Compiler.Tokens.FirstOrDefault(t => t.Contains(Cpu.ProgramSlot));

            Run(i => !token.Contains(Cpu.ProgramSlot));
        }

        private void StepOverButton_Click(object sender, RoutedEventArgs e)
        {
            var line = SourceItems.FirstOrDefault(t => t.Contains(Cpu.ProgramSlot));
            var callstack = Cpu.CallStack.Count;

            Run(i => line == null || (!line.Contains(Cpu.ProgramSlot) && Cpu.CallStack.Count <= callstack) || _breaks.Contains(Cpu.ProgramSlot));
        }

        private void StepIntoButton_Click(object sender, RoutedEventArgs e)
        {
            var line = SourceItems.FirstOrDefault(t => t.Contains(Cpu.ProgramSlot));

            Run(i => line == null || !line.Contains(Cpu.ProgramSlot) || _breaks.Contains(Cpu.ProgramSlot));
        }

        private void StepOutButton_Click(object sender, RoutedEventArgs e)
        {
            var callstack = Cpu.CallStack.Count;

            Run(i => Cpu.CallStack.Count < callstack || _breaks.Contains(Cpu.ProgramSlot));
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            Run(i => i > 0 && _breaks.Contains(Cpu.ProgramSlot));
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            Cpu = new Cpu(Compiler) { Formatter = Formatter };

            SourceItems.ToList().ForEach(si => si.TestResult = null);
            Refresh(si => true, hi => true);
        }

        private void RunTestsButton_Click(object sender, RoutedEventArgs e)
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

            Refresh(si => true, hi => true);

            var textBlock = new TextBlock();

            textBlock.Inlines.Add(new Run { Text = $"Test result - {string.Join(", ", results.Select(r => $"{r.Key}: {r.Value}"))}" });

            CpuLabel.Content = textBlock;
        }

        private void SetPc_Click(object sender, RoutedEventArgs e)
        {
            var item = SourceListBox.SelectedItem as SourceItem;

            if (item != null)
            {
                Cpu.ProgramSlot = item.CodeSlot;
            }

            Refresh(si => true, hi => true);
        }

        private void Show_Click(object sender, RoutedEventArgs e)
        {
            Refresh(si => true, hi => true);
        }

        private void MacroLevel_Click(object sender, RoutedEventArgs e)
        {
            var menuItems = View.Items.OfType<Control>().TakeWhile(c => c is MenuItem).OfType<MenuItem>().ToList();

            MacroLevel = menuItems.IndexOf((MenuItem)sender);
            menuItems.ForEach(mi => mi.IsChecked = ReferenceEquals(mi, sender));

            Refresh(si => true, hi => true);
        }

    }
}
