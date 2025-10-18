using System;
using System.Windows;

namespace TimeTrack
{
    public partial class EditEntryWindow : Window
    {
        public EditEntryWindow(TimeEntry entry)
        {
            InitializeComponent();
            DataContext = entry;
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            // Basic logical validation: if both times are present, they cannot be equal
            if (DataContext is TimeEntry te && te.StartTime.HasValue && te.EndTime.HasValue)
            {
                var start = te.StartTime.Value.ToTimeSpan();
                var end = te.EndTime.Value.ToTimeSpan();
                if (start == end)
                {
                    MessageBox.Show("Start and end time cannot be the same.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            DialogResult = true;
            Close();
        }
    }
}
