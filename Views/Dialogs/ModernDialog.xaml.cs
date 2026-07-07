using System.Windows;
using System.Windows.Controls;

namespace TimeTrack.Views.Dialogs
{
    public partial class ModernDialog : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private ModernDialog(string title, string message, MessageBoxButton buttons, MessageBoxImage icon)
        {
            InitializeComponent();

            TitleText.Text = title;
            MessageText.Text = message;

            var (iconChar, iconColor) = icon switch
            {
                MessageBoxImage.Error => ("\uE783", "#E81123"),
                MessageBoxImage.Warning => ("\uE7BA", "#FF8C00"),
                MessageBoxImage.Question => ("\uE9CE", "#0078D4"),
                MessageBoxImage.Information => ("\uE946", "#0078D4"),
                _ => ("\uE946", "#0078D4")
            };
            TitleIcon.Text = iconChar;
            TitleIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(iconColor));

            CreateButtons(buttons);
        }

        private void CreateButtons(MessageBoxButton buttons)
        {
            switch (buttons)
            {
                case MessageBoxButton.OK:
                    AddButton("OK", MessageBoxResult.OK, true, true);
                    break;
                case MessageBoxButton.OKCancel:
                    AddButton("OK", MessageBoxResult.OK, true, true);
                    AddButton("Cancel", MessageBoxResult.Cancel, false, false);
                    break;
                case MessageBoxButton.YesNo:
                    AddButton("Yes", MessageBoxResult.Yes, true, true);
                    AddButton("No", MessageBoxResult.No, false, false);
                    break;
                case MessageBoxButton.YesNoCancel:
                    AddButton("Yes", MessageBoxResult.Yes, true, true);
                    AddButton("No", MessageBoxResult.No, false, false);
                    AddButton("Cancel", MessageBoxResult.Cancel, false, false);
                    break;
            }
        }

        private void AddButton(string text, MessageBoxResult result, bool isPrimary, bool isDefault)
        {
            var btn = new Button
            {
                Content = text,
                Margin = new Thickness(0, 0, ButtonPanel.Children.Count > 0 ? 8 : 0, 0),
                IsDefault = isDefault
            };

            if (isPrimary)
                btn.Style = (Style)FindResource("PrimaryButton");

            btn.Click += (s, e) =>
            {
                Result = result;
                DialogResult = result != MessageBoxResult.Cancel;
                Close();
            };

            ButtonPanel.Children.Add(btn);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            DialogResult = false;
            Close();
        }

        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information)
        {
            var dialog = new ModernDialog(title, message, buttons, icon);
            dialog.ShowDialog();
            return dialog.Result;
        }

        public static void ShowInfo(string message, string title = "TimeTrack")
        {
            Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void ShowWarning(string message, string title = "TimeTrack")
        {
            Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static void ShowError(string message, string title = "TimeTrack")
        {
            Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static bool Confirm(string message, string title = "Confirm")
        {
            return Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }
    }
}
