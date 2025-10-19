using System;
using System.Globalization;
using System.Windows.Data;

namespace TimeTrack.Converters
{
    public class ShortcutDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var actionName = parameter as string;
                if (string.IsNullOrWhiteSpace(actionName))
                    return string.Empty;

                var shortcut = SettingsManager.GetShortcut(actionName);
                return shortcut?.DisplayText ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
