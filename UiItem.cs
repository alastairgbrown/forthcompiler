using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Xml.Linq;
using System.Xml.XPath;
using ForthCompiler.Annotations;
using static System.Windows.Media.ColorConverter;

namespace ForthCompiler
{
    public abstract class UiItem : INotifyPropertyChanged
    {
        protected static readonly Dictionary<TokenType, Brush> TokenColors = new Dictionary<TokenType, Brush>();
        protected static readonly Dictionary<string, Brush> KeywordColors = new Dictionary<string, Brush>(StringComparer.OrdinalIgnoreCase);

        static UiItem()
        {
            try
            {
                var xml = XDocument.Load("4th.xml");
                var brushes = xml.XPathSelectElements("//WordsStyle").ToDictionary(
                        x => x.Attribute("name").Value,
                        x => new SolidColorBrush((Color)(ConvertFromString("#" + x.Attribute("fgColor").Value) ?? Colors.Black)),
                        StringComparer.OrdinalIgnoreCase);

                foreach (var keywordlist in xml.XPathSelectElements("//Keywords")
                                               .Where(x => x.Attribute("name").Value.StartsWith("Keywords") ||
                                                           x.Attribute("name").Value == "Operators1"))
                {
                    var name = keywordlist.Attribute("name").Value.Replace("Operators1", "Operators");

                    foreach (Match keyword in Regex.Matches(keywordlist.Value, @"\S+"))
                    {
                        KeywordColors[keyword.Value] = brushes.At(name) ?? Brushes.Black;
                    }
                }

                TokenColors[TokenType.Excluded] = brushes.At("COMMENTS") ?? Brushes.Green;
                TokenColors[TokenType.Literal] = brushes.At("NUMBERS") ?? Brushes.Magenta;
                TokenColors[TokenType.Constant] = KeywordColors.At("CONSTANT_IDENTIFIER") ?? Brushes.Magenta;
                TokenColors[TokenType.Variable] = KeywordColors.At("VARIABLE_IDENTIFIER") ?? Brushes.Magenta;
                TokenColors[TokenType.Definition] = KeywordColors.At("DEFINITION_IDENTIFIER") ?? Brushes.Magenta;
                TokenColors[TokenType.Error] = KeywordColors.At("ERROR_IDENTIFIER") ?? Brushes.Red;
            }
            catch (Exception)
            {
                // ignored, it not the end of the world if we don't have colored text
            }
        }
        public DebugWindow Parent { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}