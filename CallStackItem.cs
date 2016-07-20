using System.Linq;

namespace ForthCompiler
{
    public class CallStackItem : UiItem
    {
        public Structure Item { get; set; }
        public string Name => Item.Name.Split('.').Skip(1).First();
        public string AddressFormatted => Parent.Formatter(Item.Value);

        public void Refresh()
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(AddressFormatted));
        }
    }
}