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
        private bool _isLoading = true;

        public SettingsWindow()
        {
            InitializeComponent();
            _originalTheme = SettingsManager.Theme;
            LoadShortcuts();
            LoadTheme();
            LoadTraySettings();
            LoadAccessibilitySettings();
            _isLoading = false;
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
                case ThemeMode.HighContrast:    ThemeHighContrast.IsChecked    = true; break;
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
            if (ThemeHighContrast.IsChecked    == true) return ThemeMode.HighContrast;
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
                if (AccessibilityPanel != null) AccessibilityPanel.Visibility = Visibility.Collapsed;

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
                else if (radioButton == AccessibilityTab && AccessibilityPanel != null)
                {
                    AccessibilityPanel.Visibility = Visibility.Visible;
                    RestoreWindowSize();
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
                AccessibilityTab.Content = string.Empty;
            }
            else
            {
                NavPanel.Width = 220;
                NavTitle.Visibility = Visibility.Visible;
                GeneralTab.Content = "General";
                KeyboardTab.Content = "Keyboard Shortcuts";
                AppearanceTab.Content = "Appearance";
                DataTab.Content = "Data";
                AccessibilityTab.Content = "Accessibility";
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
            SaveAccessibilitySettings();
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

        // ── Accessibility Settings ──

        private static readonly string[] _accessibilityCheckBoxes = new[]
        {
            nameof(ChkDayTimeline), nameof(ChkTimerColourCoding), nameof(ChkTimeOfDayLabel),
            nameof(ChkSessionProgress), nameof(ChkOvertimeMode), nameof(ChkCheckIn),
            nameof(ChkIdleDetection), nameof(ChkEodReminder), nameof(ChkUnsubmittedWarning),
            nameof(ChkContextSummary), nameof(ChkContinueFromLast), nameof(ChkRecentTickets),
            nameof(ChkQuickStart), nameof(ChkSmartPresets), nameof(ChkParkingLot),
            nameof(ChkParkEntries), nameof(ChkFocusMode), nameof(ChkEntryCountBadge),
            nameof(ChkReduceMotion), nameof(ChkCompletionFeedback), nameof(ChkStreakCounter),
            nameof(ChkDayAtAGlance)
        };

        private void LoadAccessibilitySettings()
        {
            ChkDayTimeline.IsChecked = SettingsManager.DayTimelineEnabled;
            ChkTimerColourCoding.IsChecked = SettingsManager.TimerColourCodingEnabled;
            TxtTimerCaution.Text = SettingsManager.TimerCautionMinutes.ToString();
            TxtTimerWarning.Text = SettingsManager.TimerWarningMinutes.ToString();
            ChkTimeOfDayLabel.IsChecked = SettingsManager.TimeOfDayLabelEnabled;
            ChkSessionProgress.IsChecked = SettingsManager.SessionProgressEnabled;
            TxtExpectedSession.Text = SettingsManager.ExpectedSessionMinutes.ToString();
            ChkOvertimeMode.IsChecked = SettingsManager.OvertimeModeEnabled;
            ChkCheckIn.IsChecked = SettingsManager.CheckInEnabled;
            TxtCheckInInterval.Text = SettingsManager.CheckInIntervalMinutes.ToString();
            ChkIdleDetection.IsChecked = SettingsManager.IdleDetectionEnabled;
            TxtIdleThreshold.Text = SettingsManager.IdleThresholdMinutes.ToString();
            TxtAutoPause.Text = SettingsManager.AutoPauseThresholdMinutes.ToString();
            ChkEodReminder.IsChecked = SettingsManager.EodReminderEnabled;
            TxtEodTime.Text = SettingsManager.EodReminderTime;
            ChkUnsubmittedWarning.IsChecked = SettingsManager.UnsubmittedWarningEnabled;
            ChkContextSummary.IsChecked = SettingsManager.ContextSummaryEnabled;
            ChkContinueFromLast.IsChecked = SettingsManager.ContinueFromLastEntry;
            ChkRecentTickets.IsChecked = SettingsManager.RecentTicketsEnabled;
            ChkQuickStart.IsChecked = SettingsManager.QuickStartEnabled;
            ChkSmartPresets.IsChecked = SettingsManager.SmartPresetsEnabled;
            ChkParkingLot.IsChecked = SettingsManager.ParkingLotEnabled;
            ChkParkEntries.IsChecked = SettingsManager.ParkEntriesEnabled;
            ChkFocusMode.IsChecked = SettingsManager.FocusModeEnabled;
            ChkEntryCountBadge.IsChecked = SettingsManager.EntryCountBadgeEnabled;
            ChkReduceMotion.IsChecked = SettingsManager.ReduceMotion;
            ChkCompletionFeedback.IsChecked = SettingsManager.CompletionFeedbackEnabled;
            ChkStreakCounter.IsChecked = SettingsManager.StreakCounterEnabled;
            ChkDayAtAGlance.IsChecked = SettingsManager.DayAtAGlanceEnabled;

            // ComboBoxes
            SelectComboBoxByTag(CmbFontSize, SettingsManager.FontSize.ToString());
            SelectComboBoxByTag(CmbFontFamily, SettingsManager.FontFamily.ToString());
            SelectComboBoxByTag(CmbNotificationStyle, SettingsManager.NotificationStyleMode.ToString());

            // Master toggle reflects whether all sub-toggles are on
            UpdateMasterToggle();
            UpdateSubSettingEnabledState();
        }

        private static void SelectComboBoxByTag(ComboBox combo, string tag)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if ((string)item.Tag == tag)
                {
                    item.IsSelected = true;
                    return;
                }
            }
        }

        private void SaveAccessibilitySettings()
        {
            SettingsManager.DayTimelineEnabled = ChkDayTimeline.IsChecked == true;
            SettingsManager.TimerColourCodingEnabled = ChkTimerColourCoding.IsChecked == true;
            if (int.TryParse(TxtTimerCaution.Text, out var caution)) SettingsManager.TimerCautionMinutes = caution;
            if (int.TryParse(TxtTimerWarning.Text, out var warning)) SettingsManager.TimerWarningMinutes = warning;
            SettingsManager.TimeOfDayLabelEnabled = ChkTimeOfDayLabel.IsChecked == true;
            SettingsManager.SessionProgressEnabled = ChkSessionProgress.IsChecked == true;
            if (int.TryParse(TxtExpectedSession.Text, out var expected)) SettingsManager.ExpectedSessionMinutes = expected;
            SettingsManager.OvertimeModeEnabled = ChkOvertimeMode.IsChecked == true;
            SettingsManager.CheckInEnabled = ChkCheckIn.IsChecked == true;
            if (int.TryParse(TxtCheckInInterval.Text, out var checkIn)) SettingsManager.CheckInIntervalMinutes = checkIn;
            SettingsManager.IdleDetectionEnabled = ChkIdleDetection.IsChecked == true;
            if (int.TryParse(TxtIdleThreshold.Text, out var idle)) SettingsManager.IdleThresholdMinutes = idle;
            if (int.TryParse(TxtAutoPause.Text, out var autoPause)) SettingsManager.AutoPauseThresholdMinutes = autoPause;
            SettingsManager.EodReminderEnabled = ChkEodReminder.IsChecked == true;
            SettingsManager.EodReminderTime = TxtEodTime.Text;
            SettingsManager.UnsubmittedWarningEnabled = ChkUnsubmittedWarning.IsChecked == true;
            SettingsManager.ContextSummaryEnabled = ChkContextSummary.IsChecked == true;
            SettingsManager.ContinueFromLastEntry = ChkContinueFromLast.IsChecked == true;
            SettingsManager.RecentTicketsEnabled = ChkRecentTickets.IsChecked == true;
            SettingsManager.QuickStartEnabled = ChkQuickStart.IsChecked == true;
            SettingsManager.SmartPresetsEnabled = ChkSmartPresets.IsChecked == true;
            SettingsManager.ParkingLotEnabled = ChkParkingLot.IsChecked == true;
            SettingsManager.ParkEntriesEnabled = ChkParkEntries.IsChecked == true;
            SettingsManager.FocusModeEnabled = ChkFocusMode.IsChecked == true;
            SettingsManager.EntryCountBadgeEnabled = ChkEntryCountBadge.IsChecked == true;
            SettingsManager.ReduceMotion = ChkReduceMotion.IsChecked == true;
            SettingsManager.CompletionFeedbackEnabled = ChkCompletionFeedback.IsChecked == true;
            SettingsManager.StreakCounterEnabled = ChkStreakCounter.IsChecked == true;
            SettingsManager.DayAtAGlanceEnabled = ChkDayAtAGlance.IsChecked == true;

            if (CmbFontSize.SelectedItem is ComboBoxItem fontSizeItem && fontSizeItem.Tag is string fsTag)
            if (Enum.TryParse<FontSizeScale>(fsTag, out var fs)) SettingsManager.FontSize = fs;
            if (CmbFontFamily.SelectedItem is ComboBoxItem fontFamilyItem && fontFamilyItem.Tag is string ffTag)
            if (Enum.TryParse<FontFamilyOption>(ffTag, out var ff)) SettingsManager.FontFamily = ff;
            if (CmbNotificationStyle.SelectedItem is ComboBoxItem nsItem && nsItem.Tag is string nsTag)
            if (Enum.TryParse<NotificationStyle>(nsTag, out var ns)) SettingsManager.NotificationStyleMode = ns;
        }

        private void MasterToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            var isOn = ChkMasterToggle.IsChecked == true;
            foreach (var name in _accessibilityCheckBoxes)
            {
                if (FindName(name) is CheckBox cb)
                    cb.IsChecked = isOn;
            }
            UpdateSubSettingEnabledState();
        }

        private void SubToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            UpdateMasterToggle();
            UpdateSubSettingEnabledState();
        }

        private void UpdateMasterToggle()
        {
            bool allOn = true;
            foreach (var name in _accessibilityCheckBoxes)
            {
                if (FindName(name) is CheckBox cb && cb.IsChecked != true)
                {
                    allOn = false;
                    break;
                }
            }
            ChkMasterToggle.IsChecked = allOn;
        }

        private void UpdateSubSettingEnabledState()
        {
            // Enable/disable sub-setting controls based on parent toggle
            if (TxtTimerCaution != null) TxtTimerCaution.IsEnabled = ChkTimerColourCoding.IsChecked == true;
            if (TxtTimerWarning != null) TxtTimerWarning.IsEnabled = ChkTimerColourCoding.IsChecked == true;
            if (TxtExpectedSession != null) TxtExpectedSession.IsEnabled = ChkSessionProgress.IsChecked == true;
            if (TxtCheckInInterval != null) TxtCheckInInterval.IsEnabled = ChkCheckIn.IsChecked == true;
            if (TxtIdleThreshold != null) TxtIdleThreshold.IsEnabled = ChkIdleDetection.IsChecked == true;
            if (TxtAutoPause != null) TxtAutoPause.IsEnabled = ChkIdleDetection.IsChecked == true;
            if (TxtEodTime != null) TxtEodTime.IsEnabled = ChkEodReminder.IsChecked == true;
        }
    }
}