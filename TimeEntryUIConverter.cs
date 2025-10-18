using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls; // <-- Add this if you use controls in this file
using System.Windows.Markup;   // <-- Add this if you use markup extensions in this file

// 6) Converter: handle TimeOnly/TimeSpan and target types explicitly
public sealed class TimeEntryUIConverter : IValueConverter
{
    private const string TimeFormat = "t";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            null => string.Empty,
            TimeOnly t => t.ToString(TimeFormat, culture),
            TimeSpan ts => TimeOnly.FromTimeSpan(ts).ToString(TimeFormat, culture),
            _ => Binding.DoNothing
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return targetType == typeof(TimeSpan?) || targetType == typeof(TimeOnly?) ? null! : DependencyProperty.UnsetValue;

        if (TimeOnly.TryParse(s, culture, out var t))
        {
            if (targetType == typeof(TimeOnly) || targetType == typeof(TimeOnly?)) return t;
            if (targetType == typeof(TimeSpan) || targetType == typeof(TimeSpan?)) return t.ToTimeSpan();
        }

        return DependencyProperty.UnsetValue;
    }
}