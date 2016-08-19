using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using System.Xml.XPath;
using static System.StringComparer;
using static System.Windows.Media.ColorConverter;

namespace ForthCompiler
{
    public class SyntaxStyle
    {
        public static Dictionary<TokenType, SyntaxStyle> Tokens { get; } = new Dictionary<TokenType, SyntaxStyle>();
        public static Dictionary<string, SyntaxStyle> Keywords { get; } = new Dictionary<string, SyntaxStyle>(OrdinalIgnoreCase);

        public static readonly SyntaxStyle Default = new SyntaxStyle
        {
            Foreground = Brushes.Black,
            FontWeight = FontWeights.Normal,
            FontStyle = FontStyles.Normal
        };

        static SyntaxStyle()
        {
            try
            {
                var xml = XDocument.Parse("ForthColoring.xml".LoadText());
                var styles = xml.XPathSelectElements("//WordsStyle").ToDictionary(
                        x => x.Attribute("name").Value,
                        x => new SyntaxStyle
                        {
                            Foreground = new SolidColorBrush((Color)(ConvertFromString("#" + x.Attribute("fgColor").Value) ?? Colors.Black)),
                            FontWeight = (int.Parse(x.Attribute("fontStyle").Value) & 1) == 1 ? FontWeights.Black : FontWeights.Normal,
                            FontStyle = (int.Parse(x.Attribute("fontStyle").Value) & 2) == 2 ? FontStyles.Italic : FontStyles.Normal,
                            TextDecoration = (int.Parse(x.Attribute("fontStyle").Value) & 4) == 4 ? TextDecorations.Underline : null,
                        },
                        OrdinalIgnoreCase);
                var regex = new Regex(@"(Keywords\d)|(Operators)\d|(Folder)s( in \w+)");

                foreach (var item in xml.XPathSelectElements("//Keywords")
                                        .Select(kw => new { regex.Match(kw.Attribute("name").Value).Groups, kw.Value })
                                        .Where(kw => kw.Groups[0].Success))
                {
                    var name = string.Join(null, item.Groups.OfType<Group>().Skip(1));

                    foreach (Match keyword in Regex.Matches(item.Value, @"\S+"))
                    {
                        Keywords[keyword.Value] = styles.At(name) ?? Brushes.Black;
                    }
                }

                Tokens[TokenType.Excluded] = styles.At("COMMENTS") ?? Brushes.Green;
                Tokens[TokenType.Literal] = styles.At("NUMBERS") ?? Brushes.Magenta;
                Tokens[TokenType.Constant] = Keywords.At("CONSTANT_IDENTIFIER") ?? Brushes.Black;
                Tokens[TokenType.Variable] = Keywords.At("VARIABLE_IDENTIFIER") ?? Brushes.Black;
                Tokens[TokenType.Definition] = Keywords.At("DEFINITION_IDENTIFIER") ?? Brushes.Black;
                Tokens[TokenType.Error] = Keywords.At("ERROR_IDENTIFIER") ?? Brushes.Red;
            }
            catch
            {
                // ignored, it's not the end of the world if we don't have colored text
            }
        }

        public Brush Foreground { get; set; }
        public FontWeight FontWeight { get; set; }
        public FontStyle FontStyle { get; set; }
        public TextDecorationCollection TextDecoration { get; set; }


        public static implicit operator SyntaxStyle(Brush brush)
        {
            return new SyntaxStyle { Foreground = brush };
        }
    }
}