using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TimeTrack.Data;
using TimeTrack.Utilities;

namespace TimeTrack.Views.Dialogs
{
    public partial class EditEntryWindow : Window
    {
        public EditEntryWindow(TimeEntry entry)
        {
            InitializeComponent();
            DataContext = entry;

            // Add keyboard shortcut for Ctrl+Enter to save
            PreviewKeyDown += EditEntryWindow_PreviewKeyDown;

            // Focus the first text box when window opens
            Loaded += (s, e) => StartTimeBox.Focus();
        }

        private void EditEntryWindow_PreviewKeyDown(object sender, KeyEventArgs e)
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
                OnSaveClick(sender, e);
                e.Handled = true;
            }
        }

        private void NotesBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int count = NotesBox.Text.Length;
            NotesCharacterCount.Text = count == 1 ? "1 character" : $"{count} characters";
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            // Validation
            if (DataContext is TimeEntry te)
            {
                // Check if both times are present
                if (!te.StartTime.HasValue || !te.EndTime.HasValue)
                {
                    MessageBox.Show(
                        "Both start and end times must be provided.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var start = te.StartTime.Value.ToTimeSpan();
                var end = te.EndTime.Value.ToTimeSpan();

                // Allow equal times (0 duration) but warn the user
                if (start == end)
                {
                    var result = MessageBox.Show(
                        "Start and end time are the same, resulting in 0 duration.\n\nDo you want to continue?",
                        "Zero Duration Entry",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                // Check for ticket number unless it's a lunch entry
                if (string.IsNullOrWhiteSpace(te.TicketNumber))
                {
                    var isLunch = te.Notes?.Equals("Lunch", StringComparison.OrdinalIgnoreCase) ?? false;

                    if (!isLunch)
                    {
                        var result = MessageBox.Show(
                            "Ticket number is empty. Is this correct?\n\nClick 'Yes' to save without a ticket number.",
                            "Missing Ticket Number",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result != MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }
                }
            }

            DialogResult = true;
            Close();
        }
    }
}
