using System.Windows;
using TimeTrack.Utilities;

namespace TimeTrack.Views.Dialogs
{
    public partial class WelcomeDialog : Window
    {
        public enum WelcomeResult
        {
            EnableAll,
            KeepSimple,
            Customise,
            Closed
        }

        public WelcomeResult Result { get; private set; } = WelcomeResult.Closed;

        public WelcomeDialog()
        {
            InitializeComponent();
        }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            Result = WelcomeResult.EnableAll;
            DialogResult = true;
            Close();
        }

        private void No_Click(object sender, RoutedEventArgs e)
        {
            Result = WelcomeResult.KeepSimple;
            DialogResult = true;
            Close();
        }

        private void Customise_Click(object sender, RoutedEventArgs e)
        {
            Result = WelcomeResult.Customise;
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Result = WelcomeResult.Closed;
            DialogResult = false;
            Close();
        }
    }
}
