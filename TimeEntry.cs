using System;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading;

namespace TimeTrack
{
    public delegate void TimeEntryChangedEventHandler(bool time_changed);

    // Could be converted to a record for immutable properties
    public partial class TimeEntry : ObservableObject
    {
        #pragma warning disable CS8618
        private readonly DateTime date;
        private readonly int id;
        #pragma warning restore CS8618

        private TimeOnly? start_time;
        private TimeOnly? end_time;

        private volatile TimeEntryChangedEventHandler _timeEntryChanged = delegate { };
        public event TimeEntryChangedEventHandler? TimeEntryChanged
        {
            add { lock (this) { _timeEntryChanged += value; } }
            remove { lock (this) { _timeEntryChanged -= value; } }
        }

        public TimeEntry(DateTime date, int id)
        {
            this.date = date;
            this.id = id;
            start_time = null;
            end_time = null;

            _synchronizationContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException("No SynchronizationContext available");
        }

        public TimeEntry(DateTime date, int id, TimeOnly? start_time, TimeOnly? end_time, string? case_number, string? notes, bool recorded = false)
        {
            _synchronizationContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException("No SynchronizationContext available");

            this.date = date;
            this.id = id;

            StartTime = start_time;
            EndTime = end_time;
            Notes = notes ?? string.Empty;
            Recorded = recorded;
            CaseNumber = case_number ?? string.Empty;
        }

        public DateTime Date => date;
        public int ID => id;

        public TimeOnly? StartTime
        {
            get => start_time;
            set
            {
                if (SetProperty(ref start_time, value))
                {
                    OnPropertyChanged(nameof(Duration));
                    OnPropertyChanged(nameof(IsValid));
                    OnTimeEntryChanged(true);
                }
            }
        }

        public TimeOnly? EndTime
        {
            get => end_time;
            set
            {
                if (SetProperty(ref end_time, value))
                {
                    OnPropertyChanged(nameof(Duration));
                    OnPropertyChanged(nameof(IsValid));
                    OnTimeEntryChanged(true);
                }
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string caseNumber = string.Empty;

        [ObservableProperty]
        private string notes = string.Empty;

        [ObservableProperty]
        private bool recorded;

        private const string TimeFormat = "t";
        public string StartTimeAsString() => start_time?.ToString(TimeFormat) ?? string.Empty;
        public string EndTimeAsString() => end_time?.ToString(TimeFormat) ?? string.Empty;

        public bool CaseIsEmpty() => string.IsNullOrWhiteSpace(CaseNumber);

        public TimeSpan? Duration
        {
            get
            {
                if (!start_time.HasValue || !end_time.HasValue) return null;
                var start = start_time.Value.ToTimeSpan();
                var end = end_time.Value.ToTimeSpan();
                if (end < start) end += TimeSpan.FromDays(1);
                return end - start;
            }
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(CaseNumber) && Duration.HasValue;

        private readonly SynchronizationContext _synchronizationContext;

        protected new void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            var ctx = _synchronizationContext;
            if (ctx != null)
                ctx.Post(_ => base.OnPropertyChanged(name), null);
            else
                base.OnPropertyChanged(name);
        }

        protected void OnTimeEntryChanged(bool time_changed)
        {
            _synchronizationContext.Post(_ => _timeEntryChanged(time_changed), null);
        }
    }

    // Converter: support TimeOnly and TimeSpan
    public sealed class TimeEntryUIConverter : IValueConverter
    {
        private const string TimeFormat = "hh:mm tt";

        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value switch
            {
                null => string.Empty,
                TimeOnly to => to.ToString(TimeFormat, culture),
                TimeSpan ts => (DateTime.Today + ts).ToString(TimeFormat, culture),
                _ => Binding.DoNothing
            };

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is not string s || string.IsNullOrWhiteSpace(s))
            {
                if (targetType == typeof(TimeOnly?) || targetType == typeof(TimeOnly))
                    return null!;
                if (targetType == typeof(TimeSpan?) || targetType == typeof(TimeSpan))
                    return null!;
                return System.Windows.DependencyProperty.UnsetValue;
            }

            var parsed = TimeTrack.TimeStringConverter.StringToTimeSpan(s);
            if (!parsed.HasValue)
                return System.Windows.DependencyProperty.UnsetValue;

            if (targetType == typeof(TimeOnly?) || targetType == typeof(TimeOnly))
                return TimeOnly.FromTimeSpan(parsed.Value);

            if (targetType == typeof(TimeSpan?) || targetType == typeof(TimeSpan))
                return parsed.Value;

            return System.Windows.DependencyProperty.UnsetValue;
        }
    }
}