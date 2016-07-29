using System.Windows.Input;

namespace ForthCompiler
{
    public static class Shortcuts
    {
        public static RoutedCommand Run = new RoutedCommand();
        public static RoutedCommand Restart = new RoutedCommand();
        public static RoutedCommand StepOver = new RoutedCommand();
        public static RoutedCommand StepInto = new RoutedCommand();
        public static RoutedCommand StepOut = new RoutedCommand();
        public static RoutedCommand Execute = new RoutedCommand();

        static Shortcuts()
        {
            Run.InputGestures.Add(new KeyGesture(Key.F5, ModifierKeys.None));
            Restart.InputGestures.Add(new KeyGesture(Key.F5, ModifierKeys.Shift));
            StepOver.InputGestures.Add(new KeyGesture(Key.F10, ModifierKeys.None));
            StepInto.InputGestures.Add(new KeyGesture(Key.F11, ModifierKeys.None));
            StepOut.InputGestures.Add(new KeyGesture(Key.F11, ModifierKeys.Shift));
            Execute.InputGestures.Add(new KeyGesture(Key.Enter, ModifierKeys.None));
        }
    }
}