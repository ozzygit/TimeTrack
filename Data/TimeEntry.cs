using System;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading;
using TimeTrack.Utilities;

namespace TimeTrack.Data
{
    public delegate void TimeEntryChangedEventHandler(bool timeChanged);

    public partial class TimeEntry : ObservableObject
    {
        #pragma warning disable CS8618
        private readonly DateTime date;
        private readonly int id;
        #pragma warning restore CS8618

        private TimeOnly? _startTime;
        private TimeOnly? _endTime;

        private event TimeEntryChangedEventHandler? _timeEntryChanged;
        public event TimeEntryChangedEventHandler TimeEntryChanged
        {
            add { if (value is not null) _timeEntryChanged += value; }
            remove { if (value is not null) _timeEntryChanged -= value; }
        }

        public TimeEntry(DateTime date, int id)
        {
            this.date = date;
            this.id = id;
            _startTime = null;
            _endTime = null;

            _synchronizationContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException("No SynchronizationContext available");
        }

        public TimeEntry(DateTime date, int id, TimeOnly? startTime, TimeOnly? endTime, string? ticketNumber, string? notes, bool recorded = false)
        {
            _synchronizationContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException("No SynchronizationContext available");

            this.date = date;
            this.id = id;

            StartTime = startTime;
            EndTime = endTime;
            Notes = notes ?? string.Empty;
            Recorded = recorded;
            TicketNumber = ticketNumber ?? string.Empty;
        }

        public DateTime Date => date;
        public int ID => id;

        public TimeOnly? StartTime
        {
            get => _startTime;
            set
            {
                if (SetProperty(ref _startTime, value))
                {
                    OnPropertyChanged(nameof(Duration));
                    OnPropertyChanged(nameof(IsValid));
                    OnTimeEntryChanged(true);
                }
            }
        }

        public TimeOnly? EndTime
        {
            get => _endTime;
            set
            {
                if (SetProperty(ref _endTime, value))
                {
                    OnPropertyChanged(nameof(Duration));
                    OnPropertyChanged(nameof(IsValid));
                    OnTimeEntryChanged(true);
                }
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string ticketNumber = string.Empty;

        [ObservableProperty]
        private string notes = string.Empty;

        [ObservableProperty]
        private bool recorded;

        private const string TimeFormat = "t";
        public string StartTimeAsString() => _startTime?.ToString(TimeFormat) ?? string.Empty;
        public string EndTimeAsString() => _endTime?.ToString(TimeFormat) ?? string.Empty;

        public bool TicketIsEmpty() => string.IsNullOrWhiteSpace(TicketNumber);

        public TimeSpan? Duration
        {
            get
            {
                if (!_startTime.HasValue || !_endTime.HasValue) return null;
                var start = _startTime.Value.ToTimeSpan();
                var end = _endTime.Value.ToTimeSpan();
                
                // If end equals start, duration is zero (not overnight)
                if (end == start) return TimeSpan.Zero;
                
                // If end is before start, assume it's an overnight shift (spans to next day)
                if (end < start) end += TimeSpan.FromDays(1);
                
                return end - start;
            }
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(TicketNumber) && Duration.HasValue;

        private readonly SynchronizationContext _synchronizationContext;

        protected new void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(name));
        }

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            var ctx = _synchronizationContext;
            if (ctx == null || ctx == SynchronizationContext.Current)
            {
                base.OnPropertyChanged(e);
                return;
            }

            ctx.Post(_ => base.OnPropertyChanged(e), null);
        }

        protected void OnTimeEntryChanged(bool timeChanged)
        {
            var handler = _timeEntryChanged;
            if (handler is null) return;
            _synchronizationContext.Post(_ => handler(timeChanged), null);
        }
    }

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

            var parsed = TimeStringConverter.StringToTimeSpan(s);
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