using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using TimeTrack.Data;

namespace TimeTrack
{
    public partial class MainWindow : Window
    {
        private TimeKeeper? time_keeper;
        private Brush? BtnBrush;
        private readonly System.Windows.Threading.DispatcherTimer _statusTimer = new System.Windows.Threading.DispatcherTimer();

        // Existing routed commands (remove hardcoded InputGestureCollection)
        public static readonly RoutedUICommand ExportCommand =
            new RoutedUICommand("Export Selected", "Export", typeof(MainWindow));

        public static readonly RoutedUICommand InsertCommand =
            new RoutedUICommand("Insert Record", "Insert", typeof(MainWindow));

        public static readonly RoutedUICommand TodayCommand =
            new RoutedUICommand("Today", "Today", typeof(MainWindow));

        public static readonly RoutedUICommand PrevDayCommand =
            new RoutedUICommand("Previous Day", "PrevDay", typeof(MainWindow));

        public static readonly RoutedUICommand NextDayCommand =
            new RoutedUICommand("Next Day", "NextDay", typeof(MainWindow));

        public static readonly RoutedUICommand OptionsCommand =
            new RoutedUICommand("Options", "Options", typeof(MainWindow));

        public static readonly RoutedUICommand HelpCommand =
            new RoutedUICommand("About", "Help", typeof(MainWindow));

        // New routed commands used by customizable shortcuts
        public static readonly RoutedUICommand SubmitCommand =
            new RoutedUICommand("Submit Entry", "Submit", typeof(MainWindow));
        public static readonly RoutedUICommand ToggleAllCommand =
            new RoutedUICommand("Toggle All Recorded", "ToggleAll", typeof(MainWindow));

        // Constructor — forward actual event args instead of passing null literals
        public MainWindow()
        {
            InitializeComponent();
            time_keeper = DataContext as TimeKeeper;

            InitializeTimePickerComboBoxes();

            // DB is initialized at application startup (App.OnStartup)
            LoadEntriesForDate(DateTime.Today);

            InitializeWindow();
            
            // Null check before accessing BtnSub
            if (BtnSub != null)
                BtnBrush = BtnSub.Background;
            
            ApplyKeyboardShortcuts();
            UpdateMenuGestureTexts();

            // Ensure global shortcuts work even inside TextBox
            this.PreviewKeyDown += OnGlobalPreviewKeyDown;

            // Bind handlers for all routed commands
            CommandBindings.Add(new CommandBinding(ExportCommand, (s, e) => BtnExport(s, e)));
            CommandBindings.Add(new CommandBinding(InsertCommand, (s, e) => BtnInsert(s, e)));
            CommandBindings.Add(new CommandBinding(TodayCommand, (s, e) => BtnGotoToday(s, e)));
            CommandBindings.Add(new CommandBinding(PrevDayCommand, (s, e) => BtnGoBack(s, e)));
            CommandBindings.Add(new CommandBinding(NextDayCommand, (s, e) => BtnGoForward(s, e)));
            CommandBindings.Add(new CommandBinding(OptionsCommand, (s, e) => MenuOptions_Click(s, e)));
            CommandBindings.Add(new CommandBinding(HelpCommand, (s, e) => BtnProjectInfo_Click(s, e)));
            // Gate Submit via CanExecute
            CommandBindings.Add(new CommandBinding(SubmitCommand, (s, e) => BtnSubmit(s, e), (s, e) => e.CanExecute = CanSubmit()));
            CommandBindings.Add(new CommandBinding(ToggleAllCommand, (s, e) => BtnToggleAllRecorded(s, e)));

            // Refresh CanExecute when input fields change
            if (time_keeper != null)
            {
                WeakEventManager<TimeKeeper, PropertyChangedEventArgs>.AddHandler(time_keeper, nameof(time_keeper.PropertyChanged), TimeKeeper_PropertyChanged);
            }

            // Reuse one status timer
            _statusTimer.Tick += (s, e) =>
            {
                if (StatusText != null)
                    StatusText.Text = "Ready";
                _statusTimer.Stop();
            };
        }

        private void UpdateMenuGestureTexts()
        {
            try
            {
                void setText(MenuItem? mi, string action)
                {
                    var sc = SettingsManager.GetShortcut(action);
                    if (mi != null)
                        mi.InputGestureText = sc?.DisplayText ?? string.Empty;
                }

                setText(ExportMenuItem, "Export");
                setText(SubmitMenuItem, "Submit");
                setText(InsertMenuItem, "Insert");
                setText(DeleteMenuItem, "Delete");
                setText(ToggleAllMenuItem, "ToggleAll");
                setText(TodayMenuItem, "Today");
                setText(PrevDayMenuItem, "PrevDay");
                setText(NextDayMenuItem, "NextDay");
                setText(OptionsMenuItem, "Options");
                setText(AboutMenuItem, "About");
            }
            catch { /* ignore */ }
        }

        private void TimeKeeper_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(TimeKeeper.StartTimeField)
                or nameof(TimeKeeper.EndTimeField)
                or nameof(TimeKeeper.CaseNumberField)
                or nameof(TimeKeeper.NotesField))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Require start, end, and either Lunch or Case Number; also require Notes
        private bool CanSubmit()
        {
            if (time_keeper == null) return false;
            var hasStart = time_keeper.StartTimeFieldAsTime().HasValue;
            var hasEnd = time_keeper.EndTimeFieldAsTime().HasValue;
            bool isLunch = ChkLunch != null && ChkLunch.IsChecked == true;
            bool hasCase = !string.IsNullOrWhiteSpace(time_keeper.CaseNumberField);
            bool hasNotes = !string.IsNullOrWhiteSpace(time_keeper.NotesField);
            if (!hasStart || !hasEnd) return false;
            if (!isLunch && !hasCase) return false;
            if (!hasNotes) return false;
            return true;
        }

        private static bool MatchesShortcut(KeyEventArgs e, KeyboardShortcut? shortcut)
        {
            if (shortcut is null) return false;
            return e.Key == shortcut.Key && Keyboard.Modifiers == shortcut.Modifiers;
        }

        private void OnGlobalPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Block Enter/Return globally until Notes has data
            if ((e.Key == Key.Enter || e.Key == Key.Return) && (time_keeper == null || string.IsNullOrWhiteSpace(time_keeper.NotesField)))
            {
                e.Handled = true;
                return;
            }

            // Dynamic Prev/Next day from settings
            var prev = SettingsManager.GetShortcut("PrevDay");
            if (MatchesShortcut(e, prev))
            {
                BtnGoBack(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            var next = SettingsManager.GetShortcut("NextDay");
            if (MatchesShortcut(e, next))
            {
                BtnGoForward(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // Options shortcut handled here to work everywhere
            var options = SettingsManager.GetShortcut("Options");
            if (MatchesShortcut(e, options))
            {
                MenuOptions_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
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
                    if (CanSubmit())
                    {
                        Submit();
                        e.Handled = true; // prevents newline when it’s a valid submit
                    }
                    // else: let the control handle Enter/Return normally
                }
            }
        }

        private void InitializeWindow()
        {
            if (FldStartTime != null)
                FldStartTime.Focus();
            
            if (time_keeper != null)
            {
                time_keeper.UpdateSelectedTime();
                time_keeper.SetStartTimeField();
                time_keeper.UpdateTimeTotals();
            }
        }
        
        private void LoadEntriesForDate(DateTime date)
        {
            if (time_keeper == null)
                return;

            time_keeper.Entries = Database.Retrieve(date);
            time_keeper.CurrentIdCount = Database.CurrentIdCount(date);
            time_keeper.Date = date;
        }

        private void Submit()
        {
            if (time_keeper == null)
                return;

            if (!CanSubmit())
            {
                ShowStatus("Please enter start, end, case number (unless Lunch), and notes", 5000);
                if ((ChkLunch == null || ChkLunch.IsChecked != true) && string.IsNullOrWhiteSpace(time_keeper.CaseNumberField) && FldCaseNumber != null)
                {
                    FldCaseNumber.Focus();
                }
                else if (string.IsNullOrWhiteSpace(time_keeper.StartTimeField) && FldStartTime != null)
                {
                    FldStartTime.Focus();
                }
                else if (string.IsNullOrWhiteSpace(time_keeper.EndTimeField) && FldEndTime != null)
                {
                    FldEndTime.Focus();
                }
                else if (string.IsNullOrWhiteSpace(time_keeper.NotesField) && FldNotes != null)
                {
                    FldNotes.Focus();
                }
                return;
            }

            if (time_keeper.SubmitEntry())
            {
                time_keeper.ClearFieldsAndSetStartTime();
                
                if (ChkLunch != null)
                    ChkLunch.IsChecked = false;
                
                if (DgTimeRecords != null)
                {
                    DgTimeRecords.SelectedIndex = time_keeper.Entries.Count - 1;
                    DgTimeRecords.ScrollIntoView(time_keeper.Entries.Last());
                }
                
                if (FldEndTime != null)
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
            if (time_keeper == null || DgTimeRecords == null)
                return;

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
            if (DgTimeRecords == null || DgTimeRecords.SelectedItem == null || time_keeper == null)
                return;

            TimeEntry? selected = DgTimeRecords.SelectedItem as TimeEntry;
            if (selected == null)
                return;

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

            string text = date_time + "," + time_worked + "," + (selected.Notes ?? string.Empty);
            Clipboard.SetData(DataFormats.UnicodeText, text);
            selected.Recorded = true;
            Database.Update(time_keeper.Entries);
            ShowStatus("Entry exported to clipboard");
        }

        private void BtnToggleAllRecorded(object sender, RoutedEventArgs e)
        {
            if (time_keeper == null)
                return;

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
                if (new_status && string.IsNullOrEmpty(i.CaseNumber?.Trim()))
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

            if (txtCurrentDate != null)
            {
                if (date != DateTime.Today)
                {
                    txtCurrentDate.Background = Brushes.OrangeRed;
                    txtCurrentDate.Foreground = Brushes.White;
                    if (BtnSub != null)
                        BtnSub.Background = Brushes.OrangeRed;
                    if (BtnSub != null)
                        BtnSub.Foreground = Brushes.White;
                    if (BtnToday != null)
                        BtnToday.IsEnabled = true;
                }
                else
                {
                    txtCurrentDate.Background = null;
                    txtCurrentDate.Foreground = Brushes.Black;
                    if (BtnBrush != null && BtnSub != null)
                        BtnSub.Background = BtnBrush;
                    if (BtnSub != null)
                        BtnSub.Foreground = Brushes.Black;
                    if (BtnToday != null)
                        BtnToday.IsEnabled = false;
                }
            }

            LoadEntriesForDate(date);
            time_keeper.UpdateTimeTotals();
            time_keeper.UpdateSelectedTime();
            time_keeper.SetStartTimeField();
        }

        private void BtnGotoToday(object sender, RoutedEventArgs e)
        {
            if (CalDate != null)
                CalDate.SelectedDate = DateTime.Today;
        }

        private void BtnGoForward(object sender, RoutedEventArgs e)
        {
            if (CalDate != null && CalDate.SelectedDate != null)
                CalDate.SelectedDate = CalDate.SelectedDate.Value.AddDays(1);
        }

        private void BtnGoBack(object sender, RoutedEventArgs e)
        {
            if (CalDate != null && CalDate.SelectedDate != null)
                CalDate.SelectedDate = CalDate.SelectedDate.Value.AddDays(-1);
        }

        // ChkLunch_Checked / Unchecked — clear fields with empty string instead of null
        private void ChkLunch_Checked(object sender, RoutedEventArgs e)
        {
            if (time_keeper == null)
                return;

            time_keeper.CaseNumberField = string.Empty;
            
            if (FldCaseNumber != null)
            {
                FldCaseNumber.IsEnabled = false;
                FldCaseNumber.Background = Brushes.LightGray;
            }

            time_keeper.NotesField = "Lunch";
            
            if (FldNotes != null)
            {
                FldNotes.IsEnabled = false;
                FldNotes.Background = Brushes.LightGray;
            }

            if (string.IsNullOrEmpty(time_keeper.EndTimeField))
            {
                var startTimeSpan = TimeStringConverter.StringToTimeSpan(time_keeper.StartTimeField);
                if (startTimeSpan != null)
                {
                    var EndLunch = DateTime.Today + startTimeSpan.Value;
                    EndLunch = EndLunch.AddHours(1);
                    time_keeper.EndTimeField = EndLunch.ToShortTimeString();
                }
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private void ChkLunch_Unchecked(object sender, RoutedEventArgs e)
        {
            if (time_keeper == null)
                return;

            time_keeper.CaseNumberField = string.Empty;
            if (FldCaseNumber != null)
            {
                FldCaseNumber.IsEnabled = true;
                FldCaseNumber.Background = Brushes.White;
            }

            time_keeper.EndTimeField = string.Empty;
            time_keeper.NotesField = string.Empty;
            if (FldNotes != null)
            {
                FldNotes.IsEnabled = true;
                FldNotes.Background = Brushes.White;
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private void DgTimeRecords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (time_keeper == null)
                return;

            if (DgTimeRecords != null && DgTimeRecords.SelectedItem != null && time_keeper != null)
                time_keeper.UpdateSelectedTime();
        }

        private void DgRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow row || row.Item is not TimeEntry entry)
                return;

            var editor = new EditEntryWindow(entry)
            {
                Owner = this
            };

            if (editor.ShowDialog() == true)
            {
                // Persist after editing (covers non-time property changes as well)
                if (time_keeper != null)
                {
                    Database.Update(time_keeper.Entries);
                    time_keeper.UpdateTimeTotals();
                    time_keeper.UpdateSelectedTime();
                    time_keeper.SetStartTimeField();
                }
            }
        }

        private void BtnNotesPopOut_Click(object sender, RoutedEventArgs e)
        {
            if (time_keeper == null)
                return;

            var notesEditor = new NotesEditorWindow(time_keeper.NotesField)
            {
                Owner = this
            };

            if (notesEditor.ShowDialog() == true)
            {
                time_keeper.NotesField = notesEditor.NotesText ?? string.Empty;
            }
        }

        private void TimeField_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return || e.Key == Key.Tab)
            {
                FormatTimeField(sender as TextBox);
            }
        }

        private void TimeField_LostFocus(object sender, RoutedEventArgs e)
        {
            FormatTimeField(sender as TextBox);
        }

        private void FormatTimeField(TextBox? tb)
        {
            if (tb == null || time_keeper == null) return;
            var ts = TimeStringConverter.StringToTimeSpan(tb.Text);
            if (!ts.HasValue) return;
            var formatted = (DateTime.Today + ts.Value).ToString("hh:mm tt", CultureInfo.CurrentCulture);
            tb.Text = formatted;
            if (tb == FldStartTime)
                time_keeper.StartTimeField = formatted;
            else if (tb == FldEndTime)
                time_keeper.EndTimeField = formatted;
        }

        private void BtnProjectInfo_Click(object sender, RoutedEventArgs e)
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
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
            var optionsWindow = new OptionsWindow { Owner = this };
            optionsWindow.ShowDialog();
            // Re-apply shortcuts after possible changes
            ApplyKeyboardShortcuts();
            UpdateMenuGestureTexts();
        }

        // Build key bindings based on SettingsManager values
        public void ApplyKeyboardShortcuts()
        {
            this.InputBindings.Clear();
            var shortcuts = SettingsManager.GetAllShortcuts();

            if (shortcuts.ContainsKey("Export"))
            {
                var s = shortcuts["Export"]; this.InputBindings.Add(new KeyBinding(ExportCommand, s.Key, s.Modifiers));
            }
            if (shortcuts.ContainsKey("Insert"))
            {
                var s = shortcuts["Insert"]; this.InputBindings.Add(new KeyBinding(InsertCommand, s.Key, s.Modifiers));
            }
            if (shortcuts.ContainsKey("Today"))
            {
                var s = shortcuts["Today"]; this.InputBindings.Add(new KeyBinding(TodayCommand, s.Key, s.Modifiers));
            }
            if (shortcuts.ContainsKey("PrevDay"))
            {
                var s = shortcuts["PrevDay"]; this.InputBindings.Add(new KeyBinding(PrevDayCommand, s.Key, s.Modifiers));
            }
            if (shortcuts.ContainsKey("NextDay"))
            {
                var s = shortcuts["NextDay"]; this.InputBindings.Add(new KeyBinding(NextDayCommand, s.Key, s.Modifiers));
            }
            if (shortcuts.ContainsKey("Options"))
            {
                var s = shortcuts["Options"]; this.InputBindings.Add(new KeyBinding(OptionsCommand, s.Key, s.Modifiers));
            }
            if (shortcuts.ContainsKey("Submit"))
            {
                var s = shortcuts["Submit"]; this.InputBindings.Add(new KeyBinding(SubmitCommand, s.Key, s.Modifiers));
                if (s.Key == Key.Enter || s.Key == Key.Return)
                {
                    var altKey = s.Key == Key.Enter ? Key.Return : Key.Enter;
                    this.InputBindings.Add(new KeyBinding(SubmitCommand, altKey, s.Modifiers));
                }
            }
            if (shortcuts.ContainsKey("ToggleAll") || shortcuts.ContainsKey("MarkAll"))
            {
                var s = shortcuts.ContainsKey("ToggleAll") ? shortcuts["ToggleAll"] : shortcuts["MarkAll"];
                this.InputBindings.Add(new KeyBinding(ToggleAllCommand, s.Key, s.Modifiers));
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

            // Reuse timer instance; update interval and restart
            _statusTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
            _statusTimer.Stop();
            _statusTimer.Start();
        }

        private void InitializeTimePickerComboBoxes()
        {
            if (CmbStartHour == null || CmbStartMinute == null || CmbStartPeriod == null ||
                CmbEndHour == null || CmbEndMinute == null || CmbEndPeriod == null)
                return;

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
            if (time_keeper == null || PopupStartTime == null)
                return;

            // Parse current start time if it exists
            var currentTime = TimeStringConverter.StringToTimeSpan(time_keeper.StartTimeField);
            if (currentTime.HasValue)
            {
                var dt = DateTime.Today + currentTime.Value;
                int hour = dt.Hour > 12 ? dt.Hour - 12 : (dt.Hour == 0 ? 12 : dt.Hour);
                
                if (CmbStartHour != null)
                    CmbStartHour.SelectedItem = hour.ToString("00");
                if (CmbStartMinute != null)
                    CmbStartMinute.SelectedItem = dt.Minute.ToString("00");
                if (CmbStartPeriod != null)
                    CmbStartPeriod.SelectedItem = dt.Hour >= 12 ? "PM" : "AM";
            }

            PopupStartTime.IsOpen = true;
        }

        private void BtnEndTimePicker_Click(object sender, RoutedEventArgs e)
        {
            if (time_keeper == null || CmbEndHour == null || CmbEndMinute == null || CmbEndPeriod == null || PopupEndTime == null)
                return;

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
            if (CmbStartHour == null || CmbStartMinute == null || CmbStartPeriod == null ||
                CmbStartHour.SelectedItem == null || 
                CmbStartMinute.SelectedItem == null || 
                CmbStartPeriod.SelectedItem == null ||
                time_keeper == null || PopupStartTime == null)
                return;

            string timeString = $"{CmbStartHour.SelectedItem}:{CmbStartMinute.SelectedItem} {CmbStartPeriod.SelectedItem}";
            time_keeper.StartTimeField = timeString;
            PopupStartTime.IsOpen = false;
        }

        private void BtnSetEndTime_Click(object sender, RoutedEventArgs e)
        {
            if (CmbEndHour == null || CmbEndMinute == null || CmbEndPeriod == null ||
                CmbEndHour.SelectedItem == null || 
                CmbEndMinute.SelectedItem == null || 
                CmbEndPeriod.SelectedItem == null ||
                time_keeper == null || PopupEndTime == null)
                return;

            string timeString = $"{CmbEndHour.SelectedItem}:{CmbEndMinute.SelectedItem} {CmbEndPeriod.SelectedItem}";
            time_keeper.EndTimeField = timeString;
            PopupEndTime.IsOpen = false;
        }
    }

    public class TimeKeeper : INotifyPropertyChanged
    {
        public TimeKeeper()
        {
            time_records = new ObservableCollection<TimeEntry>();
            time_records.CollectionChanged += TimeRecords_CollectionChanged;
            date = DateTime.Today.Date;
            current_date = date.Date.ToShortDateString();
            current_id_count = 0;
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
        public TimeEntry? SelectedItem
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
            // Convert TimeSpan to TimeOnly for TimeEntry constructor
            var entry = new TimeEntry(date, id, TimeOnly.FromTimeSpan(start_time), TimeOnly.FromTimeSpan(end_time), case_number, notes);
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
            // Always pre-populate with current local time in the expected display format
            StartTimeField = DateTime.Now.ToString("hh:mm tt", CultureInfo.CurrentCulture);
        }

        public void AddChangedHandlerToAllEntries()
        {
            foreach (var entry in time_records)
            {
                entry.TimeEntryChanged += OnTimeEntryChanged;
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // Event commands

        private ICommand? remove_command;
        public ICommand RemoveCommand
        {
            get
            {
                if (remove_command == null)
                    remove_command = new RelayCommand(p => RemoveCurrentlySelectedEntry());
                return remove_command;
            }
        }

        // Private vars

        private DateTime date;
        private String current_date;
        private int current_id_count;
        private ObservableCollection<TimeEntry> time_records;
        private string start_time = string.Empty;
        private string end_time = string.Empty;
        private string case_no = string.Empty;
        private string notes = string.Empty;

        private double hours_total;
        private double gaps_total;
        private string selected_hours = "-";
        private string selected_mins = "-";
        private TimeEntry? selected_item;
    }

    public static class Error
    {
        public static void Handle(string error_text, Exception e, [CallerLineNumber] int line_number = 0, [CallerMemberName] string caller = "")
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimeTrack");
            string logPath = Path.Combine(logDir, "time_track_log.txt");
            try { Directory.CreateDirectory(logDir); } catch { /* ignore */ }

            string timestamp = DateTime.UtcNow.ToString("o");
            string log = $"{timestamp},{caller},{e.GetType().Name},{e.Message.Replace("\r"," ").Replace("\n"," | ")},{error_text.Replace("\r"," ").Replace("\n"," | ")}\n";
            try { File.AppendAllText(logPath, log); } catch { /* ignore */ }

            void show()
            {
                string caption = "TimeTrack - Error";
                string msg = $"{error_text}\n\nFunction: {caller}\nLine: {line_number}\n\nException:\n{e}\nLog: {logPath}";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
            }

            var app = Application.Current;
            if (app?.Dispatcher?.CheckAccess() == true) show();
            else app?.Dispatcher?.BeginInvoke(show);
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
            string valid_24hour_format = @"^\d{2}[;:.]?(\d{2})?(?i:(\s?)+(?!AM)(PM)?)$";

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
            // Regex: looks for 1 or 2 digits, a colon, then 2 digits .'
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
                // Regex: if there are only 3, or 1 digit
                // followed by either a non digit, or the end of the string (not captured)
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
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (!IsValidTimeFormat(value))
                return null;

            bool is24Hour = Is24HourFormat(value);

            // Clean and normalize input first
            value = CleanTimeString(value, is24Hour);

            // Re-evaluate after cleaning
            bool hasPeriod = TimePeriodPresent(value);      // AM/PM present
            bool hasMinutes = ContainsMinutes(value);       // contains ":mm"

            // Build the format in the correct order
            string timeFormat =
                is24Hour
                    ? (hasMinutes ? "HH:mm" : "HH")
                    : (hasMinutes ? "hh:mm" : "hh");

            // Only 12-hour format should include AM/PM
            if (!is24Hour && hasPeriod)
                timeFormat += "tt"; // no space – CleanTimeString removed spaces

            if (!DateTime.TryParseExact(value, timeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return null;

            // If no AM/PM and not 24-hour, clamp to work hours as before
            if (!hasPeriod && !is24Hour)
                parsed = ClampToWorkHours(parsed);

            return parsed.TimeOfDay;
        }
    }

    public class RelayCommand : ICommand
    {
        private Action<object?> execute;
        private Func<object?, bool>? canExecute; // Make canExecute nullable

        public RelayCommand(Action<object?> execute)
        {
            this.execute = execute;
            this.canExecute = null;
        }

        public RelayCommand(Action<object?> execute, Func<object?, bool> canExecute)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged // <-- Make nullable to match ICommand
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return this.canExecute == null || this.canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            this.execute(parameter);
        }
    }
}