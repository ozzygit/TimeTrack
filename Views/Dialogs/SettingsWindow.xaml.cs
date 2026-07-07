using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TimeTrack.Data;
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
            LoadTraySettings();
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

        private void LoadTraySettings()
        {
            ChkStartWithWindows.IsChecked = SettingsManager.IsStartWithWindowsEnabled();
            ChkShowTrayIcon.IsChecked = SettingsManager.ShowTrayIcon;
            ChkMinimizeToTray.IsChecked = SettingsManager.MinimizeToTray;
            ChkCloseToTray.IsChecked = SettingsManager.CloseToTray;
            ChkConfirmDelete.IsChecked = SettingsManager.ConfirmDelete;
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

        private double _originalWidth = 800;
        private double _originalHeight = 600;
        private bool _isDataSized = false;

        private void NavButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                // Hide all panels
                if (GeneralPanel != null) GeneralPanel.Visibility = Visibility.Collapsed;
                if (KeyboardPanel != null) KeyboardPanel.Visibility = Visibility.Collapsed;
                if (AppearancePanel != null) AppearancePanel.Visibility = Visibility.Collapsed;
                if (DataPanel != null) DataPanel.Visibility = Visibility.Collapsed;

                // Show selected panel
                if (radioButton == GeneralTab && GeneralPanel != null)
                {
                    GeneralPanel.Visibility = Visibility.Visible;
                    RestoreWindowSize();
                }
                else if (radioButton == KeyboardTab && KeyboardPanel != null)
                {
                    KeyboardPanel.Visibility = Visibility.Visible;
                    RestoreWindowSize();
                }
                else if (radioButton == AppearanceTab && AppearancePanel != null)
                {
                    AppearancePanel.Visibility = Visibility.Visible;
                    RestoreWindowSize();
                }
                else if (radioButton == DataTab && DataPanel != null)
                {
                    DataPanel.Visibility = Visibility.Visible;
                    LoadBackups();
                    EnlargeForDataTab();
                }
            }
        }

        private void EnlargeForDataTab()
        {
            if (_isDataSized) return;
            _originalWidth = ActualWidth;
            _originalHeight = ActualHeight;
            _isDataSized = true;

            var targetWidth = Math.Max(_originalWidth, 860);
            var targetHeight = Math.Max(_originalHeight, 720);

            double maxWidth = SystemParameters.WorkArea.Width;
            double maxHeight = SystemParameters.WorkArea.Height;

            if (targetWidth > maxWidth) targetWidth = maxWidth;
            if (targetHeight > maxHeight) targetHeight = maxHeight;

            Width = targetWidth;
            Height = targetHeight;
        }

        private void RestoreWindowSize()
        {
            if (!_isDataSized) return;
            _isDataSized = false;
            Width = _originalWidth;
            Height = _originalHeight;
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
                        ModernDialog.ShowWarning(
                            "Enter (without a modifier) cannot be used as a keyboard shortcut " +
                            "because it conflicts with typing new lines in the Notes field.\n\n" +
                            "Try adding a modifier such as Ctrl+Enter.",
                            "Invalid Shortcut");
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
            if (ModernDialog.Confirm(
                "Are you sure you want to reset all keyboard shortcuts to their default values?",
                "Reset to Defaults"))
            {
                SettingsManager.ResetToDefaults();
                LoadShortcuts();
                ModernDialog.ShowInfo("Shortcuts have been reset to defaults.", "Reset Complete");
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
                DataTab.Content = string.Empty;
            }
            else
            {
                NavPanel.Width = 220;
                NavTitle.Visibility = Visibility.Visible;
                GeneralTab.Content = "General";
                KeyboardTab.Content = "Keyboard Shortcuts";
                AppearanceTab.Content = "Appearance";
                DataTab.Content = "Data";
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
            SettingsManager.ShowTrayIcon = ChkShowTrayIcon.IsChecked == true;
            SettingsManager.MinimizeToTray = ChkMinimizeToTray.IsChecked == true;
            SettingsManager.CloseToTray = ChkCloseToTray.IsChecked == true;
            SettingsManager.ConfirmDelete = ChkConfirmDelete.IsChecked == true;
            SettingsManager.StartWithWindows = ChkStartWithWindows.IsChecked == true;
            SettingsManager.ApplyStartWithWindows();
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

        // ── Data Management ──

        private void LoadBackups()
        {
            BackupList.ItemsSource = Database.GetBackups();
        }

        private void CreateBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Database.CreateBackupNow();
                LoadBackups();
                ModernDialog.ShowInfo("Backup created successfully.", "Backup Complete");
            }
            catch
            {
                // Error already handled by ErrorHandler
            }
        }

        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            if (BackupList.SelectedItem is not Database.BackupInfo selected)
            {
                ModernDialog.ShowWarning("Please select a backup to restore.", "No Backup Selected");
                return;
            }

            if (!ModernDialog.Confirm(
                $"Are you sure you want to restore the backup from {selected.DisplayDate}?\n\n" +
                "A safety backup of your current database will be created first.\n" +
                "The application will need to restart to apply the changes.",
                "Confirm Restore"))
                return;

            if (Database.RestoreFromBackup(selected.FilePath))
            {
                ModernDialog.ShowInfo(
                    "Database restored successfully. The application will now restart.",
                    "Restore Complete");

                RestartApp();
            }
        }

        private void ExportDatabase_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*",
                FileName = $"timetrack_export_{DateTime.Now:yyyy-MM-dd}.db",
                Title = "Export Database"
            };

            if (dialog.ShowDialog() == true)
            {
                if (Database.ExportDatabase(dialog.FileName))
                    ModernDialog.ShowInfo("Database exported successfully.", "Export Complete");
            }
        }

        private void ImportDatabase_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*",
                Title = "Import Database"
            };

            if (dialog.ShowDialog() != true)
                return;

            if (!ModernDialog.Confirm(
                $"Are you sure you want to import '{Path.GetFileName(dialog.FileName)}'?\n\n" +
                "This will replace your current database. A safety backup will be created first.\n" +
                "The application will need to restart to apply the changes.",
                "Confirm Import"))
                return;

            if (Database.ImportDatabase(dialog.FileName))
            {
                ModernDialog.ShowInfo(
                    "Database imported successfully. The application will now restart.",
                    "Import Complete");

                RestartApp();
            }
        }

        private void OpenBackupFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = Database.GetBackupFolder();
            if (Directory.Exists(folder))
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            else
                ModernDialog.ShowInfo("No backup folder exists yet.", "No Backups");
        }

        private void RestartApp()
        {
            var exePath = Environment.ProcessPath;
            if (exePath != null)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            Application.Current.Shutdown();
        }
    }
}