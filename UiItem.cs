using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Xml.Linq;
using System.Xml.XPath;
using ForthCompiler.Annotations;
using static System.StringComparer;
using static System.Windows.Media.ColorConverter;

namespace ForthCompiler
{
    public abstract class UiItem : INotifyPropertyChanged
    {
        protected static readonly Dictionary<TokenType, Brush> TokenColors = new Dictionary<TokenType, Brush>();
        protected static readonly Dictionary<string, Brush> KeywordColors = new Dictionary<string, Brush>(OrdinalIgnoreCase);

        static UiItem()
        {
            try
            {
                var xml = XDocument.Parse("ForthColoring.xml".LoadFileOrResource());
                var brushes = xml.XPathSelectElements("//WordsStyle").ToDictionary(
                        x => x.Attribute("name").Value,
                        x => new SolidColorBrush((Color)(ConvertFromString("#" + x.Attribute("fgColor").Value) ?? Colors.Black)),
                        OrdinalIgnoreCase);
                var regex = new Regex(@"(Keywords\d)|(Operators)1|(Folder)s( in \w+)");

                foreach (var item in xml.XPathSelectElements("//Keywords")
                                        .Select(x => new { regex.Match(x.Attribute("name").Value).Groups, x.Value })
                                        .Where(x => x.Groups[0].Success))
                {
                    var name = string.Join(null, item.Groups.OfType<Group>().Skip(1));

                    foreach (Match keyword in Regex.Matches(item.Value, @"\S+"))
                    {
                        KeywordColors[keyword.Value] = brushes.At(name) ?? Brushes.Black;
                    }
                }

                TokenColors[TokenType.Excluded] = brushes.At("COMMENTS") ?? Brushes.Green;
                TokenColors[TokenType.Literal] = brushes.At("NUMBERS") ?? Brushes.Magenta;
                TokenColors[TokenType.Constant] = KeywordColors.At("CONSTANT_IDENTIFIER") ?? Brushes.Black;
                TokenColors[TokenType.Variable] = KeywordColors.At("VARIABLE_IDENTIFIER") ?? Brushes.Black;
                TokenColors[TokenType.Definition] = KeywordColors.At("DEFINITION_IDENTIFIER") ?? Brushes.Black;
                TokenColors[TokenType.Error] = KeywordColors.At("ERROR_IDENTIFIER") ?? Brushes.Red;
            }
            catch
            {
                // ignored, it's not the end of the world if we don't have colored text
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