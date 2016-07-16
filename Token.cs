using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ForthCompiler
{
    public class Token : ISlotRange
    {
        private TokenType? _tokenType;

        public Token(string text, string file, int y, int x, int macroLevel)
        {
            Text = text;
            File = file;
            Y = y;
            X = x;
            MacroLevel = macroLevel;
            _tokenType = Regex.IsMatch(Text, @"^\s*$") ? TokenType.Excluded :
                         Regex.IsMatch(Text, @"^[#]?-?\d+$") ? TokenType.Literal :
                         Regex.IsMatch(Text, @"^[$][0-9a-fA-F]+$") ? TokenType.Literal :
                         Regex.IsMatch(Text, @"^[%][01]+$") ? TokenType.Literal : (TokenType?)null;
        }

        public int MacroLevel { get; set; }
        public string File { get; }
        public int Y { get; }
        public int X { get; }
        public string Text { get; set; }
        public List<Token> Arguments { get; set; }
        public IDictEntry DictEntry { get; set; }
        public int CodeSlot { get; set; } = -1;
        public int CodeCount { get; set; }

        public string MethodName => (DictEntry as Method)?.MethodName;
        public TokenType TokenType => _tokenType ?? DictEntry?.TokenType ?? TokenType.Undetermined;

        public override string ToString()
        {
            return $"{Text} {File}({Y+1},{X+1})";
        }

        public void SetError()
        {
            _tokenType = TokenType.Error;
        }
    }
}