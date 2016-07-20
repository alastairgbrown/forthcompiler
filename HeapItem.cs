using System.Windows.Media;

namespace ForthCompiler
{
    public class HeapItem : UiItem
    {
        public string Name { get; set; }

        public int Address { get; set; }

        public string AddressFormatted => Parent.Formatter(Address);

        public string Value => Parent.Formatter(Parent.Cpu.Heap.Entry(Address));

        public Brush ValueForeground => IsChanged ? Brushes.Red : Brushes.Black;

        public Brush NameForeground => TokenColors[TokenType.Variable];

        public bool IsChanged => Parent.Cpu.Heap.Entry(Address) != Parent.Cpu.LastHeap.Entry(Address);

        public bool WasChanged { get; set; }

        public void Refresh()
        {
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(AddressFormatted));
            OnPropertyChanged(nameof(ValueForeground));
        }
    }
}