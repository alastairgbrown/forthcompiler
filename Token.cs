using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ForthCompiler
{
    public class Token : ISlotRange
    {
        public Token(string text, string file, int y, int x, int macroLevel)
        {
            Text = text;
            File = file;
            Y = y;
            X = x;
            MacroLevel = macroLevel;
            TokenType = Regex.IsMatch(Text, @"^\s*$") ? TokenType.Excluded :
                         Regex.IsMatch(Text, @"^[#]?-?\d+$") ? TokenType.Literal :
                         Regex.IsMatch(Text, @"^[$][0-9a-fA-F]+$") ? TokenType.Literal :
                         Regex.IsMatch(Text, @"^[%][01]+$") ? TokenType.Literal : TokenType.Undetermined;
        }

        public int MacroLevel { get; set; }
        public string File { get; }
        public int Y { get; }
        public int X { get; }
        public string Text { get; set; }
        public TokenType TokenType { get; set; }
        public List<Token> Arguments { get; set; }
        public IDictEntry DictEntry { get; set; }
        public int CodeSlot { get; set; } = -1;
        public int CodeCount { get; set; }

        public string MethodName => (DictEntry as Method)?.MethodName;

        public override string ToString()
        {
            return $"{Text} {File}({Y+1},{X+1})";
        }

        public void SetError()
        {
            TokenType = TokenType.Error;
        }
    }
}