using System;
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
    }
}
