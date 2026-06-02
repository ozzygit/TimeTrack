using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TimeTrack.Utilities;
using TimeTrack.Views;

namespace TimeTrack.Views.Dialogs
{
    public partial class OptionsWindow : Window
    {
        private Dictionary<string, KeyboardShortcut> shortcuts = null!; // Suppress CS8618

        public OptionsWindow()
        {
            InitializeComponent();
            LoadShortcuts();
            LoadTheme();
        }

        private void LoadTheme()
        {
            switch (SettingsManager.Theme)
            {
                case ThemeMode.Dark:          ThemeDark.IsChecked          = true; break;
                case ThemeMode.Light:         ThemeLight.IsChecked         = true; break;
                default:                      ThemeSystemDefault.IsChecked = true; break;
            }
        }

        private ThemeMode SelectedTheme()
        {
            if (ThemeDark.IsChecked          == true) return ThemeMode.Dark;
            if (ThemeLight.IsChecked         == true) return ThemeMode.Light;
            return ThemeMode.SystemDefault;
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            ThemeManager.Apply(SelectedTheme());
        }

        private void LoadShortcuts()
        {
            shortcuts = SettingsManager.GetAllShortcuts();
            ShortcutsGrid.ItemsSource = shortcuts.Values.OrderBy(s => s.DisplayName).ToList();
        }

        private void NavButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                // Hide all panels
                if (GeneralPanel != null) GeneralPanel.Visibility = Visibility.Collapsed;
                if (KeyboardPanel != null) KeyboardPanel.Visibility = Visibility.Collapsed;
                if (AppearancePanel != null) AppearancePanel.Visibility = Visibility.Collapsed;

                // Show selected panel
                if (radioButton == GeneralTab && GeneralPanel != null)
                {
                    GeneralPanel.Visibility = Visibility.Visible;
                }
                else if (radioButton == KeyboardTab && KeyboardPanel != null)
                {
                    KeyboardPanel.Visibility = Visibility.Visible;
                }
                else if (radioButton == AppearanceTab && AppearancePanel != null)
                {
                    AppearancePanel.Visibility = Visibility.Visible;
                }
            }
        }

        private void ChangeShortcut_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string? actionName = button?.Tag as string;

            if (actionName != null && shortcuts.ContainsKey(actionName))
            {
                var dialog = new ShortcutInputDialog(shortcuts[actionName]);
                if (dialog.ShowDialog() == true)
                {
                    var selectedKey = dialog.SelectedKey;
                    var selectedMods = dialog.SelectedModifiers;

                    // Bare Enter/Return (no modifier) cannot be Submit — it conflicts with
                    // newline insertion in the Notes field. Ctrl+Enter is the standard alternative.
                    if (actionName == "Submit" &&
                        (selectedKey == Key.Enter || selectedKey == Key.Return) &&
                        selectedMods == ModifierKeys.None)
                    {
                        MessageBox.Show(
                            "Enter (without a modifier) cannot be used as the Submit shortcut " +
                            "because it conflicts with typing new lines in the Notes field.\n\n" +
                            "Try Ctrl+Enter instead.",
                            "Invalid Shortcut",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    shortcuts[actionName].Key = selectedKey;
                    shortcuts[actionName].Modifiers = selectedMods;
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
            SettingsManager.Theme = SelectedTheme();
            SettingsManager.Save();
            
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // Save all shortcuts
            foreach (var kvp in shortcuts)
            {
                SettingsManager.UpdateShortcut(kvp.Key, kvp.Value.Key, kvp.Value.Modifiers);
            }
            SettingsManager.Theme = SelectedTheme();
            SettingsManager.Save();
            
            // Notify the MainWindow to reload shortcuts
            if (Owner is MainWindow mainWindow)
            {
                mainWindow.ApplyKeyboardShortcuts();
                mainWindow.UpdateMenuGestureTexts();
            }
            
            MessageBox.Show("Settings have been applied.", "Apply Complete", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}