using System.Windows;
using System.Windows.Input;

namespace TimeTrack
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

            SelectedKey = e.Key;
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