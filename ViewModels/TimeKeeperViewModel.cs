using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using TimeTrack.Data;
using TimeTrack.Utilities;

namespace TimeTrack.ViewModels
{
    public partial class TimeKeeperViewModel : INotifyPropertyChanged, IDisposable
    {
        private DateTime _date;
        private string _currentDate;
        private int _currentIdCount;
        private ObservableCollection<TimeEntry> _timeRecords;
        private ObservableCollection<DraftEntry> _openEntries;
        private DraftEntry? _focusedEntry;
        private bool _isMainTabFocused = true;
        private readonly DispatcherTimer _autoSaveTimer;
        private readonly DispatcherTimer _uiTimer;
        private bool _disposed;
        private string _startTime = string.Empty;
        private string _endTime = string.Empty;
        private string _ticketNo = string.Empty;
        private string _notes = string.Empty;
        private string _hoursTotal = "-";
        private string _gapsTotal = "-";
        private string _selectedHours = "-";
        private string _selectedMins = "-";
        private string _billableUnits = "-";
        private TimeEntry? _selectedItem;
        private bool _isFocusMode;
        private string _contextSummary = string.Empty;
        private DispatcherTimer _checkInTimer;
        private DispatcherTimer _eodTimer;
        private DateTime _lastInputTime = DateTime.Now;
        private bool _isIdleNudgeShown;

        public event Action<string, string>? TrayNotificationRequested;
        public event Action<string, string>? IdleNudgeRequested;
        public event Action? EodReminderRequested;

        public enum TimerLevel { Normal, Caution, Warning }

        public TimeKeeperViewModel()
        {
            _timeRecords = new ObservableCollection<TimeEntry>();
            _timeRecords.CollectionChanged += TimeRecords_CollectionChanged;
            _date = DateTime.Today.Date;
            _currentDate = _date.Date.ToShortDateString();
            _currentIdCount = 0;

            var drafts = Database.RetrieveDrafts();
            _openEntries = drafts.Count > 0 ? drafts : new ObservableCollection<DraftEntry>();

            _focusedEntry = _openEntries.FirstOrDefault(d => d.IsActive);
            _isMainTabFocused = (_focusedEntry == null);
            if (_focusedEntry != null)
                LoadFocusedEntryIntoFields();

            _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            _autoSaveTimer.Start();

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _uiTimer.Tick += UiTimer_Tick;
            SyncUiTimerState();

            // Check-in timer for periodic notifications while timer is running
            _checkInTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(SettingsManager.CheckInIntervalMinutes) };
            _checkInTimer.Tick += CheckInTimer_Tick;

            // EOD reminder timer — checks every minute if we've passed the configured time
            _eodTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _eodTimer.Tick += EodTimer_Tick;
            if (SettingsManager.EodReminderEnabled)
                _eodTimer.Start();

            RefreshRecentTickets();
            RefreshDayStreak();
            RefreshParkingLotItems();
        }

        private void AutoSaveTimer_Tick(object? sender, EventArgs e) => SaveFocusedEntryToDb();

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(TimerElapsedDisplay));
            OnPropertyChanged(nameof(TimeOfDayLabel));
            OnPropertyChanged(nameof(TimerElapsedLevel));
            OnPropertyChanged(nameof(SessionProgressValue));
            OnPropertyChanged(nameof(IsOvertime));
            OnPropertyChanged(nameof(OvertimeDisplay));
            OnPropertyChanged(nameof(CurrentTimeMarkerPercent));

            // Idle detection check
            if (SettingsManager.IdleDetectionEnabled && _focusedEntry != null && _focusedEntry.IsTimerRunning)
            {
                var idleMinutes = (DateTime.Now - _lastInputTime).TotalMinutes;
                if (idleMinutes >= SettingsManager.IdleThresholdMinutes && !_isIdleNudgeShown)
                {
                    _isIdleNudgeShown = true;
                    var ticket = string.IsNullOrWhiteSpace(_focusedEntry.TicketNumber) ? "current entry" : _focusedEntry.TicketNumber;
                    IdleNudgeRequested?.Invoke($"You've been idle for {(int)idleMinutes} min", $"Still working on {ticket}? Elapsed: {TimerElapsedDisplay}");
                }
            }
        }

        private void CheckInTimer_Tick(object? sender, EventArgs e)
        {
            if (_focusedEntry == null || !_focusedEntry.IsTimerRunning) return;
            var ticket = string.IsNullOrWhiteSpace(_focusedEntry.TicketNumber) ? "current entry" : _focusedEntry.TicketNumber;
            TrayNotificationRequested?.Invoke($"Still working on {ticket}?", $"Elapsed: {TimerElapsedDisplay}");
        }

        private bool _eodFiredToday;
        private DateTime _eodFiredDate = DateTime.MinValue;

        private void EodTimer_Tick(object? sender, EventArgs e)
        {
            if (!SettingsManager.EodReminderEnabled) return;
            if (_eodFiredDate == DateTime.Today && _eodFiredToday) return;

            if (TimeSpan.TryParse(SettingsManager.EodReminderTime, out var eodTime))
            {
                if (DateTime.Now.TimeOfDay >= eodTime)
                {
                    _eodFiredToday = true;
                    _eodFiredDate = DateTime.Today;
                    EodReminderRequested?.Invoke();
                }
            }
        }

        public void RegisterUserInput()
        {
            _lastInputTime = DateTime.Now;
            _isIdleNudgeShown = false;
        }

        public bool HasUnsubmittedEntry
        {
            get
            {
                if (_focusedEntry == null || _isMainTabFocused) return false;
                var hasStart = !string.IsNullOrWhiteSpace(StartTimeField);
                var hasEnd = !string.IsNullOrWhiteSpace(EndTimeField);
                return hasStart && hasEnd;
            }
        }

        public void StartCheckInTimer()
        {
            if (SettingsManager.CheckInEnabled && _focusedEntry != null && _focusedEntry.IsTimerRunning)
            {
                _checkInTimer.Interval = TimeSpan.FromMinutes(SettingsManager.CheckInIntervalMinutes);
                if (!_checkInTimer.IsEnabled)
                    _checkInTimer.Start();
            }
        }

        public void StopCheckInTimer()
        {
            _checkInTimer.Stop();
        }

        private void TimeRecords_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (TimeEntry item in e.NewItems)
                {
                    item.TimeEntryChanged += OnTimeEntryChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (TimeEntry item in e.OldItems)
                {
                    item.TimeEntryChanged -= OnTimeEntryChanged;
                }
            }
        }

        // Properties

        public DateTime Date
        {
            get => _date;
            set
            {
                _date = value;
                CurrentDate = value.Date.ToShortDateString();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsViewingToday));
            }
        }

        public bool IsViewingToday => _date.Date == DateTime.Today;

        public int CurrentIdCount
        {
            set => _currentIdCount = value;
            get => _currentIdCount;
        }

        public int EntryCount => _timeRecords.Count;

        public string EntryCountBadge => _timeRecords.Count > 0 ? $"Main ({_timeRecords.Count})" : "Main";

        public bool IsFocusMode
        {
            get => _isFocusMode;
            set { _isFocusMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsFocusModeVisible)); }
        }

        public bool IsFocusModeVisible => !IsFocusMode;

        public string TimeOfDayLabel
        {
            get
            {
                var hour = DateTime.Now.Hour;
                return hour switch
                {
                < 6 => "🌙 Late Night",
                < 9 => "🌅 Early Morning",
                < 12 => "☀️ Morning",
                < 14 => "🕛 Midday",
                < 17 => "🌤️ Afternoon",
                < 20 => "🌆 Evening",
                < 23 => " twilight",
                _ => "🌙 Night"
                };
            }
        }

        public string ContextSummary
        {
            get => _contextSummary;
            set { _contextSummary = value; OnPropertyChanged(); }
        }

        public TimerLevel TimerElapsedLevel
        {
            get
            {
                if (_focusedEntry == null || !_focusedEntry.IsTimerRunning || string.IsNullOrWhiteSpace(_focusedEntry.TimerStartedAt))
                    return TimerLevel.Normal;
                if (DateTime.TryParse(_focusedEntry.TimerStartedAt, out var startedAt))
                {
                    var elapsed = DateTime.Now - startedAt;
                    if (elapsed.TotalMinutes >= SettingsManager.TimerWarningMinutes)
                        return TimerLevel.Warning;
                    if (elapsed.TotalMinutes >= SettingsManager.TimerCautionMinutes)
                        return TimerLevel.Caution;
                }
                return TimerLevel.Normal;
            }
        }

        public double SessionProgressValue
        {
            get
            {
                if (_focusedEntry == null || !_focusedEntry.IsTimerRunning || string.IsNullOrWhiteSpace(_focusedEntry.TimerStartedAt))
                    return 0;
                if (DateTime.TryParse(_focusedEntry.TimerStartedAt, out var startedAt))
                {
                    var elapsed = DateTime.Now - startedAt;
                    var expected = TimeSpan.FromMinutes(SettingsManager.ExpectedSessionMinutes);
                    return Math.Min(100, elapsed.TotalSeconds / expected.TotalSeconds * 100);
                }
                return 0;
            }
        }

        public bool IsOvertime
        {
            get
            {
                if (_focusedEntry == null || !_focusedEntry.IsTimerRunning || string.IsNullOrWhiteSpace(_focusedEntry.TimerStartedAt))
                    return false;
                if (DateTime.TryParse(_focusedEntry.TimerStartedAt, out var startedAt))
                {
                    var elapsed = DateTime.Now - startedAt;
                    return elapsed.TotalMinutes > SettingsManager.ExpectedSessionMinutes;
                }
                return false;
            }
        }

        public string OvertimeDisplay
        {
            get
            {
                if (!IsOvertime) return string.Empty;
                if (DateTime.TryParse(_focusedEntry?.TimerStartedAt, out var startedAt))
                {
                    var elapsed = DateTime.Now - startedAt;
                    var overtime = elapsed - TimeSpan.FromMinutes(SettingsManager.ExpectedSessionMinutes);
                    return $"Overtime +{(int)overtime.TotalHours}h {overtime.Minutes:D2}m";
                }
                return string.Empty;
            }
        }

        public double DayProgressValue
        {
            get
            {
                double totalSeconds = 0;
                foreach (var entry in _timeRecords)
                {
                    if (entry.Duration.HasValue)
                        totalSeconds += entry.Duration.Value.TotalSeconds;
                }
                var target = TimeSpan.FromHours(8).TotalSeconds;
                return Math.Min(100, totalSeconds / target * 100);
            }
        }

        public string DayAtAGlanceSummary
        {
            get
            {
                int count = _timeRecords.Count;
                if (count == 0) return "No entries yet today";
                return $"{count} {(count == 1 ? "entry" : "entries")} · {HoursTotal} logged · {GapsTotal} gaps";
            }
        }

        public string TimelineLabel
        {
            get
            {
                int start = SettingsManager.TimelineStartHour;
                int end = SettingsManager.TimelineEndHour;
                string FormatHour(int h) => h == 0 ? "12am" : h < 12 ? $"{h}am" : h == 12 ? "12pm" : $"{h - 12}pm";
                return $"Day Timeline ({FormatHour(start)} – {FormatHour(end)})";
            }
        }

        public List<DayTimelineSegment> DayTimelineSegments
        {
            get
            {
                var segments = new List<DayTimelineSegment>();
                double spanMinutes = (SettingsManager.TimelineEndHour - SettingsManager.TimelineStartHour) * 60;
                if (spanMinutes <= 0) spanMinutes = 12 * 60;
                double offsetMinutes = SettingsManager.TimelineStartHour * 60;
                foreach (var entry in _timeRecords.OrderBy(e => e.StartTime))
                {
                    if (entry.StartTime == null || entry.EndTime == null) continue;
                    var start = entry.StartTime.Value.ToTimeSpan();
                    var end = entry.EndTime.Value.ToTimeSpan();
                    if (end <= start) continue;

                    segments.Add(new DayTimelineSegment
                    {
                        StartPercent = (start.TotalMinutes - offsetMinutes) / spanMinutes * 100,
                        WidthPercent = (end - start).TotalMinutes / spanMinutes * 100,
                        TicketNumber = entry.TicketNumber,
                        HasTicket = !string.IsNullOrWhiteSpace(entry.TicketNumber)
                    });
                }
                return segments;
            }
        }

        public double CurrentTimeMarkerPercent
        {
            get
            {
                if (!IsViewingToday) return -1;
                double spanMinutes = (SettingsManager.TimelineEndHour - SettingsManager.TimelineStartHour) * 60;
                if (spanMinutes <= 0) spanMinutes = 12 * 60;
                double offsetMinutes = SettingsManager.TimelineStartHour * 60;
                var now = DateTime.Now.TimeOfDay;
                return (now.TotalMinutes - offsetMinutes) / spanMinutes * 100;
            }
        }

        public ObservableCollection<string> RecentTickets { get; } = new();

        private int _dayStreak;
        public int DayStreak
        {
            get => _dayStreak;
            set { _dayStreak = value; OnPropertyChanged(); }
        }

        public string StreakDisplay => _dayStreak > 0 ? $"{_dayStreak} day streak" : string.Empty;

        public ObservableCollection<ParkingLotItem> ParkingLotItems { get; } = new();

        public void RefreshRecentTickets()
        {
            RecentTickets.Clear();
            foreach (var ticket in Database.GetRecentTickets(10))
                RecentTickets.Add(ticket);
        }

        public void RefreshDayStreak()
        {
            DayStreak = Database.GetDayStreak();
            OnPropertyChanged(nameof(StreakDisplay));
        }

        public void RefreshParkingLotItems()
        {
            ParkingLotItems.Clear();
            foreach (var item in Database.RetrieveParkingLotItems())
                ParkingLotItems.Add(item);
        }

        public void AddParkingLotItem(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var id = Database.AddParkingLotItem(text.Trim());
            if (id > 0)
            {
                ParkingLotItems.Insert(0, new ParkingLotItem
                {
                    Id = id,
                    Text = text.Trim(),
                    CreatedAt = DateTime.Now
                });
            }
        }

        public void ResolveParkingLotItem(ParkingLotItem item)
        {
            Database.ResolveParkingLotItem(item.Id);
            item.ResolvedAt = DateTime.Now;
            OnPropertyChanged(nameof(ParkingLotItems));
        }

        public void DeleteParkingLotItem(ParkingLotItem item)
        {
            Database.DeleteParkingLotItem(item.Id);
            ParkingLotItems.Remove(item);
        }

        public ObservableCollection<TimeEntry> Entries
        {
            get => _timeRecords;
            set
            {
                if (_timeRecords == value)
                    return;

                if (_timeRecords != null)
                {
                    _timeRecords.CollectionChanged -= TimeRecords_CollectionChanged;
                    RemoveChangedHandlerFromEntries(_timeRecords);
                }

                _timeRecords = value ?? new ObservableCollection<TimeEntry>();
                _timeRecords.CollectionChanged += TimeRecords_CollectionChanged;
                AddChangedHandlerToAllEntries();
                OnPropertyChanged();
                OnPropertyChanged(nameof(EntryCount));
                OnPropertyChanged(nameof(EntryCountBadge));
            }
        }

        public string CurrentDate
        {
            get => _currentDate;
            set { _currentDate = value; OnPropertyChanged(); }
        }

        public string StartTimeField
        {
            get => _startTime;
            set
            {
                _startTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EntryDurationDisplay));
                if (_focusedEntry != null) _focusedEntry.StartTime = value;
                UpdateSelectedTime();
            }
        }

        public string EndTimeField
        {
            get => _endTime;
            set
            {
                _endTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EntryDurationDisplay));
                if (_focusedEntry != null) _focusedEntry.EndTime = value;
                UpdateSelectedTime();
            }
        }

        public TimeSpan? StartTimeFieldAsTime() => TimeStringConverter.StringToTimeSpan(_startTime);
        public TimeSpan? EndTimeFieldAsTime() => TimeStringConverter.StringToTimeSpan(_endTime);

        public string TicketNumberField
        {
            get => _ticketNo;
            set
            {
                _ticketNo = value;
                OnPropertyChanged();
                if (_focusedEntry != null) _focusedEntry.TicketNumber = value;
            }
        }

        public string NotesField
        {
            get => _notes;
            set
            {
                _notes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NotesCharacterCount));
                if (_focusedEntry != null) _focusedEntry.Notes = value;
            }
        }

        public string NotesCharacterCount
        {
            get
            {
                int count = _notes?.Length ?? 0;
                return count == 1 ? "1 character" : $"{count} characters";
            }
        }

        public string HoursTotal
        {
            get => _hoursTotal;
            set { _hoursTotal = value; OnPropertyChanged(); }
        }

        public string GapsTotal
        {
            get => _gapsTotal;
            set { _gapsTotal = value; OnPropertyChanged(); }
        }

        public string SelectedHours
        {
            get => _selectedHours;
            set { _selectedHours = value; OnPropertyChanged(); }
        }

        public string SelectedMins
        {
            get => _selectedMins;
            set { _selectedMins = value; OnPropertyChanged(); }
        }

        public string BillableUnits
        {
            get => _billableUnits;
            set { _billableUnits = value; OnPropertyChanged(); }
        }

        public TimeEntry? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem == value) return;
                _selectedItem = value;
                OnPropertyChanged();
                UpdateSelectedTime();
            }
        }

        public ObservableCollection<DraftEntry> OpenEntries
        {
            get => _openEntries;
            set { _openEntries = value; OnPropertyChanged(); }
        }

        public DraftEntry? FocusedEntry
        {
            get => _focusedEntry;
            private set { _focusedEntry = value; OnPropertyChanged(); }
        }

        public bool IsMainTabFocused
        {
            get => _isMainTabFocused;
            private set { _isMainTabFocused = value; OnPropertyChanged(); }
        }

        public string EntryDurationDisplay
        {
            get
            {
                var start = StartTimeFieldAsTime();
                var end = EndTimeFieldAsTime();
                if (!start.HasValue || !end.HasValue) return string.Empty;
                var duration = end.Value - start.Value;
                if (duration <= TimeSpan.Zero) return string.Empty;
                int blocks = Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes / 6.0));
                double units = blocks / 10.0;
                string timeStr = duration.Hours > 0
                    ? $"{duration.Hours}h {duration.Minutes:D2}m"
                    : $"{duration.Minutes}m";
                return $"{timeStr}  ·  {units:F1} units";
            }
        }

        // Running timer

        public bool IsTimerRunning => _focusedEntry?.IsTimerRunning ?? false;

        public string TimerElapsedDisplay
        {
            get
            {
                if (_focusedEntry == null || !_focusedEntry.IsTimerRunning || string.IsNullOrWhiteSpace(_focusedEntry.TimerStartedAt))
                    return string.Empty;

                if (DateTime.TryParse(_focusedEntry.TimerStartedAt, out var startedAt))
                {
                    var elapsed = DateTime.Now - startedAt;
                    if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
                    return $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
                }
                return string.Empty;
            }
        }

        public void StartTimer()
        {
            if (_isMainTabFocused || _focusedEntry == null) return;
            _focusedEntry.TimerStartedAt = DateTime.Now.ToString("o");
            StartTimeField = DateTime.Now.ToString("hh:mm tt", CultureInfo.CurrentCulture);
            EndTimeField = string.Empty;
            _focusedEntry.IsTimerRunning = true;
            SyncUiTimerState();
            OnPropertyChanged(nameof(IsTimerRunning));
            OnPropertyChanged(nameof(TimerElapsedDisplay));
            SaveFocusedEntryToDb();
            StartCheckInTimer();
        }

        public void StopTimer()
        {
            if (_focusedEntry == null || !_focusedEntry.IsTimerRunning) return;
            EndTimeField = DateTime.Now.ToString("hh:mm tt", CultureInfo.CurrentCulture);
            _focusedEntry.IsTimerRunning = false;
            SyncUiTimerState();
            OnPropertyChanged(nameof(IsTimerRunning));
            OnPropertyChanged(nameof(TimerElapsedDisplay));
            SaveFocusedEntryToDb();
            StopCheckInTimer();
        }

        private void ResetTimer()
        {
            // No-op: timer state is now per-entry and persists when switching tabs
        }

        public void SetStartTimeToNow() =>
            StartTimeField = DateTime.Now.ToString("hh:mm tt", CultureInfo.CurrentCulture);

        public void SetEndTimeToNow() =>
            EndTimeField = DateTime.Now.ToString("hh:mm tt", CultureInfo.CurrentCulture);

        public void SplitEntry()
        {
            if (_isMainTabFocused || _focusedEntry == null) return;

            // Stop timer and stamp end time on current entry
            if (_focusedEntry.IsTimerRunning)
            {
                StopTimer();
            }
            else
            {
                EndTimeField = DateTime.Now.ToString("hh:mm tt", CultureInfo.CurrentCulture);
            }

            // Save current entry state
            SaveFocusedEntryToDb();

            // Create new entry with start time = now
            string startTime = DateTime.Now.ToString("hh:mm tt", CultureInfo.CurrentCulture);
            var newDraft = Database.SaveDraft(string.Empty, string.Empty, startTime, string.Empty, isActive: false);
            if (newDraft == null) return;

            _openEntries.Add(newDraft);
            SetFocusEntry(newDraft);
        }

        // Open entries / tab management

        public void NewEntry()
        {
            SaveFocusedEntryToDb();
            string startTime = DateTime.Now.ToString("hh:mm tt", CultureInfo.CurrentCulture);

            // Continue from Last Entry: auto-fill start time from last submitted entry's end time
            if (SettingsManager.ContinueFromLastEntry && _timeRecords.Count > 0)
            {
                var lastEntry = _timeRecords
                    .Where(e => e.EndTime.HasValue)
                    .OrderByDescending(e => e.EndTime!.Value)
                    .FirstOrDefault();
                if (lastEntry?.EndTime != null)
                {
                    startTime = DateTime.Today.Add(lastEntry.EndTime.Value.ToTimeSpan())
                        .ToString("hh:mm tt", CultureInfo.CurrentCulture);
                }
            }

            var draft = Database.SaveDraft(string.Empty, string.Empty, startTime, string.Empty, isActive: false);
            if (draft == null) return;
            _openEntries.Add(draft);
            SetFocusEntry(draft);
        }

        public void EditEntry(TimeEntry entry)
        {
            SaveFocusedEntryToDb();
            string startTimeStr = entry.StartTime?.ToString("hh:mm tt", CultureInfo.CurrentCulture) ?? string.Empty;
            string endTimeStr = entry.EndTime?.ToString("hh:mm tt", CultureInfo.CurrentCulture) ?? string.Empty;
            var draft = Database.SaveDraft(entry.TicketNumber, entry.Notes, startTimeStr, endTimeStr, isActive: false);
            if (draft == null) return;
            draft.EditingEntry = (entry.Date, entry.ID);
            _openEntries.Add(draft);
            SetFocusEntry(draft);
        }

        public void FocusMainTab()
        {
            SaveFocusedEntryToDb();
            foreach (var d in _openEntries)
                d.IsActive = false;
            if (_focusedEntry != null)
                Database.UpdateDraft(_focusedEntry);
            _focusedEntry = null;
            _isMainTabFocused = true;
            OnPropertyChanged(nameof(FocusedEntry));
            OnPropertyChanged(nameof(IsMainTabFocused));
            OnPropertyChanged(nameof(IsTimerRunning));
            OnPropertyChanged(nameof(TimerElapsedDisplay));
            SyncUiTimerState();
        }

        public void SetFocusEntry(DraftEntry entry)
        {
            SaveFocusedEntryToDb();
            foreach (var d in _openEntries)
                d.IsActive = false;
            entry.IsActive = true;
            Database.UpdateDraft(entry);
            _focusedEntry = entry;
            _isMainTabFocused = false;
            OnPropertyChanged(nameof(FocusedEntry));
            OnPropertyChanged(nameof(IsMainTabFocused));
            OnPropertyChanged(nameof(IsTimerRunning));
            OnPropertyChanged(nameof(TimerElapsedDisplay));
            LoadFocusedEntryIntoFields();
            SyncUiTimerState();

            // Context Recovery: show "What was I doing?" summary
            if (SettingsManager.ContextSummaryEnabled)
            {
                UpdateContextSummary(entry);
            }
        }

        private void UpdateContextSummary(DraftEntry entry)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(entry.TicketNumber))
                parts.Add($"Ticket: {entry.TicketNumber}");
            if (!string.IsNullOrWhiteSpace(entry.Notes))
            {
                var firstLine = entry.Notes.Split('\n')[0];
                if (firstLine.Length > 60) firstLine = firstLine[..60] + "...";
                parts.Add(firstLine);
            }
            if (entry.IsTimerRunning)
                parts.Add("Timer running");
            ContextSummary = parts.Count > 0 ? "Continuing: " + string.Join(" · ", parts) : string.Empty;
        }

        public void CloseEntry(DraftEntry entry)
        {
            bool wasFocused = (_focusedEntry == entry);
            Database.DeleteDraft(entry.Id);
            int index = _openEntries.IndexOf(entry);
            _openEntries.Remove(entry);

            if (!wasFocused) return;

            if (_openEntries.Count == 0)
            {
                _focusedEntry = null;
                _isMainTabFocused = true;
                OnPropertyChanged(nameof(FocusedEntry));
                OnPropertyChanged(nameof(IsMainTabFocused));
                return;
            }
            else
            {
                int newIndex = Math.Min(index, _openEntries.Count - 1);
                var next = _openEntries[newIndex];
                next.IsActive = true;
                Database.UpdateDraft(next);
                _focusedEntry = next;
            }

            OnPropertyChanged(nameof(FocusedEntry));
            OnPropertyChanged(nameof(IsTimerRunning));
            OnPropertyChanged(nameof(TimerElapsedDisplay));
            LoadFocusedEntryIntoFields();
            SyncUiTimerState();
        }

        private void SaveFocusedEntryToDb()
        {
            if (_focusedEntry == null) return;
            _focusedEntry.TicketNumber = _ticketNo;
            _focusedEntry.Notes = _notes;
            _focusedEntry.StartTime = _startTime;
            _focusedEntry.EndTime = _endTime;
            Database.UpdateDraft(_focusedEntry);
        }

        public bool CanUndoNotes => _focusedEntry != null;

        public void UndoNotes()
        {
            if (_focusedEntry == null) return;

            var lastHistory = Database.GetLastNotesHistory(_focusedEntry.Id);
            if (lastHistory == null) return;

            // Restore the previous notes directly (bypasses history logging)
            NotesField = lastHistory.Notes;
            if (_focusedEntry != null)
            {
                _focusedEntry.Notes = lastHistory.Notes;
                Database.UpdateDraftNotesDirect(_focusedEntry.Id, lastHistory.Notes);
            }

            // Remove the restored history record so the next undo goes further back
            Database.RemoveNotesHistoryRecord(lastHistory.Id);
            OnPropertyChanged(nameof(CanUndoNotes));
        }

        private void LoadFocusedEntryIntoFields()
        {
            if (_focusedEntry == null)
            {
                TicketNumberField = string.Empty;
                NotesField = string.Empty;
                SetStartTimeField();
                EndTimeField = string.Empty;
                return;
            }
            TicketNumberField = _focusedEntry.TicketNumber;
            NotesField = _focusedEntry.Notes;
            StartTimeField = string.IsNullOrWhiteSpace(_focusedEntry.StartTime)
                ? DateTime.Now.ToString("hh:mm tt", CultureInfo.CurrentCulture)
                : _focusedEntry.StartTime;
            EndTimeField = _focusedEntry.EndTime ?? string.Empty;
        }

        // Methods

        public void AddEntry(DateTime date, int id, TimeSpan startTime, TimeSpan endTime, string ticketNumber = "", string notes = "")
        {
            var entry = new TimeEntry(date, id, TimeOnly.FromTimeSpan(startTime), TimeOnly.FromTimeSpan(endTime), ticketNumber, notes);
            entry.TimeEntryChanged += OnTimeEntryChanged;
            _timeRecords.Add(entry);
            UpdateTimeTotals();
            OnPropertyChanged(nameof(EntryCount));
            OnPropertyChanged(nameof(EntryCountBadge));
        }

        public bool InsertBlankEntry(int index)
        {
            if (_timeRecords.Count == 0)
                return false;

            if (index < 0 || index > _timeRecords.Count)
                index = _timeRecords.Count;

            _timeRecords.Insert(index, new TimeEntry(_date, ++_currentIdCount));
            UpdateTimeTotals();
            OnPropertyChanged(nameof(EntryCount));
            OnPropertyChanged(nameof(EntryCountBadge));
            return true;
        }

        public bool SubmitEntry()
        {
            TimeSpan? startTime = StartTimeFieldAsTime();
            TimeSpan? endTime = EndTimeFieldAsTime();

            if (startTime == null || endTime == null)
                return false;

            if (_focusedEntry != null && _focusedEntry.IsEditing)
            {
                var (date, id) = _focusedEntry.EditingEntry!.Value;
                var existing = _timeRecords.FirstOrDefault(e => e.Date == date && e.ID == id);
                if (existing != null)
                {
                    existing.StartTime = TimeOnly.FromTimeSpan((TimeSpan)startTime);
                    existing.EndTime = TimeOnly.FromTimeSpan((TimeSpan)endTime);
                    existing.TicketNumber = _ticketNo;
                    existing.Notes = _notes;
                    return true;
                }
            }

            AddEntry(_date, ++_currentIdCount, (TimeSpan)startTime, (TimeSpan)endTime, _ticketNo, _notes);
            RefreshRecentTickets();
            RefreshDayStreak();
            return true;
        }

        [RelayCommand]
        private void RemoveCurrentlySelectedEntry()
        {
            var item = SelectedItem;
            if (item == null)
                return;

            item.TimeEntryChanged -= OnTimeEntryChanged;
            Database.Delete(item.Date, item.ID);
            Entries.Remove(item);

            SelectLastEntry();
            UpdateTimeTotals();
            SetStartTimeField();
            Database.Update(Entries);
            OnPropertyChanged(nameof(EntryCount));
            OnPropertyChanged(nameof(EntryCountBadge));
        }

        // Expose the generated command with the old name for backward compatibility
        public ICommand RemoveCommand => RemoveCurrentlySelectedEntryCommand;

        public int RemoveRecordedEntries()
        {
            var toDelete = Entries.Where(e => e.Recorded).ToList();
            if (toDelete.Count == 0) return 0;

            foreach (var item in toDelete)
            {
                item.TimeEntryChanged -= OnTimeEntryChanged;
                Database.Delete(item.Date, item.ID);
                Entries.Remove(item);
            }

            SelectLastEntry();
            UpdateTimeTotals();
            SetStartTimeField();
            Database.Update(Entries);
            OnPropertyChanged(nameof(EntryCount));
            OnPropertyChanged(nameof(EntryCountBadge));
            return toDelete.Count;
        }

        public void SelectLastEntry()
        {
            if (Entries.Count > 0)
                SelectedItem = Entries.Last();
            else
                UpdateSelectedTime();
        }

        public void ClearFieldsAndSetStartTime()
        {
            SetStartTimeField();
            EndTimeField = string.Empty;
            TicketNumberField = string.Empty;
            NotesField = string.Empty;
            UpdateSelectedTime(); // Clear the selected time display
        }

        public void UpdateTimeTotals()
        {
            TimeSpan time = TimeSpan.Zero;
            int totalUnits = 0;

            // Billable hours/units: only entries that have a ticket number.
            foreach (var entry in Entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.TicketNumber) && entry.Duration.HasValue)
                {
                    var d = entry.Duration.Value;
                    time += d;
                    totalUnits += Math.Max(1, (int)Math.Ceiling(d.TotalMinutes / 6.0));
                }
            }

            // Gaps: unaccounted minutes between consecutive entries across the day.
            // Any entry with a valid start/end (including lunch) counts as covered time.
            TimeSpan gap = TimeSpan.Zero;
            TimeOnly? lastEnd = null;
            var ordered = Entries
                .Where(e => e.StartTime.HasValue && e.EndTime.HasValue)
                .OrderBy(e => e.StartTime!.Value)
                .ToList();

            foreach (var entry in ordered)
            {
                var start = entry.StartTime!.Value;
                var end = entry.EndTime!.Value;
                if (lastEnd.HasValue && start > lastEnd.Value)
                    gap += start - lastEnd.Value;
                if (!lastEnd.HasValue || end > lastEnd.Value)
                    lastEnd = end;
            }

            HoursTotal = $"{(int)time.TotalHours}h {time.Minutes}m";
            GapsTotal = $"{(int)gap.TotalHours}h {gap.Minutes}m";
            BillableUnits = (totalUnits / 10.0).ToString("F1");
            OnPropertyChanged(nameof(DayProgressValue));
            OnPropertyChanged(nameof(DayAtAGlanceSummary));
            OnPropertyChanged(nameof(DayTimelineSegments));
            OnPropertyChanged(nameof(CurrentTimeMarkerPercent));
            OnPropertyChanged(nameof(TimelineLabel));
        }

        public void UpdateSelectedTime()
        {
            TimeSpan? duration = null;

            // First priority: Show time for selected grid entry
            if (SelectedItem != null)
            {
                var timeSpan = (SelectedItem.EndTime - SelectedItem.StartTime);
                if (timeSpan != null)
                {
                    duration = (TimeSpan)timeSpan;
                    SelectedHours = duration.Value.Hours.ToString();
                    SelectedMins = duration.Value.Minutes.ToString();
                    return;
                }
            }

            // Second priority: Calculate from input fields (for entry being created)
            var startTime = StartTimeFieldAsTime();
            var endTime = EndTimeFieldAsTime();

            if (startTime.HasValue && endTime.HasValue)
            {
                duration = endTime.Value - startTime.Value;

                // Handle overnight shifts (end time before start time)
                if (duration < TimeSpan.Zero)
                    duration = duration.Value + TimeSpan.FromDays(1);

                SelectedHours = duration.Value.Hours.ToString();
                SelectedMins = duration.Value.Minutes.ToString();
                return;
            }

            // Default: Show dashes when no time can be calculated
            SelectedHours = "-";
            SelectedMins = "-";
        }

        public void SetStartTimeField()
        {
            if (!_isMainTabFocused) return;
            StartTimeField = DateTime.Now.ToString("hh:mm tt", CultureInfo.CurrentCulture);
        }

        public void AddChangedHandlerToAllEntries()
        {
            foreach (var entry in _timeRecords)
            {
                entry.TimeEntryChanged += OnTimeEntryChanged;
            }
        }

        private void RemoveChangedHandlerFromEntries(ObservableCollection<TimeEntry> entries)
        {
            foreach (var entry in entries)
            {
                entry.TimeEntryChanged -= OnTimeEntryChanged;
            }
        }

        private void SyncUiTimerState()
        {
            bool needTimer = (_focusedEntry != null && _focusedEntry.IsTimerRunning)
                || SettingsManager.TimeOfDayLabelEnabled;
            if (needTimer)
            {
                if (!_uiTimer.IsEnabled)
                    _uiTimer.Start();
            }
            else if (_uiTimer.IsEnabled)
            {
                _uiTimer.Stop();
            }
        }

        public void OnTimeEntryChanged(bool timeChanged)
        {
            if (timeChanged)
            {
                UpdateTimeTotals();
                UpdateSelectedTime();
                SetStartTimeField();
            }
            Database.Update(_timeRecords);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _autoSaveTimer.Tick -= AutoSaveTimer_Tick;
            _autoSaveTimer.Stop();

            _uiTimer.Tick -= UiTimer_Tick;
            _uiTimer.Stop();

            _checkInTimer.Tick -= CheckInTimer_Tick;
            _checkInTimer.Stop();

            _eodTimer.Tick -= EodTimer_Tick;
            _eodTimer.Stop();

            if (_timeRecords != null)
            {
                _timeRecords.CollectionChanged -= TimeRecords_CollectionChanged;
                RemoveChangedHandlerFromEntries(_timeRecords);
            }
        }

        // INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class DayTimelineSegment
    {
        public double StartPercent { get; set; }
        public double WidthPercent { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public bool HasTicket { get; set; }
    }
}
