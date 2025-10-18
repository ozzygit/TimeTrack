using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TimeTrack
{
    public partial class MainWindow : Window
    {
        private TimeKeeper time_keeper;
        private Brush BtnBrush;

        public static readonly RoutedUICommand ExportCommand =
            new RoutedUICommand("Export Selected", "Export", typeof(MainWindow),
                new InputGestureCollection { new KeyGesture(Key.E, ModifierKeys.Control) });

        public static readonly RoutedUICommand InsertCommand =
            new RoutedUICommand("Insert Record", "Insert", typeof(MainWindow),
                new InputGestureCollection { new KeyGesture(Key.I, ModifierKeys.Control) });

        public static readonly RoutedUICommand TodayCommand =
            new RoutedUICommand("Today", "Today", typeof(MainWindow),
                new InputGestureCollection { new KeyGesture(Key.T, ModifierKeys.Control) });

        public static readonly RoutedUICommand PrevDayCommand =
            new RoutedUICommand("Previous Day", "PrevDay", typeof(MainWindow),
                new InputGestureCollection { new KeyGesture(Key.Left, ModifierKeys.Control) });

        public static readonly RoutedUICommand NextDayCommand =
            new RoutedUICommand("Next Day", "NextDay", typeof(MainWindow),
                new InputGestureCollection { new KeyGesture(Key.Right, ModifierKeys.Control) });

        public static readonly RoutedUICommand OptionsCommand =
            new RoutedUICommand("Options", "Options", typeof(MainWindow),
                new InputGestureCollection { new KeyGesture(Key.O, ModifierKeys.Control) });

        public static readonly RoutedUICommand HelpCommand =
            new RoutedUICommand("About", "Help", typeof(MainWindow),
                new InputGestureCollection { new KeyGesture(Key.F1) });

        public MainWindow()
        {
            InitializeComponent();
            time_keeper = DataContext as TimeKeeper;

            InitializeTimePickerComboBoxes();

            if (Database.Exists())
                LoadEntriesForDate(DateTime.Today);
            else
            {
                switch (PromptForNewDatabase())
                {
                    case MessageBoxResult.OK: Database.CreateDatabase(); break;
                    case MessageBoxResult.Cancel: Application.Current.Shutdown(); break;
                }
            }

            InitializeWindow();
            BtnBrush = BtnSub.Background;
            ApplyKeyboardShortcuts();

            // Ensure Ctrl+Left/Right work even inside TextBox
            this.PreviewKeyDown += OnGlobalPreviewKeyDown;

            CommandBindings.Add(new CommandBinding(ExportCommand, (s, e) => BtnExport(null, null)));
            CommandBindings.Add(new CommandBinding(InsertCommand, (s, e) => BtnInsert(null, null)));
            CommandBindings.Add(new CommandBinding(TodayCommand, (s, e) => BtnGotoToday(null, null)));
            CommandBindings.Add(new CommandBinding(PrevDayCommand, (s, e) => BtnGoBack(null, null)));
            CommandBindings.Add(new CommandBinding(NextDayCommand, (s, e) => BtnGoForward(null, null)));
            CommandBindings.Add(new CommandBinding(OptionsCommand, (s, e) => MenuOptions_Click(null, null)));
            CommandBindings.Add(new CommandBinding(HelpCommand, (s, e) => BtnProjectInfo_Click(null, null)));
        }

        private void OnGlobalPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.Left)
                {
                    BtnGoBack(null, null);
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Right)
                {
                    BtnGoForward(null, null);
                    e.Handled = true;
                    return;
                }
            }

            // Unified Submit shortcut handling (works in TextBoxes, including Notes)
            var submit = SettingsManager.GetShortcut("Submit");
            if (submit != null)
            {
                bool keyMatch =
                    e.Key == submit.Key ||
                    (submit.Key == Key.Enter && e.Key == Key.Return) ||
                    (submit.Key == Key.Return && e.Key == Key.Enter);

                if (keyMatch && Keyboard.Modifiers == submit.Modifiers)
                {
                    Submit();
                    e.Handled = true; // prevents newline in Notes when Enter is the submit key
                }
            }
        }

        private void InitializeWindow()
        {
            FldStartTime.Focus();
            time_keeper.UpdateSelectedTime();
            time_keeper.SetStartTimeField();
            time_keeper.UpdateTimeTotals();
        }
        
        private MessageBoxResult PromptForNewDatabase()
        {
            string messageBoxText = "The entries database could not be found in this directory.\nWould you like to create a new one?";
            string caption = "TimeTrack - Error";

            return MessageBox.Show(messageBoxText, caption, MessageBoxButton.OKCancel, MessageBoxImage.Error, MessageBoxResult.OK);
        }

        private void LoadEntriesForDate(DateTime date)
        {
            time_keeper.Entries = Database.Retrieve(date);
            time_keeper.CurrentIdCount = Database.CurrentIdCount(date);
            time_keeper.Date = date;
        }

        private void Submit()
        {
            if (time_keeper.SubmitEntry())
            {
                time_keeper.ClearFieldsAndSetStartTime();
                ChkLunch.IsChecked = false;
                DgTimeRecords.SelectedIndex = time_keeper.Entries.Count - 1;
                DgTimeRecords.ScrollIntoView(time_keeper.Entries.Last());
                FldEndTime.Focus();
                Database.Update(time_keeper.Entries);
                ShowStatus("Entry submitted successfully");
            }
            else
            {
                // Entry failed - show error status
                ShowStatus("Failed to submit entry - check start and end times", 5000);
            }
        }

        private void BtnSubmit(object sender, RoutedEventArgs e)
        {
            Submit();
        }

        private void BtnInsert(object sender, RoutedEventArgs e)
        {
            int insertedIndex = DgTimeRecords.SelectedIndex;
            if (time_keeper.InsertBlankEntry(insertedIndex))
            {
                // If no selection, item was inserted at the end
                if (insertedIndex < 0)
                    DgTimeRecords.SelectedIndex = DgTimeRecords.Items.Count - 1;
                else
                    DgTimeRecords.SelectedIndex = insertedIndex;
                
                DgTimeRecords.Focus();
                Database.Update(time_keeper.Entries);
                ShowStatus("Blank entry inserted");
            }
        }

        private void BtnExport(object sender, RoutedEventArgs e)
        {
            if (DgTimeRecords.SelectedItem == null)
                return;

            TimeEntry selected = (TimeEntry)DgTimeRecords.SelectedItem;

            if (selected.StartTime == null || selected.EndTime == null)
            {
                MessageBox.Show("Record must have a valid start and end time", "TimeTrack - Error", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (selected.EndTime < selected.StartTime)
            {
                MessageBox.Show("Cannot export a negative time duration", "TimeTrack - Error", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            string date_time = selected.Date.ToString("yyyy-MM-dd") + " " + selected.StartTime.ToString();
            
            TimeSpan timespan_worked = (TimeSpan)(selected.EndTime - selected.StartTime);
            int hours_worked = timespan_worked.Hours;
            double minutes_worked = timespan_worked.Minutes;
            double time_worked = hours_worked + (Math.Ceiling((minutes_worked / 60) * 10) / 10);

            string text = date_time + "," + time_worked + "," + selected.Notes;
            Clipboard.SetData(DataFormats.UnicodeText, text);
            selected.Recorded = true;
            Database.Update(time_keeper.Entries);
            ShowStatus("Entry exported to clipboard");
        }

        private void BtnExportAll(object sender, RoutedEventArgs e)
        {
            if (DgTimeRecords.Items.IsEmpty)
                return;

            try
            {
                string path = "C:\\temp\\_time_export.csv";
                if (File.Exists(path))
                    File.Delete(path);

                string[] output = new string[DgTimeRecords.Items.Count];
                var all_records = DgTimeRecords.Items;

                for (int i = 0; i < all_records.Count; i++)
                {
                    TimeEntry entry = all_records[i] as TimeEntry;

                    if (entry.Recorded || entry.CaseIsEmpty())
                        continue;

                    string case_number = entry.CaseNumber.Trim();
                    string date = entry.Date.ToShortDateString().Replace("/", "-");
                    string hours = entry.Hours().ToString();
                    string minutes = entry.Minutes().ToString();
                    string time_period = entry.StartTimeAsString() + " - " + entry.EndTimeAsString();

                    if (date[1] == '-')
                        date = "0" + date;

                    output[i] = case_number + "," + date + "," + hours + "," + minutes + "," + time_period + "," + entry.Notes;
                }
                File.WriteAllLines(path, output);

                foreach (var i in DgTimeRecords.Items)
                {
                    var entry = i as TimeEntry;
                    if (entry.CaseIsEmpty())
                        continue;
                    entry.Recorded = true;
                }
            } 
            catch (Exception exc)
            {
                Error.Handle("Something went wrong while exporting all entries", exc);
            }

            Database.Update(time_keeper.Entries);
        }

        private void BtnToggleAllRecorded(object sender, RoutedEventArgs e)
        {
            bool new_status = true;

            foreach (var i in time_keeper.Entries)
            {
                if (i.Recorded == true)
                {
                    new_status = false;
                    break;
                }
            }

            foreach (var i in time_keeper.Entries)
            {
                if (new_status && string.IsNullOrEmpty(i.CaseNumber.Trim()))
                    continue;
                i.Recorded = new_status;
            }
            Database.Update(time_keeper.Entries);
        }

        private void CalLoadDate(object sender, RoutedEventArgs e)
        {
            if (time_keeper == null)
                return;

            var date = time_keeper.Date;
            time_keeper.CurrentDate = date.Date.ToShortDateString();

            if (date != DateTime.Today)
            {
                txtCurrentDate.Background = Brushes.OrangeRed;
                txtCurrentDate.Foreground = Brushes.White;
                BtnSub.Background = Brushes.OrangeRed;
                BtnSub.Foreground = Brushes.White;
                BtnToday.IsEnabled = true;
            } else {
                txtCurrentDate.Background = null;
                txtCurrentDate.Foreground = Brushes.Black;
                BtnSub.Background = BtnBrush;
                BtnSub.Foreground = Brushes.Black;
                BtnToday.IsEnabled = false;
            }

            LoadEntriesForDate(date);
            time_keeper.UpdateTimeTotals();
            time_keeper.UpdateSelectedTime();
            time_keeper.SetStartTimeField();
        }

        private void BtnGotoToday(object sender, RoutedEventArgs e)
        {
            CalDate.SelectedDate = DateTime.Today;
        }

        private void BtnGoForward(object sender, RoutedEventArgs e)
        {
            CalDate.SelectedDate += TimeSpan.FromDays(1);
        }

        private void BtnGoBack(object sender, RoutedEventArgs e)
        {
            CalDate.SelectedDate -= TimeSpan.FromDays(1);
        }

        private void ChkLunch_Checked(object sender, RoutedEventArgs e)
        {
            time_keeper.CaseNumberField = null;
            FldCaseNumber.IsEnabled = false;
            FldCaseNumber.Background = Brushes.LightGray;

            time_keeper.NotesField = "Lunch";
            FldNotes.IsEnabled = false;
            FldNotes.Background = Brushes.LightGray;

            if (time_keeper.EndTimeField == null || time_keeper.EndTimeField == "")
            {
                var startTimeSpan = TimeStringConverter.StringToTimeSpan(time_keeper.StartTimeField);
                if (startTimeSpan != null)
                {
                    var EndLunch = DateTime.Today + startTimeSpan.Value;
                    EndLunch = EndLunch.AddHours(1);
                    time_keeper.EndTimeField = EndLunch.ToShortTimeString();
                }
            }
        }

        private void ChkLunch_Unchecked(object sender, RoutedEventArgs e)
        {
            time_keeper.CaseNumberField = null;
            FldCaseNumber.IsEnabled = true;
            FldCaseNumber.Background = Brushes.White;

            time_keeper.EndTimeField = null;
            time_keeper.NotesField = null;
            FldNotes.IsEnabled = true;
            FldNotes.Background = Brushes.White;
        }

        private void DgTimeRecords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgTimeRecords.SelectedItem != null)
                time_keeper.UpdateSelectedTime();
        }

        private void BtnProjectInfo_Click(object sender, RoutedEventArgs e)
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string messageBoxText = $"TimeTrack\nVersion: {version}\n\nA simple time tracking application for daily work entries.";
            string caption = "Project Information";
            MessageBox.Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MenuOptions_Click(object sender, RoutedEventArgs e)
        {
            // Open your OptionsWindow
            var optionsWindow = new OptionsWindow();
            optionsWindow.Owner = this;
            optionsWindow.ShowDialog();
        }

        private void ApplyKeyboardShortcuts()
        {
            this.InputBindings.Clear();
            var shortcuts = SettingsManager.GetAllShortcuts();

            if (shortcuts.ContainsKey("Export"))
            {
                var s = shortcuts["Export"];
                this.InputBindings.Add(new KeyBinding(new RelayCommand(p => BtnExport(null, null)), s.Key, s.Modifiers));
            }
            if (shortcuts.ContainsKey("Insert"))
            {
                var s = shortcuts["Insert"];
                this.InputBindings.Add(new KeyBinding(new RelayCommand(p => BtnInsert(null, null)), s.Key, s.Modifiers));
            }
            if (shortcuts.ContainsKey("Today"))
            {
                var s = shortcuts["Today"];
                this.InputBindings.Add(new KeyBinding(new RelayCommand(p => BtnGotoToday(null, null)), s.Key, s.Modifiers));
            }
            if (shortcuts.ContainsKey("PrevDay"))
            {
                var s = shortcuts["PrevDay"];
                this.InputBindings.Add(new KeyBinding(new RelayCommand(p => BtnGoBack(null, null)), s.Key, s.Modifiers));
            }
            if (shortcuts.ContainsKey("NextDay"))
            {
                var s = shortcuts["NextDay"];
                this.InputBindings.Add(new KeyBinding(new RelayCommand(p => BtnGoForward(null, null)), s.Key, s.Modifiers));
            }
            if (shortcuts.ContainsKey("Options"))
            {
                var s = shortcuts["Options"];
                this.InputBindings.Add(new KeyBinding(new RelayCommand(p => MenuOptions_Click(null, null)), s.Key, s.Modifiers));
            }
            // Submit (respect configured key – Enter/Return supported)
            if (shortcuts.ContainsKey("Submit"))
            {
                var s = shortcuts["Submit"];
                this.InputBindings.Add(new KeyBinding(new RelayCommand(p => Submit()), s.Key, s.Modifiers));
                if (s.Key == Key.Enter || s.Key == Key.Return)
                {
                    // Add the other Enter variant too (main vs numpad)
                    var altKey = s.Key == Key.Enter ? Key.Return : Key.Enter;
                    this.InputBindings.Add(new KeyBinding(new RelayCommand(p => Submit()), altKey, s.Modifiers));
                }
            }

            // Back-compat: Toggle All vs Mark All
            if (shortcuts.ContainsKey("ToggleAll") || shortcuts.ContainsKey("MarkAll"))
            {
                var s = shortcuts.ContainsKey("ToggleAll") ? shortcuts["ToggleAll"] : shortcuts["MarkAll"];
                this.InputBindings.Add(new KeyBinding(new RelayCommand(p => BtnToggleAllRecorded(null, null)), s.Key, s.Modifiers));
            }
        }

        private void ShowStatus(string message, int durationMs = 3000)
        {
            if (StatusText == null)
            {
                // StatusText not found - this shouldn't happen
                System.Diagnostics.Debug.WriteLine("ERROR: StatusText is null!");
                return;
            }
            
            StatusText.Text = message;
            
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(durationMs);
            timer.Tick += (s, e) =>
            {
                StatusText.Text = "Ready";
                timer.Stop();
            };
            timer.Start();
        }

        private void InitializeTimePickerComboBoxes()
        {
            // Hours (1-12)
            for (int i = 1; i <= 12; i++)
            {
                CmbStartHour.Items.Add(i.ToString("00"));
                CmbEndHour.Items.Add(i.ToString("00"));
            }
            
            // Minutes (00-59) - All minutes available
            for (int i = 0; i < 60; i++)
            {
                CmbStartMinute.Items.Add(i.ToString("00"));
                CmbEndMinute.Items.Add(i.ToString("00"));
            }
            
            // AM/PM
            CmbStartPeriod.Items.Add("AM");
            CmbStartPeriod.Items.Add("PM");
            
            CmbEndPeriod.Items.Add("AM");
            CmbEndPeriod.Items.Add("PM");
        }

        private void BtnStartTimePicker_Click(object sender, RoutedEventArgs e)
        {
            // Parse current start time if it exists
            var currentTime = TimeStringConverter.StringToTimeSpan(time_keeper.StartTimeField);
            if (currentTime.HasValue)
            {
                var dt = DateTime.Today + currentTime.Value;
                int hour = dt.Hour > 12 ? dt.Hour - 12 : (dt.Hour == 0 ? 12 : dt.Hour);
                CmbStartHour.SelectedItem = hour.ToString("00");
                CmbStartMinute.SelectedItem = dt.Minute.ToString("00");
                CmbStartPeriod.SelectedItem = dt.Hour >= 12 ? "PM" : "AM";
            }
    
            PopupStartTime.IsOpen = true;
        }

        private void BtnEndTimePicker_Click(object sender, RoutedEventArgs e)
        {
            // Parse current end time if it exists
            var currentTime = TimeStringConverter.StringToTimeSpan(time_keeper.EndTimeField);
            if (currentTime.HasValue)
            {
                var dt = DateTime.Today + currentTime.Value;
                int hour = dt.Hour > 12 ? dt.Hour - 12 : (dt.Hour == 0 ? 12 : dt.Hour);
                CmbEndHour.SelectedItem = hour.ToString("00");
                CmbEndMinute.SelectedItem = dt.Minute.ToString("00");
                CmbEndPeriod.SelectedItem = dt.Hour >= 12 ? "PM" : "AM";
            }
    
            PopupEndTime.IsOpen = true;
        }

        private void BtnSetStartTime_Click(object sender, RoutedEventArgs e)
        {
            if (CmbStartHour.SelectedItem != null && 
                CmbStartMinute.SelectedItem != null && 
                CmbStartPeriod.SelectedItem != null)
            {
                string timeString = $"{CmbStartHour.SelectedItem}:{CmbStartMinute.SelectedItem} {CmbStartPeriod.SelectedItem}";
                time_keeper.StartTimeField = timeString;
                PopupStartTime.IsOpen = false;
            }
        }

        private void BtnSetEndTime_Click(object sender, RoutedEventArgs e)
        {
            if (CmbEndHour.SelectedItem != null && 
                CmbEndMinute.SelectedItem != null && 
                CmbEndPeriod.SelectedItem != null)
            {
                string timeString = $"{CmbEndHour.SelectedItem}:{CmbEndMinute.SelectedItem} {CmbEndPeriod.SelectedItem}";
                time_keeper.EndTimeField = timeString;
                PopupEndTime.IsOpen = false;
            }
        }

        private void OnNotesKeyDown(object sender, KeyEventArgs e)
        {
            // Allow Enter to create new lines in Notes field
            // Submit only on Ctrl+Enter
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnSub.Focus();
                Submit();
                e.Handled = true;
            }
            // Allow normal Enter for new lines (don't handle the event)
        }
    }

    public class TimeKeeper : INotifyPropertyChanged
    {
        public TimeKeeper()
        {
            time_records = new ObservableCollection<TimeEntry>();
            date = DateTime.Today.Date;
            current_date = date.Date.ToShortDateString();
            current_id_count = 0;
        }

        // Accessor functions

        public DateTime Date
        {
            get => date;
            set => date = value;
        }
        public int CurrentIdCount
        {
            set => current_id_count = value;
        }
        public ObservableCollection<TimeEntry> Entries
        {
            get => time_records;
            set { time_records = value; OnPropertyChanged(); AddChangedHandlerToAllEntries(); }
        }
        public string CurrentDate
        {
            get => current_date;
            set { current_date = value; OnPropertyChanged(); }
        }
        public string StartTimeField
        {
            get => start_time;
            set { start_time = value; OnPropertyChanged(); }
        }
        public string EndTimeField
        {
            get => end_time;
            set { end_time = value; OnPropertyChanged(); }
        }
        public TimeSpan? StartTimeFieldAsTime() => TimeStringConverter.StringToTimeSpan(start_time);
        public TimeSpan? EndTimeFieldAsTime() => TimeStringConverter.StringToTimeSpan(end_time);
        public string CaseNumberField
        {
            get => case_no;
            set { case_no = value; OnPropertyChanged(); }
        }
        public string NotesField
        {
            get => notes;
            set { notes = value; OnPropertyChanged(); }
        }
        public double HoursTotal
        {
            get => hours_total;
            set { hours_total = value; OnPropertyChanged(); }
        }
        public double GapsTotal
        {
            get => gaps_total;
            set { gaps_total = value; OnPropertyChanged(); }
        }
        public string SelectedHours
        {
            get => selected_hours;
            set { selected_hours = value; OnPropertyChanged(); }
        }
        public string SelectedMins
        {
            get => selected_mins;
            set { selected_mins = value; OnPropertyChanged(); }
        }
        public TimeEntry SelectedItem
        {
            get => selected_item;
            set
            {
                if (selected_item == value) return;
                selected_item = value;
                OnPropertyChanged();
                UpdateSelectedTime(); // ensure SelectedHours/SelectedMins update on row selection
            }
        }

        // Functions

        public void AddEntry(DateTime date, int id, TimeSpan start_time, TimeSpan end_time, string case_number = "", string notes = "")
        {
            var entry = new TimeEntry(date, id, start_time, end_time, case_number, notes);
            entry.TimeEntryChanged += OnTimeEntryChanged;
            time_records.Add(entry);
            UpdateTimeTotals();
        }

        public bool InsertBlankEntry(int index)
        {
            if (time_records.Count == 0)
                return false;

            // If no item is selected, insert at the end
            if (index < 0 || index > time_records.Count)
                index = time_records.Count;

            time_records.Insert(index, new TimeEntry(date, ++current_id_count));
            UpdateTimeTotals();
            return true;
        }

        public bool SubmitEntry()
        {
            TimeSpan? start_time = StartTimeFieldAsTime();
            TimeSpan? end_time = EndTimeFieldAsTime();

            if (start_time == null || end_time == null)
                return false;

            AddEntry(date, ++current_id_count, (TimeSpan)start_time, (TimeSpan)end_time, case_no, notes);
            return true;
        }

        public void RemoveCurrentlySelectedEntry()
        {
            var item = SelectedItem;
            if (item == null)
                return;

            // Detach event handler to avoid leaks
            item.TimeEntryChanged -= OnTimeEntryChanged;

            // Remove from DB then from collection
            Database.Delete(item.Date, item.ID);
            Entries.Remove(item);

            // Update selection first, then recompute totals and persist
            SelectLastEntry();
            UpdateTimeTotals();
            SetStartTimeField();
            Database.Update(Entries);
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

            EndTimeField = "";
            CaseNumberField = "";
            NotesField = "";
        }

        public void UpdateTimeTotals()
        {
            TimeSpan time = new TimeSpan();
            TimeSpan gap = new TimeSpan();

            foreach (var i in Entries)
            {
                if (i.StartTime != null && i.EndTime != null)
                {
                    if (i.CaseNumber != null && i.CaseNumber != "")
                        time += (TimeSpan)(i.EndTime - i.StartTime);
                    else
                    {
                        if (i.Notes != null && i.Notes.ToLower().Trim() == "lunch")
                            continue;
                        else
                            gap += (TimeSpan)(i.EndTime - i.StartTime);
                    }
                }
            }

            HoursTotal = Math.Round(time.TotalHours, 2, MidpointRounding.AwayFromZero);
            GapsTotal = gap.TotalMinutes;
        }

        public void UpdateSelectedTime()
        {
            bool blank_value = false;

            if (SelectedItem != null)
            {
                var time_span = (SelectedItem.EndTime - SelectedItem.StartTime);
                if (time_span != null)
                {
                    SelectedHours = ((TimeSpan)time_span).Hours.ToString();
                    SelectedMins = ((TimeSpan)time_span).Minutes.ToString();
                }
                else
                    blank_value = true;
            }
            else
                blank_value = true;

            if (blank_value)
            {
                SelectedHours = "-";
                SelectedMins = "-";
            }
        }

        public void SetStartTimeField()
        {
            if (time_records.Count > 0)
                StartTimeField = time_records.Last<TimeEntry>().EndTimeAsString();
            else
                StartTimeField = null;
        }

        public void AddChangedHandlerToAllEntries()
        {
            foreach (var entry in time_records)
            {
                ((TimeEntry)entry).TimeEntryChanged += OnTimeEntryChanged;
            }
        }

        public void OnTimeEntryChanged(bool time_changed)
        {
            if (time_changed)
            {
                UpdateTimeTotals();
                UpdateSelectedTime();
                SetStartTimeField();
            }
            Database.Update(time_records);
        }

        // Inheritance items

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // Event commands

        private ICommand remove_command;
        public ICommand RemoveCommand
        {
            get
            {
                if (remove_command == null)
                    remove_command = new RelayCommand(p => RemoveCurrentlySelectedEntry());
                return remove_command;
            }
        }

        private ICommand submit_command;
        public ICommand SubmitCommand
        {
            get => submit_command;
            set => submit_command = value;
        }

        // Private vars

        private DateTime date;
        private String current_date;
        private int current_id_count;
        private ObservableCollection<TimeEntry> time_records;
        private string start_time;
        private string end_time;
        private string case_no;
        private string notes;

        private double hours_total;
        private double gaps_total;
        private string selected_hours;
        private string selected_mins;
        private TimeEntry selected_item;
    }

    class Database
    {
        // Public variables
        public const string date_format = "yyyy-MM-dd";

        //Public functions
        public static bool Exists()
        {
            return File.Exists(databasePath);
        }
        public static void CreateDatabase()
        {
            Connect();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
            cmd.Prepare();
            var result = cmd.ExecuteScalar();

            if ((string)result == "time_entries")
                return;

            cmd.CommandText = @"CREATE TABLE time_entries(date TEXT, id INTEGER, start_time TEXT, end_time TEXT, case_number TEXT, notes TEXT, recorded INTEGER, CONSTRAINT pk PRIMARY KEY(date, id));";
            cmd.ExecuteNonQuery();
            Close();
        }
        public static int CurrentIdCount(DateTime date)
        {
            Connect();
            cmd.CommandText = "SELECT MAX(id) FROM time_entries WHERE date = @date;";
            cmd.Parameters.AddWithValue("@date", DateToString(date));
            
            int return_result = 0;
            try
            {
                cmd.Prepare();
                var query = cmd.ExecuteReader();

                while (query.Read() && !query.IsDBNull(0))
                    return_result = query.GetInt32(0);
                query.Close();
            }
            catch (Exception e)
            {
                Close();
                Error.Handle("Could not get current entry index.", e);
                throw e;
            }
            
            Close();
            return return_result;
        }
        public static ObservableCollection<TimeEntry> Retrieve(DateTime date)
        {
            Connect();
            var return_val = new ObservableCollection<TimeEntry>();
            
            cmd.CommandText = "SELECT * FROM time_entries WHERE date = @date ORDER BY start_time ASC, end_time ASC, id ASC";
            cmd.Parameters.AddWithValue("@date", DateToString(date));

            try
            {
                cmd.Prepare();
                var query = cmd.ExecuteReader();

                while (query.Read())
                {
                    var out_date = StringToDate(query.GetString(0));
                    var id = query.GetInt32(1);
                    int recorded = query.GetInt32(6);

                    TimeSpan? start_time = null;
                    TimeSpan? end_time = null;
                    string case_no = "";
                    string notes = "";
                        

                    if (!query.IsDBNull(2))
                        start_time = StringToTimeSpan(query.GetString(2));
                    if (!query.IsDBNull(3))
                        end_time = StringToTimeSpan(query.GetString(3));
                    if (!query.IsDBNull(4))
                        case_no = query.GetString(4);
                    if (!query.IsDBNull(5))
                        notes = query.GetString(5);

                    return_val.Add(new TimeEntry(out_date, id, start_time, end_time, case_no, notes, Convert.ToBoolean(recorded)));
                }
            } 
            catch (Exception e)
            {
                Close();
                Error.Handle("Something went wrong while retrieving today's entries.", e);
                throw e;
            }
            Close();
            return return_val;
        }
        public static void Update(ObservableCollection<TimeEntry> entries)
        {
            if (entries.Count < 1)
                return;

            Connect();
            for (int i = 0; i < entries.Count; i++)
            {
                cmd.CommandText = "INSERT OR REPLACE INTO time_entries(date, id, start_time, end_time, case_number, notes, recorded) " +
                    "VALUES(@date, @id, @start_time, @end_time, @case_number, @notes, @recorded)";
                cmd.Parameters.AddWithValue("@date", DateToString(entries[i].Date));
                cmd.Parameters.AddWithValue("@id", entries[i].ID);
                cmd.Parameters.AddWithValue("@start_time", entries[i].StartTime);
                cmd.Parameters.AddWithValue("@end_time", entries[i].EndTime);
                cmd.Parameters.AddWithValue("@case_number", entries[i].CaseNumber);
                cmd.Parameters.AddWithValue("@notes", entries[i].Notes);
                cmd.Parameters.AddWithValue("@recorded", entries[i].Recorded);

                try
                {
                    cmd.Prepare();
                    cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    Error.Handle("Something went wrong while updating the entries database.\nThe saved records may not be consistent with what is displayed.", e);
                }
            }
            Close();
        }
        public static void Delete(DateTime date, int id)
        {
            Connect();
            cmd.CommandText = "DELETE FROM time_entries WHERE date = @date AND id = @id";
            cmd.Parameters.AddWithValue("@date", DateToString(date));
            cmd.Parameters.AddWithValue("@id", id);
            try
            {
                cmd.Prepare();
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Error.Handle("Could not delete the record from the database.\nThe saved records may not consistent with what is displayed.", e);
            }
            Close();
        }

        // Private functions
        private static void Connect()
        {
            try
            {
                connection = new SQLiteConnection(databaseURI);
                connection.Open();
                cmd = connection.CreateCommand();
            }
            catch (Exception e)
            {
                Error.Handle("Could not open a connection to the database.", e);
                throw e;
            }
        }
        private static void Close()
        {
            connection.Close();
            connection.Dispose();
        }
        private static string DateToString(DateTime date)
        {
            return date.ToString(date_format);
        }
        private static DateTime StringToDate(string str)
        {
            return DateTime.ParseExact(str, date_format, DateTimeFormatInfo.InvariantInfo);
        }
        private static TimeSpan StringToTimeSpan(string str)
        {
            return TimeSpan.ParseExact(str, "c", CultureInfo.InvariantCulture, TimeSpanStyles.None);
        }

        // Private variables
        private const string databasePath = "timetrack.db";
        private const string databaseURI = @"URI=file:" + databasePath;
        private static SQLiteConnection connection;
        private static SQLiteCommand cmd;
    }

    public static class Error
    {
        public static void Handle(string error_text, Exception e, [CallerLineNumber] int line_number = 0, [CallerMemberName] string caller = null)
        {
            string caption = "TimeTrack - Error";
            string messageBoxText = error_text + "\n" + "\nFunction name: " + caller + "\nLine number: " + line_number + "\n\nException details:\n" + e.Message;

            MessageBox.Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

            string log_text = DateTime.Now.ToLocalTime() + "," + caller + "," + e.Message.Replace("\r", "").Replace("\n"," | ") + "," + error_text.Replace("\n", " | ");
            File.AppendAllText("time_track_log.txt", log_text);
        }
    }

    public static class TimeStringConverter
    {
        static DateTime work_hours_start = DateTime.ParseExact("07:00AM", "hh:mmtt", CultureInfo.InvariantCulture);
        static DateTime work_hours_end = DateTime.ParseExact("07:00PM", "hh:mmtt", CultureInfo.InvariantCulture);

        public static bool IsValidTimeFormat(string value)
        {
            if (value == null)
                return false;

            /* Regex Explantion:
             * ^                : starting at the beginning of the string
             * \d{1,2}          : between 1, and 2 digit characters
             * [;:.]?           : ";", ":", or "." - optional
             * ()               : encapsulated logic. The same as you are used to.
             * (\d{2})?         : exactly 2 digit characters - optional
             * (\s?)+           : any number of whitespaces - optional
             * (...)?           : everything contained in the brackets is optional
             * (?i: ...)        : contained commands will be case-insensitive
             * ...[AP]M)?       : either A, or P characters, followed by M. All optional
             * $                : the end of the string
             */
            string valid_time_format = @"^\d{1,2}[;:.]?(\d{2})?((\s?)+(?i:[AP]M)?)?$";

            /* Regex Explantion:
             * ^                : Start of the string
             * \d{1,2}          : 2 digits
             * [;:.]?           : either ";", ":" or "." - optional
             * (\d{2})?         : 2 digits - optional
             * (?i: ...)?       : contained commands will be case-insensitive. All optional
             * (\s?)+           : any number of whitespaces - optional
             * (?!AM)           : fail if "AM" is present
             * (PM)?            : PM - optional
             * $                : end of the string
             */
            string valid_24hour_format = @"^\d{2}[;:.]?(\d{2})?(?i:(\s?)+(?!AM)(PM)?)?$";

            if (Regex.IsMatch(value, valid_time_format))
            {
                if (Is24HourFormat(value))
                    return Regex.IsMatch(value, valid_24hour_format);

                return true;
            }

            return false;
        }
        public static bool Is24HourFormat(string value)
        {
            /* Regex: 
             * at the start of the string 
             * 2 digits
             * a non-digit character, or the end of the string (not captured)
             * OR
             * 2 more digits
             */
            if (value.Length >= 2 && Regex.IsMatch(value, @"^\d{2}((?:\D|$)|\d{2})"))
            {
                int hour = Convert.ToInt32(value.Substring(0, 2));
                return hour >= 13 && hour <= 23;
            }
            else
                return false;
        }
        public static bool TimePeriodPresent(string value)
        {
            return Regex.IsMatch(value, @"(?i)[AP]M$");
        }
        private static bool ContainsMinutes(string value)
        {
            // Regex: looks for 1 or 2 digits, a colon, then 2 digits .
            return Regex.IsMatch(value, @"^\d{1,2}:\d{2}");
        }
        private static string CleanTimeString(string value, bool remove_period = false)
        {
            value = value.Trim();
            value = value.Replace(";", ":");
            value = value.Replace(".", ":");
            value = value.Replace(" ", "");

            bool period_present = TimePeriodPresent(value);

            if (period_present && remove_period)
                value = value.Remove(value.Length - 2, 2);

            if (!value.Contains(":"))
            {
                // Regex: if there are only 3, or 1 digits
                // followed by either a non digit, or the end of the string
                if (Regex.IsMatch(value, @"^(\d{3}|\d)(?:\D|$)"))
                    value = value.Insert(1, ":");
                else
                    value = value.Insert(2, ":");
            }

            // if there is only 1 hour digit, add a 0 to the front.
            if (Regex.IsMatch(value, @"^\d:"))
                value = "0" + value;

            return value;
        }
        private static DateTime ClampToWorkHours(DateTime value)
        {
            if (value.TimeOfDay > work_hours_start.TimeOfDay && value.TimeOfDay < work_hours_end.TimeOfDay)
                return value;

            switch (value.ToString("tt"))
            {
                case "AM":
                    return value.AddHours(12);
                case "PM":
                    return value.AddHours(-12);
                default:
                    return value;
            }
        }
        public static TimeSpan? StringToTimeSpan(string value)
        {
            if (!IsValidTimeFormat(value))
                return null;

            DateTime return_val;
            string time_format;
            bool time_period_present = false;
            bool format_24_hour = Is24HourFormat(value);

            value = CleanTimeString(value, format_24_hour);

            if (format_24_hour)
                time_format = "HH:";
            else
            {
                int hour_digit_count = value.Length;

                if (time_period_present = TimePeriodPresent(value))
                    hour_digit_count -= 2;

                if (hour_digit_count > 3)
                    time_format = "hh:";
                else
                    time_format = "hh:";
            }

            if (ContainsMinutes(value))
                time_format += "mm";

            if (!format_24_hour && TimePeriodPresent(value))
                time_format += "tt";

            if (!DateTime.TryParseExact(value, time_format, CultureInfo.InvariantCulture, DateTimeStyles.None, out return_val))
                return null;
            else
            {
                if (!time_period_present && !format_24_hour)
                    return_val = ClampToWorkHours(return_val);

                return return_val.TimeOfDay;
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private Action<object> execute;
        private Func<object, bool> canExecute;

        public RelayCommand(Action<object> execute)
        {
            this.execute = execute;
            this.canExecute = null;
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return this.canExecute == null || this.canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            this.execute(parameter);
        }
    }

    public class HotkeyDelegateCommand : ICommand
    {
        // Specify the keys and mouse actions that invoke the command. 
        public Key HotKey { get; set; }

        Action<object> _executeDelegate;

        public HotkeyDelegateCommand(Action<object> executeDelegate)
        {
            _executeDelegate = executeDelegate;
        }

        public void Execute(object parameter)
        {
            _executeDelegate(parameter);
        }

        public bool CanExecute(object parameter) { return true; }
#pragma warning disable CS0067 // boilerplate code
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067 // boilerplate code
    }
}