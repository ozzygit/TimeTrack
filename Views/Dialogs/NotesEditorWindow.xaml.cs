using System.Windows;
using System.Windows.Input;
using TimeTrack.Utilities;

namespace TimeTrack.Views.Dialogs
{
    public partial class NotesEditorWindow : Window
    {
        public string? NotesText { get; set; }

        public NotesEditorWindow(string? initialText)
        {
            InitializeComponent();
            NotesText = initialText ?? string.Empty;
            DataContext = this;
            
            // Set initial character count
            UpdateCharacterCount();
            
            // Add keyboard shortcut for Ctrl+Enter to save
            PreviewKeyDown += NotesEditorWindow_PreviewKeyDown;
            
            // Focus the text box when window opens
            Loaded += (s, e) => NotesBox.Focus();
        }

        private void NotesEditorWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool isEnter = e.Key == Key.Enter || e.Key == Key.Return;
            if (!isEnter) return;

            var submit = SettingsManager.GetShortcut("Submit");
            Key submitKey = (submit != null && submit.Key != Key.None) ? submit.Key : Key.Enter;
            ModifierKeys submitMods = (submit != null && submit.Key != Key.None) ? submit.Modifiers : ModifierKeys.Control;

            bool keyMatch = e.Key == submitKey ||
                (submitKey == Key.Enter && e.Key == Key.Return) ||
                (submitKey == Key.Return && e.Key == Key.Enter);

            if (keyMatch && Keyboard.Modifiers == submitMods)
            {
                OnSave(sender, e);
                e.Handled = true;
            }
        }

        private void NotesBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateCharacterCount();
        }

        private void UpdateCharacterCount()
        {
            if (CharacterCount != null)
            {
                int count = NotesBox?.Text.Length ?? 0;
                CharacterCount.Text = count == 1 ? "1 character" : $"{count} characters";
            }
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OnClear(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all notes?",
                "Clear Notes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                NotesText = string.Empty;
                NotesBox.Text = string.Empty;
                UpdateCharacterCount();
                NotesBox.Focus();
            }
        }
    }
}
