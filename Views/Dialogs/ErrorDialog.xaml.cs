using System.Windows;
using TimeTrack.Utilities;

namespace TimeTrack.Views.Dialogs
{
    public partial class ErrorDialog : Window
    {
        public ErrorDialog(string summary, string details)
        {
            InitializeComponent();
            ErrorSummary.Text = summary;
            ErrorDetails.Text = details;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText($"{ErrorSummary.Text}\n\n{ErrorDetails.Text}");
            }
            catch { }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
