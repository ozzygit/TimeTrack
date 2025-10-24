using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using TimeTrack.Data;
using TimeTrack.Utilities;
using TimeTrack.Views;

namespace TimeTrack
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
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
            base.OnStartup(e);
            
            // Check for .NET 8 Desktop Runtime prerequisite
            if (!CheckDotNetRuntimeInstalled())
            {
                var result = MessageBox.Show(
                    "TimeTrack v2 requires the .NET 8 Desktop Runtime (x64) to run.\n\n" +
                    "This free runtime is not currently installed on your system.\n\n" +
                    "Would you like to download and install it now?",
                    "Missing .NET Runtime",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Open the .NET 8 Desktop Runtime download page
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://dotnet.microsoft.com/download/dotnet/8.0/runtime",
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Failed to open download page: {ex.Message}\n\n" +
                            "Please visit: https://dotnet.microsoft.com/download/dotnet/8.0/runtime",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }

                Shutdown(1);
                return;
            }
            
            try
            {
                // Display diagnostic information in debug mode
                System.Diagnostics.Debug.WriteLine("=== TimeTrack v2 Startup Diagnostics ===");
                System.Diagnostics.Debug.WriteLine($"Executable Location: {AppDomain.CurrentDomain.BaseDirectory}");
                System.Diagnostics.Debug.WriteLine($"Current Directory: {Environment.CurrentDirectory}");
                System.Diagnostics.Debug.WriteLine($"User Profile: {Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}");
                System.Diagnostics.Debug.WriteLine($"AppData (Roaming): {Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}");
                
                // Initialize database
                Database.CreateDatabase();
                Database.BackupDatabaseIfNeeded();
                
                System.Diagnostics.Debug.WriteLine($"Database Location: {Database.GetDatabasePath()}");
                System.Diagnostics.Debug.WriteLine("Database initialized successfully");
            }
            catch (Exception ex)
            {
                // More detailed error message for startup failures
                string diagnosticInfo = 
                    $"TimeTrack v2 failed to start.\n\n" +
                    $"Error: {ex.Message}\n\n" +
                    $"Diagnostic Information:\n" +
                    $"- Executable: {AppDomain.CurrentDomain.BaseDirectory}\n" +
                    $"- Working Directory: {Environment.CurrentDirectory}\n" +
                    $"- User Profile: {Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\n" +
                    $"- AppData: {Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\n\n" +
                    $"The application may not have sufficient permissions or the database location may be inaccessible.\n\n" +
                    $"Full Exception:\n{ex}";
                
                System.Diagnostics.Debug.WriteLine($"Startup failed: {ex}");
                
                MessageBox.Show(
                    diagnosticInfo,
                    "TimeTrack v2 - Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                // Don't continue if database initialization failed
                Shutdown(1);
                return;
            }

            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                ErrorHandler.Handle("Failed to create main window.", ex);
                
                MessageBox.Show(
                    $"Failed to create the main window.\n\n" +
                    $"Error: {ex.Message}\n\n" +
                    $"The application will now exit.",
                    "TimeTrack v2 - Window Creation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                Shutdown(1);
            }
        }

        /// <summary>
        /// Check if .NET 8 Desktop Runtime is installed by looking for the runtime directory.
        /// </summary>
        private static bool CheckDotNetRuntimeInstalled()
        {
            try
            {
                // Check common .NET install locations for WindowsDesktop.App 8.x
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var dotnetPath = Path.Combine(programFiles, "dotnet", "shared", "Microsoft.WindowsDesktop.App");
                
                if (!Directory.Exists(dotnetPath))
                    return false;

                // Look for any 8.x version
                var directories = Directory.GetDirectories(dotnetPath);
                foreach (var dir in directories)
                {
                    var versionFolder = Path.GetFileName(dir);
                    if (versionFolder.StartsWith("8."))
                        return true;
                }

                return false;
            }
            catch
            {
                // If we can't check, assume runtime is present to avoid false positives
                // The app will fail naturally if runtime is actually missing
                return true;
            }
        }
    }
}
