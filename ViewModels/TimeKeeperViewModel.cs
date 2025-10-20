using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
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
        private string _startTime = string.Empty;
        private string _endTime = string.Empty;
        private string _ticketNo = string.Empty;
        private string _notes = string.Empty;
        private double _hoursTotal;
        private double _gapsTotal;
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
            set => _date = value;
        }

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
                UpdateSelectedTime(); // Update selected time when start time changes
            }
        }

        public string EndTimeField
        {
            get => _endTime;
            set 
            { 
                _endTime = value; 
                OnPropertyChanged();
                UpdateSelectedTime(); // Update selected time when end time changes
            }
        }

        public TimeSpan? StartTimeFieldAsTime() => TimeStringConverter.StringToTimeSpan(_startTime);
        public TimeSpan? EndTimeFieldAsTime() => TimeStringConverter.StringToTimeSpan(_endTime);

        public string TicketNumberField
        {
            get => _ticketNo;
            set { _ticketNo = value; OnPropertyChanged(); }
        }

        public string NotesField
        {
            get => _notes;
            set { _notes = value; OnPropertyChanged(); }
        }

        public double HoursTotal
        {
            get => _hoursTotal;
            set { _hoursTotal = value; OnPropertyChanged(); }
        }

        public double GapsTotal
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

            if (index < 0 | index > _timeRecords.Count)
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
            TimeSpan gap = TimeSpan.Zero;

            foreach (var entry in Entries)
            {
                if (entry.StartTime != null && entry.EndTime != null)
                {
                    if (!string.IsNullOrWhiteSpace(entry.TicketNumber))
                        time += (TimeSpan)(entry.EndTime - entry.StartTime);
                    else
                    {
                        if (entry.Notes != null && entry.Notes.Equals("lunch", StringComparison.OrdinalIgnoreCase))
                            continue;
                        else
                            gap += (TimeSpan)(entry.EndTime - entry.StartTime);
                    }
                }
            }

            HoursTotal = Math.Round(time.TotalHours, 2, MidpointRounding.AwayFromZero);
            GapsTotal = gap.TotalMinutes;
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
                    CalculateBillableUnits(duration.Value);
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
                CalculateBillableUnits(duration.Value);
                return;
            }

            // Default: Show dashes when no time can be calculated
            SelectedHours = "-";
            SelectedMins = "-";
            BillableUnits = "-";
        }

        private void CalculateBillableUnits(TimeSpan duration)
        {
            // Calculate billable units in 6-minute blocks, rounding UP
            // Since time is only tracked in whole minutes, 0 minutes means 0-59 seconds
            // Minimum billable unit is 0.1 (6 minutes) for any work done
            // 0-6 minutes = 0.1, 7-12 minutes = 0.2, 13-18 minutes = 0.3, etc.
            double totalMinutes = duration.TotalMinutes;
            
            if (totalMinutes < 0)
            {
                // Negative duration (shouldn't happen, but safety check)
                BillableUnits = "0.0";
                return;
            }
            
            // Round up to nearest 6-minute block
            // Even 0 minutes (0-59 seconds) gets billed as minimum 0.1
            int blocks = Math.Max(1, (int)Math.Ceiling(totalMinutes / 6.0));
            double units = blocks / 10.0;
            
            // Format with 1 decimal place
            BillableUnits = units.ToString("F1");
        }

        public void SetStartTimeField()
        {
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
