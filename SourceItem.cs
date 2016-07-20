using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ForthCompiler
{
    public class SourceItem : UiItem, ISlotRange
    {

        private int[] _originalCodeCounts;

        public bool Break { get; set; }

        public List<Token> DisplayTokens
        {
            get
            {
                var tokens = new List<Token>();

                _originalCodeCounts = _originalCodeCounts ?? Tokens.Select(t => t.CodeCount).ToArray();

                for (int i = 0; i < Tokens.Count; i++)
                {
                    Tokens[i].CodeCount = _originalCodeCounts[i];

                    if (Tokens[i].MacroLevel > Parent.MacroLevel)
                    {
                        tokens.Last().CodeCount += Tokens[i].CodeCount;
                    }
                    else
                    {
                        tokens.Add(Tokens[i]);
                    }
                }

                return tokens;
            }
        }

        public bool IsTestCase => string.Join(null, Tokens.Take(6).Select(t => t.Text)).IsEqual("( Test Case ");

        public TextBlock Text
        {
            get
            {
                var block = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 1000, MinWidth = 1000 };

                foreach (var token in DisplayTokens)
                {
                    bool current = token.Contains(Parent.ProgramSlot);

                    block.Inlines.Add(new Run
                    {
                        Text = token.Text,
                        Foreground = TokenColors.ContainsKey(token.TokenType) ? TokenColors[token.TokenType] :
                                        KeywordColors.ContainsKey(token.Text) ? KeywordColors[token.Text] : Brushes.Black,
                        Background = current ? Brushes.LightGray : Brushes.Transparent,
                        ToolTip = Parent.Formatter(token.CodeSlot)
                    });

                    for (var i = token.CodeSlot; Parent.ShowAsm.IsChecked && i < token.CodeSlot + token.CodeCount; i++)
                    {
                        var codeslot = Parent.Compiler.CodeSlots[i];

                        if (codeslot == null)
                            continue;

                        current = Parent.ProgramSlot == i;

                        block.Inlines.Add(new Run
                        {
                            Text = $" {codeslot.Code}{(codeslot.Code == Code.Lit ? " " + Parent.Formatter(codeslot.Value) : null)}",
                            Foreground = current ? Brushes.Black : Brushes.DarkGray,
                            Background = current ? Brushes.LightGray : Brushes.Transparent,
                            ToolTip = Parent.Formatter(i)
                        });
                    }
                }

                return block;
            }
        }

        public string Address => Parent.Formatter(Tokens.First().CodeSlot);

        public Visibility ShowAddress => Parent.ShowAddress.IsChecked ? Visibility.Visible : Visibility.Collapsed;

        public Brush Background => this.Contains(Parent.Cpu.ProgramSlot)
                                        ? Brushes.Yellow
                                        : TestResult == null
                                            ? Brushes.Transparent
                                            : TestResult.StartsWith("PASS")
                                                ? Brushes.LightGreen
                                                : Brushes.LightPink;


        public void Refresh()
        {
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(Background));
            OnPropertyChanged(nameof(Address));
            OnPropertyChanged(nameof(ShowAddress));
            OnPropertyChanged(nameof(Tooltip));
        }

        public string Tooltip => $"{TestResult} {Tokens.First().File}({Tokens.First().Y + 1})";

        public string TestResult { get; set; }

        public Compiler Compiler => Parent.Compiler;
        public List<Token> Tokens { get; } = new List<Token>();
        public int CodeSlot => Tokens.First().CodeSlot;
        public int CodeCount => Tokens.Last().CodeSlot - Tokens.First().CodeSlot + Tokens.Last().CodeCount;
    }
}