using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TimeTrack.Utilities;

namespace TimeTrack.Views.Dialogs
{
    public partial class SettingsWindow : Window
    {
        private Dictionary<string, KeyboardShortcut> shortcuts = null!; // Suppress CS8618
        private ThemeMode _originalTheme;

        public SettingsWindow()
        {
            InitializeComponent();
            _originalTheme = SettingsManager.Theme;
            LoadShortcuts();
            LoadTheme();
        }

        private void LoadTheme()
        {
            switch (SettingsManager.Theme)
            {
                case ThemeMode.Dark:          ThemeDark.IsChecked          = true; break;
                case ThemeMode.Light:         ThemeLight.IsChecked         = true; break;
                case ThemeMode.MonokaiDimmed:    ThemeMonokaiDimmed.IsChecked    = true; break;
                case ThemeMode.KimbieDark:       ThemeKimbieDark.IsChecked       = true; break;
                case ThemeMode.SolarizedDark:    ThemeSolarizedDark.IsChecked    = true; break;
                case ThemeMode.TomorrowNightBlue: ThemeTomorrowNightBlue.IsChecked = true; break;
                default:                      ThemeSystemDefault.IsChecked = true; break;
            }
        }

        private ThemeMode SelectedTheme()
        {
            if (ThemeDark.IsChecked          == true) return ThemeMode.Dark;
            if (ThemeLight.IsChecked         == true) return ThemeMode.Light;
            if (ThemeMonokaiDimmed.IsChecked    == true) return ThemeMode.MonokaiDimmed;
            if (ThemeKimbieDark.IsChecked       == true) return ThemeMode.KimbieDark;
            if (ThemeSolarizedDark.IsChecked    == true) return ThemeMode.SolarizedDark;
            if (ThemeTomorrowNightBlue.IsChecked == true) return ThemeMode.TomorrowNightBlue;
            return ThemeMode.SystemDefault;
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            ThemeManager.Apply(SelectedTheme());
        }

        private void LoadShortcuts()
        {
            // Deep-copy so local edits don't mutate the live SettingsManager state until OK is clicked.
            shortcuts = SettingsManager.GetAllShortcuts().ToDictionary(
                kvp => kvp.Key,
                kvp => new KeyboardShortcut
                {
                    ActionName = kvp.Value.ActionName,
                    DisplayName = kvp.Value.DisplayName,
                    Key = kvp.Value.Key,
                    Modifiers = kvp.Value.Modifiers
                });
            // Submit is a fixed shortcut (Ctrl+Enter) and is intentionally not user-configurable.
            ShortcutsList.ItemsSource = shortcuts.Values
                .Where(s => s.ActionName != "Submit")
                .OrderBy(s => s.DisplayName)
                .ToList();
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

                    // Bare Enter/Return (no modifier) cannot be assigned to any shortcut —
                    // it conflicts with newline insertion in the Notes field and general typing.
                    if ((selectedKey == Key.Enter || selectedKey == Key.Return) &&
                        selectedMods == ModifierKeys.None)
                    {
                        MessageBox.Show(
                            "Enter (without a modifier) cannot be used as a keyboard shortcut " +
                            "because it conflicts with typing new lines in the Notes field.\n\n" +
                            "Try adding a modifier such as Ctrl+Enter.",
                            "Invalid Shortcut",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    shortcuts[actionName].Key = selectedKey;
                    shortcuts[actionName].Modifiers = selectedMods;
                    ShortcutsList.Items.Refresh();
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

        private bool _navCollapsed = false;

        private void BtnHamburger_Click(object sender, RoutedEventArgs e)
        {
            _navCollapsed = !_navCollapsed;
            if (_navCollapsed)
            {
                NavPanel.Width = 52;
                NavTitle.Visibility = Visibility.Collapsed;
                GeneralTab.Content = string.Empty;
                KeyboardTab.Content = string.Empty;
                AppearanceTab.Content = string.Empty;
            }
            else
            {
                NavPanel.Width = 220;
                NavTitle.Visibility = Visibility.Visible;
                GeneralTab.Content = "General";
                KeyboardTab.Content = "Keyboard Shortcuts";
                AppearanceTab.Content = "Appearance";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Cancel_Click(sender, e);
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
            ThemeManager.Apply(_originalTheme);
            DialogResult = false;
            Close();
        }
    }
}