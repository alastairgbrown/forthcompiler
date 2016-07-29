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

        public int MacroLevel { get; }
        public string File { get; }
        public int Y { get; }
        public int X { get; }
        public string Text { get; }
        public TokenType TokenType { get; set; }
        public CodeSlot CodeSlot { get; set; }
        public int CodeIndex { get; set; }
        public int CodeCount { get; set; }

        public bool IsExcluded => TokenType == TokenType.Excluded;

        public override string ToString()
        {
            return $"{File}({Y+1},{X+1}) : {Text} {TokenType} {CodeSlot} {CodeIndex} {CodeCount}";
        }
    }
}