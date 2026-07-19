using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using TimeTrack.Data;
using TimeTrack.Utilities;
using TimeTrack.Views;
using TimeTrack.Views.Dialogs;

namespace TimeTrack
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly Mutex SingleInstanceMutex = new(false, "Global\\TimeTrack_v3_SingleInstance");

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsHungAppWindow(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        private static void CreateStartMenuShortcut()
        {
            try
            {
                var startMenuFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    "TimeTrack.lnk");

                if (File.Exists(startMenuFolder))
                    return;

                var exePath = Environment.ProcessPath;
                if (exePath == null) return;

                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(startMenuFolder);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                shortcut.Description = "TimeTrack v3 - MSP time tracking";
                shortcut.IconLocation = exePath;
                shortcut.Save();
            }
            catch { }
        }

        private static void ActivateExistingInstance()
        {
            var windowTitle = AppVersion.MainWindowTitle;
            var hWnd = FindWindow(null, windowTitle);
            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
            }
        }

        private static bool IsExistingInstanceHung()
        {
            var windowTitle = AppVersion.MainWindowTitle;
            var hWnd = FindWindow(null, windowTitle);
            if (hWnd == IntPtr.Zero)
            {
                var processes = Process.GetProcessesByName("TimeTrack");
                if (processes.Length > 1)
                {
                    foreach (var p in processes)
                    {
                        if (p.Id != Environment.ProcessId && !p.Responding)
                            return true;
                    }
                }
                return false;
            }
            return IsHungAppWindow(hWnd);
        }

        private static void KillExistingInstance()
        {
            var processes = Process.GetProcessesByName("TimeTrack");
            foreach (var p in processes)
            {
                if (p.Id != Environment.ProcessId)
                {
                    try { p.Kill(); p.WaitForExit(3000); } catch { }
                }
            }
        }

        public App()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                ErrorHandler.Handle("Unhandled UI exception.", e.Exception);
                e.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                ErrorHandler.Handle("Unobserved task exception.", e.Exception);
                e.SetObserved();
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    ErrorHandler.Handle("Unhandled domain exception.", ex);
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (!SingleInstanceMutex.WaitOne(0, false))
            {
                if (IsExistingInstanceHung())
                {
                    KillExistingInstance();
                    try { SingleInstanceMutex.WaitOne(1000, false); }
                    catch (AbandonedMutexException) { }
                }
                else
                {
                    ActivateExistingInstance();
                    Shutdown(0);
                    return;
                }
            }

            base.OnStartup(e);

            try
            {
                // Display diagnostic information in debug mode
                System.Diagnostics.Debug.WriteLine("=== TimeTrack v3 Startup Diagnostics ===");
                System.Diagnostics.Debug.WriteLine($"Executable Location: {AppDomain.CurrentDomain.BaseDirectory}");
                System.Diagnostics.Debug.WriteLine($"Current Directory: {Environment.CurrentDirectory}");
                System.Diagnostics.Debug.WriteLine($"User Profile: {Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}");
                System.Diagnostics.Debug.WriteLine($"AppData (Roaming): {Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}");

                // Apply saved theme before any windows open
                ThemeManager.ApplySavedTheme();

                // Initialize database
                Database.CreateDatabase();
                Database.BackupDatabaseIfNeeded();
                Database.PurgeOldDeletedEntries();

                // Ensure Start Menu shortcut exists
                CreateStartMenuShortcut();

                System.Diagnostics.Debug.WriteLine($"Database Location: {Database.GetDatabasePath()}");
                System.Diagnostics.Debug.WriteLine("Database initialized successfully");
            }
            catch (Exception ex)
            {
                // More detailed error message for startup failures
                string diagnosticInfo =
                    $"TimeTrack v3 failed to start.\n\n" +
                    $"Error: {ex.Message}\n\n" +
                    $"Diagnostic Information:\n" +
                    $"- Executable: {AppDomain.CurrentDomain.BaseDirectory}\n" +
                    $"- Working Directory: {Environment.CurrentDirectory}\n" +
                    $"- User Profile: {Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\n" +
                    $"- AppData: {Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\n\n" +
                    $"The application may not have sufficient permissions or the database location may be inaccessible.\n\n" +
                    $"Full Exception:\n{ex}";

                System.Diagnostics.Debug.WriteLine($"Startup failed: {ex}");

                new ErrorDialog("TimeTrack v3 - Startup Error", diagnosticInfo).ShowDialog();

                // Don't continue if database initialization failed
                Shutdown(1);
                return;
            }

            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();

                // First-run accessibility prompt
                ShowAccessibilityWelcomePromptIfNeeded(mainWindow);
            }
            catch (Exception ex)
            {
                ErrorHandler.Handle("Failed to create main window.", ex);

                new ErrorDialog("TimeTrack v3 - Window Creation Error",
                    $"Failed to create the main window.\n\n" +
                    $"Error: {ex.Message}\n\n" +
                    "The application will now exit.").ShowDialog();

                Shutdown(1);
            }
        }

        private static void ShowAccessibilityWelcomePromptIfNeeded(Window owner)
        {
            if (SettingsManager.AccessibilityPromptShown)
                return;

            var dialog = new WelcomeDialog { Owner = owner };
            if (dialog.ShowDialog() == true)
            {
                switch (dialog.Result)
                {
                    case WelcomeDialog.WelcomeResult.EnableAll:
                        // All features already default to true, so nothing to change
                        break;
                    case WelcomeDialog.WelcomeResult.KeepSimple:
                        SettingsManager.DayTimelineEnabled = false;
                        SettingsManager.TimerColourCodingEnabled = false;
                        SettingsManager.TimeOfDayLabelEnabled = false;
                        SettingsManager.SessionProgressEnabled = false;
                        SettingsManager.OvertimeModeEnabled = false;
                        SettingsManager.CheckInEnabled = false;
                        SettingsManager.IdleDetectionEnabled = false;
                        SettingsManager.EodReminderEnabled = false;
                        SettingsManager.UnsubmittedWarningEnabled = false;
                        SettingsManager.ContextSummaryEnabled = false;
                        SettingsManager.ContinueFromLastEntry = false;
                        SettingsManager.RecentTicketsEnabled = false;
                        SettingsManager.QuickStartEnabled = false;
                        SettingsManager.SmartPresetsEnabled = false;
                        SettingsManager.ParkingLotEnabled = false;
                        SettingsManager.ParkEntriesEnabled = false;
                        SettingsManager.FocusModeEnabled = false;
                        SettingsManager.EntryCountBadgeEnabled = false;
                        SettingsManager.CompletionFeedbackEnabled = false;
                        SettingsManager.StreakCounterEnabled = false;
                        SettingsManager.DayAtAGlanceEnabled = false;
                        break;
                    case WelcomeDialog.WelcomeResult.Customise:
                        var settingsWindow = new SettingsWindow { Owner = owner };
                        settingsWindow.ShowDialog();
                        break;
                }
            }

            SettingsManager.AccessibilityPromptShown = true;
            SettingsManager.Save();
        }
    }
}
