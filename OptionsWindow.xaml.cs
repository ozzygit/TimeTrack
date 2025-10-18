using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TimeTrack
{
    public partial class OptionsWindow : Window
    {
        private Dictionary<string, KeyboardShortcut> shortcuts;

        public OptionsWindow()
        {
            InitializeComponent();
            LoadShortcuts();
        }

        private void LoadShortcuts()
        {
            shortcuts = SettingsManager.GetAllShortcuts();
            ShortcutsGrid.ItemsSource = shortcuts.Values.OrderBy(s => s.DisplayName).ToList();
        }

        private void ChangeShortcut_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string actionName = button?.Tag as string;

            if (actionName != null && shortcuts.ContainsKey(actionName))
            {
                var dialog = new ShortcutInputDialog(shortcuts[actionName]);
                if (dialog.ShowDialog() == true)
                {
                    shortcuts[actionName].Key = dialog.SelectedKey;
                    shortcuts[actionName].Modifiers = dialog.SelectedModifiers;
                    ShortcutsGrid.Items.Refresh();
                }
            }
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all keyboard shortcuts to their default values?",
                "Reset to Defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SettingsManager.ResetToDefaults();
                LoadShortcuts();
                MessageBox.Show("Shortcuts have been reset to defaults.", "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // Save all shortcuts
            foreach (var kvp in shortcuts)
            {
                SettingsManager.UpdateShortcut(kvp.Key, kvp.Value.Key, kvp.Value.Modifiers);
            }
            SettingsManager.Save();
            
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