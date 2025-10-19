using System.Windows;

namespace TimeTrack
{
    public partial class NotesEditorWindow : Window
    {
        public string? NotesText { get; set; }

        public NotesEditorWindow(string? initialText)
        {
            InitializeComponent();
            NotesText = initialText ?? string.Empty;
            DataContext = this;
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
