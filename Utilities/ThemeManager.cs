using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;

namespace TimeTrack.Utilities
{
    public static class ThemeManager
    {
        private const string LightThemeUri = "pack://application:,,,/Themes/LightTheme.xaml";
        private const string DarkThemeUri  = "pack://application:,,,/Themes/DarkTheme.xaml";

        public static void ApplySavedTheme()
        {
            Apply(SettingsManager.Theme);
        }

        public static void Apply(ThemeMode mode)
        {
            var uri = ResolveUri(mode);
            SwapThemeDictionary(new Uri(uri, UriKind.Absolute));
        }

        private static string ResolveUri(ThemeMode mode)
        {
            return mode switch
            {
                ThemeMode.Dark          => DarkThemeUri,
                ThemeMode.Light         => LightThemeUri,
                ThemeMode.SystemDefault => IsSystemDark() ? DarkThemeUri : LightThemeUri,
                _                       => LightThemeUri
            };
        }

        private static void SwapThemeDictionary(Uri targetUri)
        {
            var merged = Application.Current.Resources.MergedDictionaries;
            var existing = merged.FirstOrDefault(d =>
                d.Source != null &&
                (d.Source.OriginalString.Contains("LightTheme") ||
                 d.Source.OriginalString.Contains("DarkTheme")));

            if (existing != null && existing.Source == targetUri)
                return;

            var next = new ResourceDictionary { Source = targetUri };
            if (existing != null)
                merged.Remove(existing);
            merged.Add(next);
        }

        private static bool IsSystemDark()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                return value is int i && i == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
