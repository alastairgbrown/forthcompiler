using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ForthCompiler.Annotations;

namespace ForthCompiler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class DebugWindow
    {
        private readonly bool _test;
        private int[] _originalCodeCounts;
        private int _macroLevel;
        public Compiler Compiler { get; }

        public Cpu Cpu { get; set; }

        private ObservableCollection<SourceItem> SourceItems { get; } = new ObservableCollection<SourceItem>();
        private ObservableCollection<HeapItem> HeapItems { get; } = new ObservableCollection<HeapItem>();

        public DebugWindow(Compiler compiler, bool test)
        {
            InitializeComponent();

            _test = test;
            _originalCodeCounts = compiler.Tokens.Select(t => t.CodeCount).ToArray();

            Compiler = compiler;


            LoadSource();

            for (int i = 0; i < Compiler.HeapSize; i++)
            {
                HeapItems.Add(new HeapItem { Address = i, Parent = this, Name = compiler.Dict.FirstOrDefault(t => (t.Value as VariableEntry)?.HeapAddress == i).Key });
            }

            HeapListBox.ItemsSource = HeapItems;
            SourceListBox.ItemsSource = SourceItems;

            RestartButton_Click(null, null);

            if (compiler.Error != null)
            {
                var textBlock = new TextBlock();

                textBlock.Inlines.Add(new Run { Text = $"{Compiler.Error}", Foreground = Brushes.Red });

                CpuLabel.Content = textBlock;

            }
            else if (test)
            {
                //RunButton_Click(null, null);
                //CheckTestButton_Click(null, null);
            }
        }

        private void LoadSource()
        {
            SourceItems.Clear();
            for (int i = 0; i < Compiler.Tokens.Count; i++)
            {
                var token = Compiler.Tokens[i];

                token.CodeCount = _originalCodeCounts[i];

                if (i == 0 || Compiler.Tokens[i - 1].File != token.File || Compiler.Tokens[i - 1].Y != token.Y)
                {
                    SourceItems.Add(new SourceItem { Parent = this });
                }

                if (token.MacroLevel > _macroLevel)
                {
                    SourceItems.Last().Tokens.Last().CodeCount += token.CodeCount;
                }
                else
                {
                    SourceItems.Last().Tokens.Add(token);
                }
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
        }

        private void Run(Func<int, bool?> continueCondition)
        {
            var linebefore = SourceItems.FirstOrDefault(t => t.Contains(Cpu.ProgramSlot));

            Cpu.Run(continueCondition);

            var lineafter = SourceItems.FirstOrDefault(t => t.Contains(Cpu.ProgramSlot));

            Refresh(si => si == linebefore || si == lineafter, hi => hi.IsChanged);
        }


        private void StepAsmButton_Click(object sender, RoutedEventArgs e)
        {
            Run(i => i == 0);
        }

        private void StepTokenButton_Click(object sender, RoutedEventArgs e)
        {
            var token = Compiler.Tokens.FirstOrDefault(t => t.Contains(Cpu.ProgramSlot));

            Run(i => token?.Contains(Cpu.ProgramSlot));
        }

        private void StepLineButton_Click(object sender, RoutedEventArgs e)
        {
            var line = SourceItems.FirstOrDefault(t => t.Contains(Cpu.ProgramSlot));

            Run(i => line?.Contains(Cpu.ProgramSlot));
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            var breaks = new HashSet<int>(SourceItems.Where(i => i.Break).Select(i => i.CodeSlot));

            Run(i => i == 0 || !breaks.Contains(Cpu.ProgramSlot));
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            Cpu = new Cpu(Compiler);

            Refresh(si => true, hi => true);
        }

        private void RunTestsButton_Click(object sender, RoutedEventArgs e)
        {
            var tests = SourceItems.Any(si => si.Break) ? SourceItems.Where(si => si.Break).ToArray() : SourceItems.ToArray();
            var results = new Dictionary<string, int> { { "PASS", 0 }, { "FAIL", 0 } };

            foreach (var test in tests)
            {
                Cpu = new Cpu(Compiler) { ProgramSlot = test.CodeSlot };
                Cpu.Run(i => test.Contains(Cpu.ProgramSlot));

                var stack = Cpu.ForthStack.Reverse().ToArray();
                var result = "FAIL";

                if (stack.Length > 0 && stack.Length == 1 + stack.First() * 2)
                {
                    var actual = string.Join(" ", stack.Skip(1).Take(stack.First()));
                    var expected = string.Join(" ", stack.Skip(1 + stack.First()).Take(stack.First()));
                    result = actual == expected ? "PASS" : "FAIL";
                    test.TestResult = $"{result} expected={expected} actual={actual}";
                }
                else
                {
                    test.TestResult = $"{result} stack={string.Join(" ", stack)}";
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
            LoadSource();
            Refresh(si => true, hi => true);
        }

        private void MacroLevel_Click(object sender, RoutedEventArgs e)
        {
            var menuItems = View.Items.OfType<Control>().TakeWhile(c => c is MenuItem).OfType<MenuItem>().ToList();

            _macroLevel = menuItems.IndexOf((MenuItem)sender);
            menuItems.ForEach(mi => mi.IsChecked = ReferenceEquals(mi, sender));

            LoadSource();
            Refresh(si => true, hi => true);
        }
    }

    public class Item : INotifyPropertyChanged
    {
        public DebugWindow Parent { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class HeapItem : Item
    {
        public string Name { get; set; }

        public int Address { get; set; }

        public int Value => Parent.Cpu.Heap[Address];

        public Brush Foreground => IsChanged ? Brushes.Red : Brushes.Black;

        public bool IsChanged => Parent.Cpu.Heap[Address] != Parent.Cpu.LastHeap[Address];

        public void Refresh()
        {
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(Foreground));
        }
    }

    public class SourceItem : Item, ISlotRange
    {
        private static readonly Dictionary<TokenType, Brush> TokenColors = new Dictionary<TokenType, Brush>
            {
                {TokenType.Undetermined, Brushes.Black},
                {TokenType.Organisation, Brushes.Black},
                {TokenType.Excluded, Brushes.Green},
                {TokenType.Literal, Brushes.Black},
                {TokenType.Math, Brushes.Black},
                {TokenType.Stack, Brushes.Black},
                {TokenType.Structure, Brushes.Blue},
                {TokenType.Variable, Brushes.Magenta},
                {TokenType.Constant, Brushes.Black},
                {TokenType.Definition, Brushes.Black},
                {TokenType.Label, Brushes.Black},
                {TokenType.Error, Brushes.Red},
            };

        public bool Break { get; set; }

        public TextBlock Text
        {
            get
            {
                var block = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 1000, MinWidth = 1000 };

                foreach (var token in Tokens)
                {
                    bool current = token.Contains(Parent.Cpu.ProgramSlot);

                    block.Inlines.Add(new Run
                    {
                        Text = token.Text,
                        Foreground = TokenColors[token.TokenType],
                        Background = current ? Brushes.LightGray : Brushes.Transparent,
                    });

                    for (int i = 0, slot = token.CodeSlot; Parent.ShowAsm.IsChecked && i < token.CodeCount; i++, slot++)
                    {
                        var codeslot = Parent.Compiler.CodeSlots[slot];

                        if (codeslot == null)
                            continue;

                        current = Parent.Cpu.ProgramSlot == slot;

                        block.Inlines.Add(new Run
                        {
                            Text = $" {codeslot}",
                            Background = current ? Brushes.LightGray : Brushes.Transparent,
                            ToolTip = $"{slot:X}"
                        });
                    }
                }

                return block;
            }
        }

        public string Address => $"{Tokens.First().CodeSlot:X}";
        public Visibility ShowAddress => Parent.ShowAddress.IsChecked ? Visibility.Visible : Visibility.Collapsed;

        public Brush Background => this.Contains(Parent.Cpu.ProgramSlot) ? Brushes.Yellow :
                                    TestResult == null ? Brushes.Transparent :
                                    TestResult.StartsWith("PASS") ? Brushes.LightGreen : Brushes.LightPink;


        public void Refresh()
        {
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(Code));
            OnPropertyChanged(nameof(Background));
            OnPropertyChanged(nameof(Tooltip));
        }

        public string Tooltip => TestResult;

        public string TestResult { get; set; }

        public Compiler Compiler => Parent.Compiler;
        public List<Token> Tokens = new List<Token>();
        public int CodeSlot => Tokens.First().CodeSlot;
        public int CodeCount => Tokens.Last().CodeSlot - Tokens.First().CodeSlot + Tokens.Last().CodeCount;
    }
}
