using System.Windows.Media;

namespace ForthCompiler
{
    public class HeapItem : UiItem
    {
        public string Name { get; set; }

        public int Address { get; set; }

        public string AddressFormatted => Parent.Formatter(Address);

        public string Value => Parent.Formatter(Parent.Cpu.Heap[Address]);

        public Brush Foreground => IsChanged ? Brushes.Red : Brushes.Black;

        public bool IsChanged => Parent.Cpu.Heap[Address] != Parent.Cpu.LastHeap[Address];

        public void Refresh()
        {
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(AddressFormatted));
            OnPropertyChanged(nameof(Foreground));
        }
    }
}