using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TimeTrack.Utilities
{
    public static class TimeStringConverter
    {
        private static readonly DateTime WorkHoursStart = DateTime.ParseExact("07:00AM", "hh:mmtt", CultureInfo.InvariantCulture);
        private static readonly DateTime WorkHoursEnd = DateTime.ParseExact("07:00PM", "hh:mmtt", CultureInfo.InvariantCulture);

        public static bool IsValidTimeFormat(string value)
        {
            if (value == null)
                return false;

            string validTimeFormat = @"^\d{1,2}[;:.]?(\d{2})?((\s?)+(?i:[AP]M)?)?$";
            string valid24HourFormat = @"^\d{2}[;:.]?(\d{2})?(?i:(\s?)+(?!AM)(PM)?)$";

            if (Regex.IsMatch(value, validTimeFormat))
            {
                if (Is24HourFormat(value))
                    return Regex.IsMatch(value, valid24HourFormat);

                return true;
            }

            return false;
        }

        public static bool Is24HourFormat(string value)
        {
            if (value.Length >= 2 && Regex.IsMatch(value, @"^\d{2}((?:\D|$)|\d{2})"))
            {
                int hour = Convert.ToInt32(value.Substring(0, 2));
                if (hour >= 13 && hour <= 23)
                    return true;
                // 4-digit no-separator input with a leading zero (e.g. 0600, 0700) is
                // unambiguously 24-hour (military) format — no AM/PM needed.
                if (hour <= 9 && Regex.IsMatch(value, @"^\d{4}$"))
                    return true;
            }
            return false;
        }

        public static bool TimePeriodPresent(string value)
        {
            return Regex.IsMatch(value, @"(?i)[AP]M$");
        }

        private static bool ContainsMinutes(string value)
        {
            return Regex.IsMatch(value, @"^\d{1,2}:\d{2}");
        }

        private static string CleanTimeString(string value, bool removePeriod = false)
        {
            value = value.Trim();
            value = value.Replace(";", ":");
            value = value.Replace(".", ":");
            value = value.Replace(" ", "");

            bool periodPresent = TimePeriodPresent(value);

            if (periodPresent && removePeriod)
                value = value.Remove(value.Length - 2, 2);

            if (!value.Contains(":"))
            {
                if (Regex.IsMatch(value, @"^(\d{3}|\d)(?:\D|$)"))
                    value = value.Insert(1, ":");
                else
                    value = value.Insert(2, ":");
            }

            if (Regex.IsMatch(value, @"^\d:"))
                value = "0" + value;

            return value;
        }

        private static DateTime ClampToWorkHours(DateTime value)
        {
            if (value.TimeOfDay > WorkHoursStart.TimeOfDay && value.TimeOfDay < WorkHoursEnd.TimeOfDay)
                return value;

            return value.ToString("tt") switch
            {
                "AM" => value.AddHours(12),
                "PM" => value.AddHours(-12),
                _ => value
            };
        }

        public static TimeSpan? StringToTimeSpan(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (!IsValidTimeFormat(value))
                return null;

            bool is24Hour = Is24HourFormat(value);
            value = CleanTimeString(value, is24Hour);

            bool hasPeriod = TimePeriodPresent(value);
            bool hasMinutes = ContainsMinutes(value);

            string timeFormat = is24Hour
                ? (hasMinutes ? "HH:mm" : "HH")
                : (hasMinutes ? "hh:mm" : "hh");

            if (!is24Hour && hasPeriod)
                timeFormat += "tt";

            if (!DateTime.TryParseExact(value, timeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return null;

            // Only clamp ambiguous bare-hour inputs (e.g. "7" → 7 PM).
            // When the user explicitly supplies minutes (e.g. "06:00", "01:01") trust their value.
            if (!hasPeriod && !is24Hour && !hasMinutes)
                parsed = ClampToWorkHours(parsed);

            return parsed.TimeOfDay;
        }
    }
}
