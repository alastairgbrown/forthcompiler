using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ForthCompiler
{
    public class Token : ISlotRange
    {
        private TokenType? _tokenType;

        public Token(string text, string file, int y, int x, bool isMacro)
        {
            Text = text;
            File = file;
            Y = y;
            X = x;
            IsMacro = isMacro;
            _tokenType = Regex.IsMatch(Text, @"^(\s)") ? TokenType.Excluded :
                         Regex.IsMatch(Text, @"^-?\d+$") ? TokenType.Literal : (TokenType?)null;
        }

        public bool IsMacro { get; set; }

        public string File { get; }
        public int Y { get; }
        public int X { get; }
        public string Text { get; set; }
        public IDictEntry DictEntry { get; set; }
 
        public string MethodName => DictEntry.Method.Name;
        public TokenType TokenType => _tokenType ?? DictEntry?.TokenType ?? TokenType.Undetermined;

        public override string ToString()
        {
            return MethodName == nameof(Compiler.DefinitionStart) ? $"_{Text}" : $"_{Text}_{CodeSlot:X}";
        }

        public int CodeSlot { get; set; } = -1;
        public int CodeCount { get; set; }
        public int Value { get; set; }

        public void SetError()
        {
            _tokenType = TokenType.Error;
        }
    }
}