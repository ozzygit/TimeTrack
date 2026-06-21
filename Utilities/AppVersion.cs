using System;
using System.Reflection;

namespace TimeTrack.Utilities
{
    public static class AppVersion
    {
        private const string ProductName = "TimeTrack";

        private static readonly Lazy<Assembly?> EntryAssembly = new(() => Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly());

        private static readonly Lazy<string> CachedVersion = new(() =>
        {
            var assembly = EntryAssembly.Value;
            if (assembly == null)
            {
                return "Unknown";
            }

            var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVersion))
            {
                int plusIndex = infoVersion.IndexOf('+');
                return plusIndex >= 0 ? infoVersion[..plusIndex] : infoVersion;
            }

            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        });

        public static Assembly? Assembly => EntryAssembly.Value;

        public static string Version => CachedVersion.Value;

        public static string ProductDisplay => $"{ProductName} v{Version}";

        public static string MainWindowTitle => ProductDisplay;

        public static string AboutWindowTitle => $"About {ProductDisplay}";

        public static string VersionLabel => $"Version: {Version}";
    }
}
