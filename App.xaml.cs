using System;
using System.Windows;
using TimeTrack.Data;
using TimeTrack.Utilities;

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
                Database.CreateDatabase();
                Database.BackupDatabaseIfNeeded();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database initialization failed: {ex.Message}");
            }

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
