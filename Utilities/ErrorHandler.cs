using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using TimeTrack.Views.Dialogs;

namespace TimeTrack.Utilities
{
    public static class ErrorHandler
    {
        public static void Handle(string errorText, Exception e, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string caller = "")
        {
            // PORTABLE MODE: Store logs in same folder as executable
            // This matches the database location (portable mode) and avoids Airlock blocking
            // Log file will be created at: <exe-folder>\time_track_log.txt
            string logDir = AppDomain.CurrentDomain.BaseDirectory;
            string logPath = Path.Combine(logDir, "time_track_log.txt");
            
            try 
            { 
                Directory.CreateDirectory(logDir); 
            } 
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create log directory: {ex.Message}");
            }

            string timestamp = DateTime.UtcNow.ToString("o");
            string log = $"{timestamp},{caller},{e.GetType().Name},{e.Message.Replace("\r", " ").Replace("\n", " | ")},{errorText.Replace("\r", " ").Replace("\n", " | ")}\n";
            
            try 
            { 
                File.AppendAllText(logPath, log); 
            } 
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write to log: {ex.Message}");
            }

            void ShowError()
            {
                string summary = errorText;
                string details = $"Function: {caller}\nLine: {lineNumber}\n\nException:\n{e}\n\nLog: {logPath}";
                var dialog = new ErrorDialog(summary, details);
                dialog.ShowDialog();
            }

            var app = Application.Current;
            if (app?.Dispatcher?.CheckAccess() == true)
                ShowError();
            else
                app?.Dispatcher?.InvokeAsync(ShowError);
        }
    }
}
