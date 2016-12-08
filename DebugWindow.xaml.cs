using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using static System.Environment;
using static System.Linq.Enumerable;
using static System.Math;
using static System.String;
using Brushes = System.Windows.Media.Brushes;

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
        public long ProgramIndex { get; set; }

        private HashSet<long> _breaks;
        private readonly int _codeOrig;
        private readonly int _compOrig;
        private readonly int _tokenOrig;
        private SourceItem _commandLine;
        private readonly List<string> _commandLines = new List<string>();


        private ObservableCollection<SourceItem> SourceItems { get; } = new ObservableCollection<SourceItem>();
        private ObservableCollection<HeapItem> HeapItems { get; } = new ObservableCollection<HeapItem>();
        private ObservableCollection<CallStackItem> CallStackItems { get; } = new ObservableCollection<CallStackItem>();


        public DebugWindow(Compiler compiler, bool test, string error)
        {
            InitializeComponent();
            Icon = BitmapFrame.Create(new MemoryStream("ForthIcon.ico".LoadBytes()));
            Compiler = compiler;
            HeapListBox.ItemsSource = HeapItems;
            SourceListBox.ItemsSource = SourceItems;
            CallStackListBox.ItemsSource = CallStackItems;
            _tokenOrig = Compiler.Tokens.Count;
            _compOrig = Compiler.Compilation.Count;
            _codeOrig = Compiler.CodeSlots.Count;
            _commandLine = new SourceItem { Parent = this };

            for (int i = 0; i < Compiler.Tokens.Count; i++)
            {
                var token = Compiler.Tokens[i];

                if (i == 0 || Compiler.Tokens[i - 1].File != token.File || Compiler.Tokens[i - 1].Y != token.Y)
                {
                    SourceItems.Add(new SourceItem { Parent = this });
                }

                SourceItems.Last().Tokens.Add(token);
            }

            if (error != null)
            {
                Cpu = new Cpu(Compiler.CodeSlots, Compiler.Labels, Compiler.Architecture);
                ProgramIndex = (Compiler.ArgToken?.CodeIndex ?? 1) - 1;
                Refresh();
                CpuStatus.Inlines.Clear();
                CpuStatus.Inlines.Add(new Run { Text = error, Foreground = Brushes.Red });
            }
            else
            {
                Restart_Click(null, null);
            }
        }

        private void Refresh(Func<SourceItem, bool> refreshSource = null, Func<HeapItem, bool> refreshHeap = null)
        {
            var last = Cpu.LastState ?? Cpu.CurrState.ToArray();
            var curr = Cpu.CurrState.ToArray();

            Array.Resize(ref last, Max(last.Length, curr.Length));
            Array.Resize(ref curr, Max(last.Length, curr.Length));

            CpuStatus.Inlines.Clear();
            CpuStatus.Inlines.AddRange(Range(0, curr.Length).Select(
                    i => new Run
                    {
                        Text = curr[i] == null ? "-" : curr[i] is int ? FormatNumber((int)curr[i]) : $"{curr[i]}",
                        Foreground = curr[i] == last[i] ? Brushes.Black : Brushes.Red
                    }));
            Status.Text = _commandLines.LastOrDefault() ?? "";
            Status.Foreground = Brushes.Black;

            if (Cpu.Output.Any())
            {
                InputOutput.Text += Join(null, Cpu.Output);
                InputOutput.Select(InputOutput.Text.Length, 0);
                Cpu.Output.Clear();
            }

            if (ShowCommandLine.IsChecked)
            {
                CommandLineTokens.Content = _commandLine?.Text;
            }

            foreach (var item in SourceItems.Where(si => refreshSource == null || refreshSource(si)))
            {
                item.Refresh();
            }

            var addresses = new HashSet<long>(HeapItems.Select(hi => hi.Address));
            HeapItems.AddRange(Cpu.Heap.Keys.Where(a => !addresses.Contains(a)).Select(a => new HeapItem { Parent = this, Address = a }));
            HeapItems.Sort(hi => hi.Address);

            foreach (var item in HeapItems.Where(hi => hi.WasChanged || refreshHeap == null || refreshHeap(hi)))
            {
                item.Refresh();
                item.WasChanged = item.IsChanged;
            }

            CallStackItems.AddRange(Range(0, Max(0, Cpu.CallStack.Count - CallStackItems.Count))
                                              .Select(i => new CallStackItem { Parent = this }));
            CallStackItems.RemoveRange(0, Max(0, CallStackItems.Count - Cpu.CallStack.Count));

            for (int i = 0; i < CallStackItems.Count; i++)
            {
                CallStackItems[i].Item = Cpu.CallStack.ElementAt(i);
                CallStackItems[i].Refresh();
            }

            foreach (var sourceitem in SourceItems.Where(si => si.Contains(ProgramIndex)).Take(1))
            {
                SourceListBox.ScrollIntoView(sourceitem);
            }
        }

        public string FormatNumber(long i)
        {
            return ShowHex.IsChecked ? $"${i:X}" : $"{i}";
        }

        public string FormatAddress(long address)
        {
            return $"{FormatNumber(address)}/{Compiler.Architecture.ToAddressAndSubWordSlot(address):X}";
        }

        private void Run(Func<bool> breakCondition)
        {
            var start = SourceItems.FirstOrDefault(si => si.Contains(Cpu.ProgramIndex));

            if (Cpu.ProgramIndex == _codeOrig && CommandLine.Text.Trim() != "")
            {
                _commandLines.Remove(CommandLine.Text);
                _commandLines.Add(CommandLine.Text);
            }

            _breaks = new HashSet<long>(SourceItems.Where(i => i.Break).Select(i => i.CodeIndex));
            Cpu.Run(breakCondition);

            ProgramIndex = Cpu.ProgramIndex;
            Refresh(si => si == start || si.Contains(ProgramIndex), hi => hi.IsChanged || hi.WasChanged);
        }


        private void StepAsm_Click(object sender, RoutedEventArgs e)
        {
            Run(() => true);
        }

        private void StepToken_Click(object sender, RoutedEventArgs e)
        {
            var token = Compiler.Tokens.First(t => t.Contains(Cpu.ProgramIndex));

            Run(() => !token.Contains(Cpu.ProgramIndex));
        }

        private void StepOver_Click(object sender, RoutedEventArgs e)
        {
            var line = SourceItems.FirstOrDefault(si => si.Contains(Cpu.ProgramIndex));
            var callstackDepth = Cpu.CallStack.Count;

            Run(() => line == null || (Cpu.CallStack.Count <= callstackDepth && !line.Contains(Cpu.ProgramIndex)) || _breaks.Contains(Cpu.ProgramIndex));
        }

        private void StepInto_Click(object sender, RoutedEventArgs e)
        {
            var line = SourceItems.Concat(new[] { _commandLine }).FirstOrDefault(si => si.Contains(Cpu.ProgramIndex));

            Run(() => line == null || !line.Contains(Cpu.ProgramIndex) || _breaks.Contains(Cpu.ProgramIndex));
        }

        private void StepOut_Click(object sender, RoutedEventArgs e)
        {
            var callstackDepth = Cpu.CallStack.Count;

            Run(() => Cpu.CallStack.Count < callstackDepth || _breaks.Contains(Cpu.ProgramIndex));
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            Run(() => _breaks.Contains(Cpu.ProgramIndex));
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            Compiler.Tokens.SetCount(_tokenOrig);
            Compiler.Compilation.SetCount(_compOrig);
            Compiler.CodeSlots.SetCount(_codeOrig);
            CommandLine.Text = "";
            Cpu = new Cpu(Compiler.CodeSlots, Compiler.Labels, Compiler.Architecture);
            ProgramIndex = 0;

            ResetHeapItems();

            InputOutput.Text = string.Empty;
            SourceItems.ForEach(si => si.TestResult = null);

            Refresh();
        }

        private void ResetHeapItems()
        {
            HeapItems.Clear();
            foreach (var variable in Compiler.Words.Select(v => new { v.Key, Value = v.Value as Variable }).Where(v => v.Value != null))
            {
                HeapItems.Add(new HeapItem { Parent = this, Name = variable.Key, Address = variable.Value.HeapAddress });
            }

            foreach (var prop in Cpu.Architecture.GetProperties()
                                                 .Select(p => new { p.Key, Value = p.Value.Get()?.FirstOrDefault() ?? 0 })
                                                 .Where(p => p.Key.EndsWith("_Address") && p.Value >= 0)
                                                 .GroupBy(p => p.Value)
                                                 .Where(g => HeapItems.All(hi => hi.Address != g.Key)))
            {
                HeapItems.Add(new HeapItem { Parent = this, Name = Join(NewLine, prop.Select(p => p.Key)), Address = prop.Key });
            }
        }

        private void RunTests_Click(object sender, RoutedEventArgs e)
        {
            var selected = new HashSet<long>(SourceItems.Where(i => i.Break).Select(i => i.CodeIndex));
            var results = new Dictionary<string, int> { { "PASS", 0 }, { "FAIL", 0 } };
            var tests = SourceItems.Where(i => i.IsTestCase).ToDictionary(i => i.CodeIndex);

            Cpu = new Cpu(Compiler.CodeSlots, Compiler.Labels, Compiler.Architecture);
            ResetHeapItems();

            while (Cpu.ProgramIndex < Compiler.CodeSlots.Count)
            {
                var test = tests.At(Cpu.ProgramIndex);

                if (test == null)
                {
                    Cpu.Run(() => tests.ContainsKey(Cpu.ProgramIndex));
                    continue;
                }

                if (selected.Any() && !selected.Contains(Cpu.ProgramIndex))
                {
                    Cpu.ProgramIndex = test.CodeIndex + test.CodeCount;
                    continue;
                }

                Cpu.ResetStacks();
                Cpu.Input.Clear();
                Cpu.Output.Clear();
                Cpu.Run(() => Cpu.ProgramIndex == test.CodeIndex + test.CodeCount);

                var stack = new Stack<long>(Cpu.Stack.Reverse());
                var result = "FAIL";
                var outputRegex = new Regex("^Actual:(?<actual>.*)Expected:(?<expected>.*)Input:(?<input>.+)?$", RegexOptions.Singleline);
                var output = outputRegex.Match(Join(null, Cpu.Output));

                if (output.Groups["input"].Success)
                {
                    Cpu.ResetStacks();
                    Cpu.Input.Clear();
                    Cpu.Output.Clear();
                    output.Groups["input"].Value.AsEnumerable().ForEach(c => Cpu.Input.Enqueue(c));
                    Cpu.ProgramIndex = test.CodeIndex;
                    Cpu.Run(() => Cpu.ProgramIndex == test.CodeIndex + test.CodeCount);

                    output = outputRegex.Match(Join(null, Cpu.Output));
                }

                if (output.Success || (stack.Any() && stack.First() >= 0 && stack.Count >= 1 + stack.First()))
                {
                    var count = output.Success ? 0 : (int)stack.Pop();
                    var actual = output.Success ? output.Groups["actual"].Value : Join(" ", stack.Skip(count).Reverse());
                    var expected = output.Success ? output.Groups["expected"].Value : Join(" ", stack.Take(count).Reverse());

                    result = actual == expected ? "PASS" : "FAIL";
                    test.TestResult = $"{result} expected={expected} actual={actual}";
                }
                else
                {
                    test.TestResult = $"{result} stack={Join(" ", stack)}";
                }

                results[result]++;
            }

            ProgramIndex = Cpu.ProgramIndex;
            Refresh();

            Status.Text = $"Test result - {Join(", ", results.Select(r => $"{r.Key}: {r.Value}"))}";
            Status.Foreground = results["FAIL"] == 0 && results["PASS"] > 0 ? Brushes.Green : Brushes.Red;
        }

        private void SetPc_Click(object sender, RoutedEventArgs e)
        {
            var item = SourceListBox.SelectedItem as SourceItem;

            if (item != null)
            {
                Cpu.ProgramIndex = ProgramIndex = item.CodeIndex;
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
                ProgramIndex = ((CallStackItem)CallStackListBox.SelectedItem).Item.Value;
                Refresh(si => Cpu.CallStack.Any(csi => si.Contains(csi.Value)), hi => hi.IsChanged);
            }
        }

        private void ShowCommandLine_Click(object sender, RoutedEventArgs e)
        {
            CommandLineBorder.Visibility = ShowCommandLine.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            CommandLine.Focus();
        }

        private void CommandLineRun_Click(object sender, RoutedEventArgs e)
        {
            if (ShowCommandLine.IsChecked)
            {
                Cpu.ProgramIndex = ProgramIndex = _codeOrig;
                Run_Click(null, null);
                CommandLine.Text = "";
            }
        }

        private void CommandLine_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CommandLine.Text.IsEqual("Words"))
            {
                CpuStatus.Inlines.Clear();
                CpuStatus.Inlines.AddRange(Compiler.Words.Keys.OrderBy(w => w).Select(w =>
                    new Run
                    {
                        Text = w + " ",
                        Foreground = (SyntaxStyle.Keywords.At(w) ?? SyntaxStyle.Default).Foreground,
                        FontWeight = (SyntaxStyle.Keywords.At(w) ?? SyntaxStyle.Default).FontWeight,
                        FontStyle = (SyntaxStyle.Keywords.At(w) ?? SyntaxStyle.Default).FontStyle,
                        TextDecorations = (SyntaxStyle.Keywords.At(w) ?? SyntaxStyle.Default).TextDecoration,
                        ToolTip = Compiler.Doc.At(w)
                    }));
                Status.Text = "";
                CommandLineTokens.Content = null;
                return;
            }

            try
            {
                var start = SourceItems.FirstOrDefault(si => si.Contains(Cpu.ProgramIndex));

                Compiler.Tokens.SetCount(_tokenOrig);
                Compiler.Compilation.SetCount(_compOrig);
                Compiler.CodeSlots.SetCount(_codeOrig);
                Compiler.ReadFile(_tokenOrig, "CommandLine", x => 0, y => 0, CommandLine.Text);
                Compiler.Compile(_tokenOrig);
                Compiler.PostCompile(_codeOrig, _compOrig, _tokenOrig);

                Cpu.ProgramIndex = ProgramIndex = _codeOrig;
                _commandLine = new SourceItem { Parent = this, Tokens = Compiler.Tokens.Skip(_tokenOrig).ToList() };
                Refresh(si => si == start || si.Contains(ProgramIndex), hi => false);
            }
            catch (Exception ex)
            {
                Compiler.Tokens.SkipWhile(t => t != Compiler.ArgToken).ForEach(t => t.TokenType = TokenType.Error);
                _commandLine = new SourceItem { Parent = this, Tokens = Compiler.Tokens.Skip(_tokenOrig).ToList() };
                Refresh(si => false, hi => false);
                Status.Text = ex.Message;
                Status.Foreground = Brushes.Red;
            }
        }

        private void CommandLine_KeyUp(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Up || e.Key == Key.Down) && _commandLines.Any())
            {
                var index = _commandLines.IndexOf(CommandLine.Text);

                if (e.Key == Key.Up)
                {
                    CommandLine.Text = index < 0 ? _commandLines.Last() : _commandLines[Max(index - 1, 0)];
                }
                else
                {
                    CommandLine.Text = index < 0 || index + 1 >= _commandLines.Count ? "" : _commandLines[index + 1];
                }
            }
            else if (e.Key == Key.Return)
            {
                CommandLineRun_Click(null, null);
            }
        }

        private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
        }

        private void InputOutput_TextInput(object sender, TextCompositionEventArgs e)
        {
            e.Text.AsEnumerable().ForEach(c => Cpu.Input.Enqueue(c));
            Refresh(si => false, hi => false);
        }

    }
}
