using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ForthCompiler
{
    public class SourceItem : UiItem, ISlotRange
    {
        public string TestResult { get; set; }

        public Compiler Compiler => Parent.Compiler;
        public List<Token> Tokens { get; set; } = new List<Token>();
        public int CodeIndex => Tokens.FirstOrDefault()?.CodeIndex ?? 0;
        public int CodeCount => (Tokens.LastOrDefault()?.CodeIndex - 
                                 Tokens.FirstOrDefault()?.CodeIndex + Tokens.LastOrDefault()?.CodeCount) ?? 0;

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

        public TextBlock Text
        {
            get
            {
                var block = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 1000, MinWidth = 1000 };

                foreach (var token in DisplayTokens)
                {
                    var current = token.Contains(Parent.ProgramIndex);
                    var style = SyntaxStyle.Keywords.At(token.Text) ?? SyntaxStyle.Tokens.At(token.TokenType) ?? SyntaxStyle.Default;
                    var ci = token.CodeIndex;

                    block.Inlines.Add(new Run
                    {
                        Text = token.Text,
                        Background = current ? Brushes.DarkGoldenrod : Brushes.Transparent,
                        Foreground = current ? Brushes.Black : style.Foreground,
                        FontWeight = style.FontWeight,
                        FontStyle = style.FontStyle,
                        TextDecorations = style?.TextDecoration,
                        ToolTip = $"{Parent.FormatAddress(ci)} {token.CodeCount}"
                    });

                    for (var i = token.CodeIndex; Parent.ShowAsm.IsChecked && i < token.CodeIndex + token.CodeCount; i++)
                    {
                        var codeslot = Parent.Compiler.CodeSlots[i];

                        if (codeslot == null)
                            continue;

                        current = Parent.ProgramIndex == i;

                        block.Inlines.Add(new Run
                        {
                            Text = $" {codeslot.Code}" +
                                   $"{(codeslot.Code == Code.Literal || codeslot.Code == Code.Address || codeslot.Code == Code.Label ? " " + Parent.FormatNumber(codeslot.Value) : null)}" +
                                   $"{codeslot.Label}",
                            Background = current ? Brushes.DarkGoldenrod : Brushes.Transparent,
                            Foreground = current ? Brushes.Black : Brushes.DarkGray,
                            ToolTip = Parent.FormatAddress(i)
                        });
                    }
                }

                return block;
            }
        }

        public string Address => $"{Parent.FormatAddress(CodeIndex)}";
        public Visibility ShowAddress => Parent.ShowAddress.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ShowPass => TestResult?.StartsWith("PASS") == true ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ShowFail => TestResult?.StartsWith("FAIL") == true ? Visibility.Visible : Visibility.Collapsed;
        public bool IsTestCase => string.Join(null, Tokens.Take(4).Select(t => t.Text)).IsEqual("( TestCase ");
        public Brush Background => this.Contains(Parent.ProgramIndex) ? Brushes.Yellow : Brushes.Transparent;

        public void Refresh()
        {
            OnPropertyChanged(nameof(ShowAddress));
            OnPropertyChanged(nameof(ShowPass));
            OnPropertyChanged(nameof(ShowFail));
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(Background));
            OnPropertyChanged(nameof(Address));
            OnPropertyChanged(nameof(Tooltip));
            OnPropertyChanged(nameof(TestResult));
        }

        public string Tooltip => $"{Tokens.First().File}({Tokens.First().Y + 1})";
    }
}