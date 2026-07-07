using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using TimeTrack.Data;
using TimeTrack.Utilities;

namespace TimeTrack.Views.Dialogs
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

            var assembly = AppVersion.Assembly ?? Assembly.GetExecutingAssembly();

            VersionText.Text = AppVersion.VersionLabel;

            // Set description from AssemblyDescription attribute
            var description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
            if (!string.IsNullOrEmpty(description))
            {
                DescriptionText.Text = description;
            }
            else
            {
                DescriptionText.Text = "Time tracking application for daily work entries";
            }

            // Set database location
            UpdateDatabaseLocationDisplay();
        }

        private void UpdateDatabaseLocationDisplay()
        {
            try
            {
                var dbPath = Database.GetDatabasePath();
                if (DatabaseLocationText != null)
                {
                    DatabaseLocationText.Text = dbPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating database location: {ex.Message}");
                if (DatabaseLocationText != null)
                {
                    DatabaseLocationText.Text = "Unknown";
                }
            }
        }

        private void DatabaseLocationLink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dbDirectory = Database.GetDatabaseDirectory();

                // Ensure the directory exists before trying to open it
                if (!System.IO.Directory.Exists(dbDirectory))
                {
                    ModernDialog.ShowInfo($"Database directory does not exist yet:\n{dbDirectory}",
                                    "Directory Not Found");
                    return;
                }

                // Open the directory in File Explorer
                Process.Start(new ProcessStartInfo
                {
                    FileName = dbDirectory,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                ModernDialog.ShowError($"Failed to open database folder:\n{ex.Message}",
                                "Error");
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                // Open the GitHub link in the default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                ModernDialog.ShowError($"Failed to open link:\n{ex.Message}",
                                "Error");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
