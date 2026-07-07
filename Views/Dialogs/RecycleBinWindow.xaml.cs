using System.Windows;
using TimeTrack.Data;
using TimeTrack.Utilities;

namespace TimeTrack.Views.Dialogs
{
    public partial class RecycleBinWindow : Window
    {
        public RecycleBinWindow()
        {
            InitializeComponent();
            LoadDeletedEntries();
        }

        private void LoadDeletedEntries()
        {
            DeletedList.ItemsSource = Database.GetDeletedEntries();
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (DeletedList.SelectedItem is not Database.DeletedEntryInfo selected)
            {
                ModernDialog.ShowWarning("Please select an entry to restore.", "No Entry Selected");
                return;
            }

            if (Database.RestoreDeletedEntry(selected.Date, selected.Id))
            {
                LoadDeletedEntries();
                ModernDialog.ShowInfo("Entry restored successfully.", "Restore Complete");
            }
        }

        private void Purge_Click(object sender, RoutedEventArgs e)
        {
            if (DeletedList.SelectedItem is not Database.DeletedEntryInfo selected)
            {
                ModernDialog.ShowWarning("Please select an entry to permanently delete.", "No Entry Selected");
                return;
            }

            if (!ModernDialog.Confirm(
                "Are you sure you want to permanently delete this entry?\n" +
                "This action cannot be undone.",
                "Confirm Permanent Delete"))
                return;

            if (Database.PurgeDeletedEntry(selected.Date, selected.Id))
                LoadDeletedEntries();
        }

        private void PurgeAll_Click(object sender, RoutedEventArgs e)
        {
            if (DeletedList.Items.Count == 0)
            {
                ModernDialog.ShowInfo("Recycle bin is already empty.", "Nothing to Empty");
                return;
            }

            if (!ModernDialog.Confirm(
                $"Are you sure you want to permanently delete all {DeletedList.Items.Count} entries?\n" +
                "This action cannot be undone.",
                "Empty Recycle Bin"))
                return;

            var count = Database.PurgeAllDeleted();
            LoadDeletedEntries();
            ModernDialog.ShowInfo($"{count} entries permanently deleted.", "Recycle Bin Emptied");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
