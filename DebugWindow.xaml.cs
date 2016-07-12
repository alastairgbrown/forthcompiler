using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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
        private bool _hideMacros = true;
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

                if (token.IsMacro && _hideMacros)
                {
                    SourceItems.Last().Tokens.Last().CodeCount += token.CodeCount;
                }
                else
                {
                    SourceItems.Last().Tokens.Add(token);
                }
            }
        }



        private void Refresh()
        {
            var lastState = Cpu.LastState ?? Cpu.ThisState;
            var thisState = Cpu.ThisState;
            var textBlock = new TextBlock();

            for (var i = 0; i < thisState.Length; i++)
            {
                textBlock.Inlines.Add(new Run { Text = thisState[i], Foreground = thisState[i] == lastState[i] ? Brushes.Black : Brushes.Red });
            }

            CpuLabel.Content = textBlock;

            foreach (var item in SourceItems)
            {
                item.Refresh();
            }

            foreach (var item in HeapItems)
            {
                item.Refresh();
            }
        }



        private void StepAsmButton_Click(object sender, RoutedEventArgs e)
        {
            Cpu.Run(i => i == 0);
            Refresh();
        }

        private void StepTokenButton_Click(object sender, RoutedEventArgs e)
        {
            var token = Compiler.Tokens.FirstOrDefault(t => t.Contains(Cpu.ProgramSlot));

            Cpu.Run(i => token?.Contains(Cpu.ProgramSlot));
            Refresh();
        }

        private void StepLineButton_Click(object sender, RoutedEventArgs e)
        {
            var item = SourceItems.FirstOrDefault(t => t.Contains(Cpu.ProgramSlot));

            Cpu.Run(i => item?.Contains(Cpu.ProgramSlot));
            Refresh();
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            var breaks = new HashSet<int>(SourceItems.Where(i => i.Break).Select(i => i.CodeSlot));

            Cpu.Run(i => i == 0 || !breaks.Contains(Cpu.ProgramSlot));
            Refresh();
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            Cpu = new Cpu(Compiler);
            Refresh();
        }

        private void CheckTestButton_Click(object sender, RoutedEventArgs e)
        {
            var results = Cpu.ForthStack.Reverse().ToArray();
            int success = 0, fail = 0, invalid = 0;

            SourceItems.ToList().ForEach(si => si.TestResult = null);

            for (int i = 0, count; i < results.Length; i += count)
            {
                count = results[i];

                if (count < 2 || i + count > results.Length || (count % 2) != 0)
                {
                    invalid++;
                    break;
                }

                int line = results[i + 1], items = count / 2 - 1;

                if (Enumerable.Range(i + 2, items).Any(x => results[x] != results[x + items]))
                {
                    fail++;
                    SourceItems[line - 1].TestResult = $"FAIL {string.Join(" ", results.Skip(i + 2).Take(count - 2))}";

                }
                else
                {
                    success++;
                    SourceItems[line - 1].TestResult = $"SUCCESS {string.Join(" ", results.Skip(i + 2).Take(count - 2))}";
                }
            }

            Refresh();

            var textBlock = new TextBlock();

            textBlock.Inlines.Add(new Run { Text = $"{ success } lines succeeded, { fail } lines failed, {invalid} format failures" });

            CpuLabel.Content = textBlock;
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

        private void HideShowMacro_Click(object sender, RoutedEventArgs e)
        {
            _hideMacros = !_hideMacros;
            LoadSource();
            Refresh();
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

        public Brush Foreground => Parent.Cpu.Heap[Address] == Parent.Cpu.LastHeap[Address] ? Brushes.Black : Brushes.Red;

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
                var block = new TextBlock();
                var count = 0;

                foreach (var token in Tokens)
                {
                    bool current = token.Contains(Parent.Cpu.ProgramSlot);

                    block.Inlines.Add(new Run
                    {
                        Text = token.Text + (count > 80 ? Environment.NewLine : string.Empty),
                        Foreground = TokenColors[token.TokenType],
                        Background = current ? Brushes.LightGray : Brushes.Transparent,
                    });

                    count = count > 80 ? 0 : count + block.Inlines.OfType<Run>().Last().Text.Length;
                }

                return block;
            }
        }

        public string Address => $"{Tokens.First().CodeSlot:X}";

        public TextBlock Code
        {
            get
            {
                var block = new TextBlock();
                var count = 0;

                for (int i = 0, slot = CodeSlot; i < CodeCount; i++, slot++)
                {
                    var current = Parent.Cpu.ProgramSlot == slot;
                    var codeslot = Parent.Compiler.CodeSlots[slot];

                    if (codeslot == null)
                        continue;
                    
                    block.Inlines.Add(new Run
                    {
                        Text = codeslot + " " + (count > 80 ? Environment.NewLine : string.Empty),
                        Background = current ? Brushes.LightGray : Brushes.Transparent,
                        ToolTip = $"{slot:X}"
                    });

                    count = count > 80 ? 0 : count + block.Inlines.OfType<Run>().Last().Text.Length;
                }

                return block;
            }
        }

        public Brush Background => this.Contains(Parent.Cpu.ProgramSlot) ? Brushes.Yellow :
                                    TestResult == null ? Brushes.Transparent :
                                    TestResult.StartsWith("FAIL") ? Brushes.LightPink : Brushes.LightGreen;


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
