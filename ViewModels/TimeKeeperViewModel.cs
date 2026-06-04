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
    public partial class TimeKeeperViewModel : INotifyPropertyChanged
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
        private bool _isTimerRunning;
        private DateTime _timerStartedAt;
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

            _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _autoSaveTimer.Tick += (_, _) => SaveFocusedEntryToDb();
            _autoSaveTimer.Start();

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _uiTimer.Tick += (_, _) => OnPropertyChanged(nameof(TimerElapsedDisplay));
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
        }

        public ObservableCollection<TimeEntry> Entries
        {
            get => _timeRecords;
            set 
            { 
                _timeRecords = value; 
                OnPropertyChanged(); 
                AddChangedHandlerToAllEntries(); 
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
                int units = (int)Math.Ceiling(duration.TotalMinutes / 6.0);
                string timeStr = duration.Hours > 0
                    ? $"{duration.Hours}h {duration.Minutes:D2}m"
                    : $"{duration.Minutes}m";
                return $"{timeStr}  ·  {units} units";
            }
        }

        // Running timer

        public bool IsTimerRunning
        {
            get => _isTimerRunning;
            private set
            {
                _isTimerRunning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimerElapsedDisplay));
            }
        }

        public string TimerElapsedDisplay
        {
            get
            {
                if (!_isTimerRunning) return string.Empty;
                var elapsed = DateTime.Now - _timerStartedAt;
                if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
                return $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            }
        }

        public void StartTimer()
        {
            if (_isMainTabFocused) return;
            _timerStartedAt = DateTime.Now;
            StartTimeField = _timerStartedAt.ToString("hh:mm tt", CultureInfo.CurrentCulture);
            EndTimeField = string.Empty;
            IsTimerRunning = true;
            _uiTimer.Start();
        }

        public void StopTimer()
        {
            if (!_isTimerRunning) return;
            EndTimeField = DateTime.Now.ToString("hh:mm tt", CultureInfo.CurrentCulture);
            IsTimerRunning = false;
            _uiTimer.Stop();
        }

        private void ResetTimer()
        {
            if (!_isTimerRunning) return;
            IsTimerRunning = false;
            _uiTimer.Stop();
        }

        public void SetStartTimeToNow() =>
            StartTimeField = DateTime.Now.ToString("hh:mm tt", CultureInfo.CurrentCulture);

        public void SetEndTimeToNow() =>
            EndTimeField = DateTime.Now.ToString("hh:mm tt", CultureInfo.CurrentCulture);

        // Open entries / tab management

        public void NewEntry()
        {
            SaveFocusedEntryToDb();
            string startTime = DateTime.Now.ToString("hh:mm tt", CultureInfo.CurrentCulture);
            var draft = Database.SaveDraft(string.Empty, string.Empty, startTime, string.Empty, isActive: false);
            if (draft == null) return;
            _openEntries.Add(draft);
            SetFocusEntry(draft);
        }

        public void FocusMainTab()
        {
            ResetTimer();
            SaveFocusedEntryToDb();
            foreach (var d in _openEntries)
                d.IsActive = false;
            if (_focusedEntry != null)
                Database.UpdateDraft(_focusedEntry);
            _focusedEntry = null;
            _isMainTabFocused = true;
            OnPropertyChanged(nameof(FocusedEntry));
            OnPropertyChanged(nameof(IsMainTabFocused));
        }

        public void SetFocusEntry(DraftEntry entry)
        {
            ResetTimer();
            SaveFocusedEntryToDb();
            foreach (var d in _openEntries)
                d.IsActive = false;
            entry.IsActive = true;
            Database.UpdateDraft(entry);
            _focusedEntry = entry;
            _isMainTabFocused = false;
            OnPropertyChanged(nameof(FocusedEntry));
            OnPropertyChanged(nameof(IsMainTabFocused));
            LoadFocusedEntryIntoFields();
        }

        public void CloseEntry(DraftEntry entry)
        {
            bool wasFocused = (_focusedEntry == entry);
            if (wasFocused) ResetTimer();
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
            LoadFocusedEntryIntoFields();
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
        }

        public bool InsertBlankEntry(int index)
        {
            if (_timeRecords.Count == 0)
                return false;

            if (index < 0 || index > _timeRecords.Count)
                index = _timeRecords.Count;

            _timeRecords.Insert(index, new TimeEntry(_date, ++_currentIdCount));
            UpdateTimeTotals();
            return true;
        }

        public bool SubmitEntry()
        {
            TimeSpan? startTime = StartTimeFieldAsTime();
            TimeSpan? endTime = EndTimeFieldAsTime();

            if (startTime == null || endTime == null)
                return false;

            AddEntry(_date, ++_currentIdCount, (TimeSpan)startTime, (TimeSpan)endTime, _ticketNo, _notes);
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
        }

        // Expose the generated command with the old name for backward compatibility
        public ICommand RemoveCommand => RemoveCurrentlySelectedEntryCommand;

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
                    totalUnits += Math.Max(0, (int)Math.Ceiling(d.TotalMinutes / 6.0));
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

        // INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
