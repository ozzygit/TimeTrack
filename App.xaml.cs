using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TimeTrack; // for Error
using TimeTrack.Data; // for Database

namespace TimeTrack
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            this.DispatcherUnhandledException += (s, e) =>
            {
                Error.Handle("Unhandled UI exception.", e.Exception);
                e.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Error.Handle("Unobserved task exception.", e.Exception);
                e.SetObserved();
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    Error.Handle("Unhandled domain exception.", ex);
            };
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            // Run DB setup/migrations before any window or DB usage
            try
            {
                Database.CreateDatabase();
            }
            catch
            {
                // Errors are already logged/shown via Error.Handle; decide whether to continue
            }

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
