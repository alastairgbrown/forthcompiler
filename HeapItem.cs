using System.Windows.Media;

namespace ForthCompiler
{
    public class HeapItem : UiItem
    {
        public string Name { get; set; }

        public int Address { get; set; }

        public bool WasChanged { get; set; }

        public string AddressFormatted => Parent.FormatNumber(Address);

        public string Value => Parent.FormatNumber(Parent.Cpu.Heap.At(Address));

        public Brush ValueForeground => IsChanged ? Brushes.Red : Brushes.Black;

        public Brush NameForeground => (SyntaxStyle.Tokens.At(TokenType.Variable) ?? SyntaxStyle.Default).Foreground;

        public bool IsChanged => Parent.Cpu.Heap.At(Address) != Parent.Cpu.LastHeap.At(Address);

        public void Refresh()
        {
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(AddressFormatted));
            OnPropertyChanged(nameof(ValueForeground));
        }
    }
}