using System.Windows;
using System.Windows.Input;
using TimeTrack.Utilities;

namespace TimeTrack.Views.Dialogs
{
    public partial class ShortcutInputDialog : Window
    {
        public Key SelectedKey { get; private set; }
        public ModifierKeys SelectedModifiers { get; private set; }

        public ShortcutInputDialog(KeyboardShortcut currentShortcut)
        {
            InitializeComponent();
            SelectedKey = currentShortcut.Key;
            SelectedModifiers = currentShortcut.Modifiers;
            UpdateDisplay();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Ignore modifier keys by themselves
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
                return;

            // When Alt is pressed, WPF reports e.Key as Key.System and the actual key is in e.SystemKey
            Key actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            
            SelectedKey = actualKey;
            SelectedModifiers = Keyboard.Modifiers;
            UpdateDisplay();

            e.Handled = true;
        }

        private void UpdateDisplay()
        {
            string display = "";
            if (SelectedModifiers.HasFlag(ModifierKeys.Control))
                display += "Ctrl+";
            if (SelectedModifiers.HasFlag(ModifierKeys.Alt))
                display += "Alt+";
            if (SelectedModifiers.HasFlag(ModifierKeys.Shift))
                display += "Shift+";
            if (SelectedModifiers.HasFlag(ModifierKeys.Windows))
                display += "Win+";

            display += SelectedKey.ToString();
            ShortcutTextBlock.Text = display;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}