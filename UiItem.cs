using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForthCompiler.Annotations;

namespace ForthCompiler
{
    public class UiItem : INotifyPropertyChanged
    {
        public DebugWindow Parent { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}