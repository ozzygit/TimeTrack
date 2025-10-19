using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TimeTrack.Data;
using TimeTrack.ViewModels;
using TimeTrack.Utilities;

namespace TimeTrack
{
    public partial class MainWindow : Window
    {
        private TimeKeeperViewModel? _timeKeeper;
        private Brush? _btnBrush;
        private readonly System.Windows.Threading.DispatcherTimer _statusTimer = new();

        // Routed commands
        public static readonly RoutedUICommand ExportCommand =
            new("Export Selected", "Export", typeof(MainWindow));

        public static readonly RoutedUICommand InsertCommand =
            new("Insert Record", "Insert", typeof(MainWindow));

        public static readonly RoutedUICommand TodayCommand =
            new("Today", "Today", typeof(MainWindow));

        public static readonly RoutedUICommand PrevDayCommand =
            new("Previous Day", "PrevDay", typeof(MainWindow));

        public static readonly RoutedUICommand NextDayCommand =
            new("Next Day", "NextDay", typeof(MainWindow));

        public static readonly RoutedUICommand OptionsCommand =
            new("Options", "Options", typeof(MainWindow));

        public static readonly RoutedUICommand HelpCommand =
            new("About", "Help", typeof(MainWindow));

        public static readonly RoutedUICommand SubmitCommand =
            new("Submit Entry", "Submit", typeof(MainWindow));
            
        public static readonly RoutedUICommand ToggleAllCommand =
            new("Toggle All Recorded", "ToggleAll", typeof(MainWindow));

        public MainWindow()
        {
            InitializeComponent();

            // Ensure DataContext is a TimeKeeperViewModel instance
            if (DataContext is not TimeKeeperViewModel)
            {
                var tk = new TimeKeeperViewModel();
                DataContext = tk;
            }
            _timeKeeper = DataContext as TimeKeeperViewModel;

            InitializeTimePickerComboBoxes();
            LoadEntriesForDate(DateTime.Today);
            InitializeWindow();
            
            if (BtnSub != null)
                _btnBrush = BtnSub.Background;
            
            ApplyKeyboardShortcuts();
            UpdateMenuGestureTexts();

            this.PreviewKeyDown += OnGlobalPreviewKeyDown;

            // Bind command handlers
            CommandBindings.Add(new CommandBinding(ExportCommand, (s, e) => BtnExport(s, e)));
            CommandBindings.Add(new CommandBinding(InsertCommand, (s, e) => BtnInsert(s, e)));
            CommandBindings.Add(new CommandBinding(TodayCommand, (s, e) => BtnGotoToday(s, e)));
            CommandBindings.Add(new CommandBinding(PrevDayCommand, (s, e) => BtnGoBack(s, e)));
            CommandBindings.Add(new CommandBinding(NextDayCommand, (s, e) => BtnGoForward(s, e)));
            CommandBindings.Add(new CommandBinding(OptionsCommand, (s, e) => MenuOptions_Click(s, e)));
            CommandBindings.Add(new CommandBinding(HelpCommand, (s, e) => BtnProjectInfo_Click(s, e)));
            CommandBindings.Add(new CommandBinding(SubmitCommand, (s, e) => BtnSubmit(s, e), (s, e) => e.CanExecute = CanSubmit()));
            CommandBindings.Add(new CommandBinding(ToggleAllCommand, (s, e) => BtnToggleAllRecorded(s, e)));

            if (_timeKeeper != null)
            {
                WeakEventManager<TimeKeeperViewModel, PropertyChangedEventArgs>.AddHandler(
                    _timeKeeper, 
                    nameof(_timeKeeper.PropertyChanged), 
                    TimeKeeper_PropertyChanged);
            }

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
                void SetText(MenuItem? mi, string action)
                {
                    var sc = SettingsManager.GetShortcut(action);
                    if (mi != null)
                        mi.InputGestureText = sc?.DisplayText ?? string.Empty;
                }

                SetText(ExportMenuItem, "Export");
                SetText(SubmitMenuItem, "Submit");
                SetText(InsertMenuItem, "Insert");
                SetText(DeleteMenuItem, "Delete");
                SetText(ToggleAllMenuItem, "ToggleAll");
                SetText(TodayMenuItem, "Today");
                SetText(PrevDayMenuItem, "PrevDay");
                SetText(NextDayMenuItem, "NextDay");
                SetText(OptionsMenuItem, "Options");
                SetText(AboutMenuItem, "About");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating menu gesture texts: {ex.Message}");
            }
        }

        private void TimeKeeper_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(TimeKeeperViewModel.StartTimeField)
                or nameof(TimeKeeperViewModel.EndTimeField)
                or nameof(TimeKeeperViewModel.CaseNumberField)
                or nameof(TimeKeeperViewModel.NotesField))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool CanSubmit()
        {
            if (_timeKeeper == null) return false;
            var hasStart = _timeKeeper.StartTimeFieldAsTime().HasValue;
            var hasEnd = _timeKeeper.EndTimeFieldAsTime().HasValue;
            bool isLunch = ChkLunch?.IsChecked == true;
            bool hasCase = !string.IsNullOrWhiteSpace(_timeKeeper.CaseNumberField);
            bool hasNotes = !string.IsNullOrWhiteSpace(_timeKeeper.NotesField);
            
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
            if ((e.Key == Key.Enter || e.Key == Key.Return) && 
                (_timeKeeper == null || string.IsNullOrWhiteSpace(_timeKeeper.NotesField)))
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

            var options = SettingsManager.GetShortcut("Options");
            if (MatchesShortcut(e, options))
            {
                MenuOptions_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // Unified Submit shortcut handling
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
                        e.Handled = true;
                    }
                }
            }
        }

        private void InitializeWindow()
        {
            FldStartTime?.Focus();
            
            if (_timeKeeper != null)
            {
                _timeKeeper.UpdateSelectedTime();
                _timeKeeper.SetStartTimeField();
                _timeKeeper.UpdateTimeTotals();
            }
        }
        
        private void LoadEntriesForDate(DateTime date)
        {
            if (_timeKeeper == null)
                return;

            _timeKeeper.Entries = Database.Retrieve(date);
            _timeKeeper.CurrentIdCount = Database.CurrentIdCount(date);
            _timeKeeper.Date = date;
        }

        private void Submit()
        {
            if (_timeKeeper == null)
                return;

            if (!CanSubmit())
            {
                ShowStatus("Please enter start, end, case number (unless Lunch), and notes", 5000);
                
                if ((ChkLunch == null || ChkLunch.IsChecked != true) && 
                    string.IsNullOrWhiteSpace(_timeKeeper.CaseNumberField))
                {
                    FldCaseNumber?.Focus();
                }
                else if (string.IsNullOrWhiteSpace(_timeKeeper.StartTimeField))
                {
                    FldStartTime?.Focus();
                }
                else if (string.IsNullOrWhiteSpace(_timeKeeper.EndTimeField))
                {
                    FldEndTime?.Focus();
                }
                else if (string.IsNullOrWhiteSpace(_timeKeeper.NotesField))
                {
                    FldNotes?.Focus();
                }
                return;
            }

            if (_timeKeeper.SubmitEntry())
            {
                _timeKeeper.ClearFieldsAndSetStartTime();
                
                if (ChkLunch != null)
                    ChkLunch.IsChecked = false;
                
                if (DgTimeRecords != null)
                {
                    DgTimeRecords.SelectedIndex = _timeKeeper.Entries.Count - 1;
                    DgTimeRecords.ScrollIntoView(_timeKeeper.Entries.Last());
                }
                
                FldEndTime?.Focus();
                
                Database.Update(_timeKeeper.Entries);
                ShowStatus("Entry submitted successfully");
            }
            else
            {
                ShowStatus("Failed to submit entry - check start and end times", 5000);
            }
        }

        private void BtnSubmit(object sender, RoutedEventArgs e)
        {
            Submit();
        }

        private void BtnInsert(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null || DgTimeRecords == null)
                return;

            int insertedIndex = DgTimeRecords.SelectedIndex;
            if (_timeKeeper.InsertBlankEntry(insertedIndex))
            {
                if (insertedIndex < 0)
                    DgTimeRecords.SelectedIndex = DgTimeRecords.Items.Count - 1;
                else
                    DgTimeRecords.SelectedIndex = insertedIndex;
                
                DgTimeRecords.Focus();
                Database.Update(_timeKeeper.Entries);
                ShowStatus("Blank entry inserted");
            }
        }

        private void BtnExport(object sender, RoutedEventArgs e)
        {
            if (DgTimeRecords?.SelectedItem is not TimeEntry selected || _timeKeeper == null)
                return;

            if (selected.StartTime == null || selected.EndTime == null)
            {
                MessageBox.Show("Record must have a valid start and end time", "TimeTrack v2 - Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (selected.EndTime < selected.StartTime)
            {
                MessageBox.Show("Cannot export a negative time duration", "TimeTrack v2 - Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string dateTime = selected.Date.ToString("yyyy-MM-dd") + " " + selected.StartTime.ToString();

            TimeSpan timespanWorked = (TimeSpan)(selected.EndTime - selected.StartTime);
            int hoursWorked = timespanWorked.Hours;
            double minutesWorked = timespanWorked.Minutes;
            double timeWorked = hoursWorked + (Math.Ceiling((minutesWorked / 60) * 10) / 10);

            string text = $"{dateTime},{timeWorked},{selected.Notes ?? string.Empty}";
            Clipboard.SetData(DataFormats.UnicodeText, text);
            selected.Recorded = true;
            Database.Update(_timeKeeper.Entries);
            ShowStatus("Entry exported to clipboard");
        }

        private void BtnToggleAllRecorded(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null)
                return;

            // Optimized LINQ: check if all entries are recorded
            bool newStatus = !_timeKeeper.Entries.Any(e => e.Recorded);

            foreach (var entry in _timeKeeper.Entries)
            {
                // Don't mark blank entries as recorded
                if (newStatus && string.IsNullOrWhiteSpace(entry.CaseNumber))
                    continue;
                    
                entry.Recorded = newStatus;
            }
            
            Database.Update(_timeKeeper.Entries);
        }

        private void CalLoadDate(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null)
                return;

            var date = _timeKeeper.Date;
            _timeKeeper.CurrentDate = date.Date.ToShortDateString();

            if (txtCurrentDate != null)
            {
                if (date != DateTime.Today)
                {
                    txtCurrentDate.Background = Brushes.OrangeRed;
                    txtCurrentDate.Foreground = Brushes.White;
                    if (BtnSub != null)
                    {
                        BtnSub.Background = Brushes.OrangeRed;
                        BtnSub.Foreground = Brushes.White;
                    }
                    if (BtnToday != null)
                        BtnToday.IsEnabled = true;
                }
                else
                {
                    txtCurrentDate.Background = null;
                    txtCurrentDate.Foreground = Brushes.Black;
                    if (_btnBrush != null && BtnSub != null)
                        BtnSub.Background = _btnBrush;
                    if (BtnSub != null)
                        BtnSub.Foreground = Brushes.Black;
                    if (BtnToday != null)
                        BtnToday.IsEnabled = false;
                }
            }

            LoadEntriesForDate(date);
            _timeKeeper.UpdateTimeTotals();
            _timeKeeper.UpdateSelectedTime();
            _timeKeeper.SetStartTimeField();
        }

        private void BtnGotoToday(object sender, RoutedEventArgs e)
        {
            if (CalDate != null)
                CalDate.SelectedDate = DateTime.Today;
        }

        private void BtnGoForward(object sender, RoutedEventArgs e)
        {
            if (CalDate?.SelectedDate != null)
                CalDate.SelectedDate = CalDate.SelectedDate.Value.AddDays(1);
        }

        private void BtnGoBack(object sender, RoutedEventArgs e)
        {
            if (CalDate?.SelectedDate != null)
                CalDate.SelectedDate = CalDate.SelectedDate.Value.AddDays(-1);
        }

        private void ChkLunch_Checked(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null)
                return;

            _timeKeeper.CaseNumberField = string.Empty;
            
            if (FldCaseNumber != null)
            {
                FldCaseNumber.IsEnabled = false;
                FldCaseNumber.Background = Brushes.LightGray;
            }

            _timeKeeper.NotesField = "Lunch";
            
            if (FldNotes != null)
            {
                FldNotes.IsEnabled = false;
                FldNotes.Background = Brushes.LightGray;
            }

            if (string.IsNullOrEmpty(_timeKeeper.EndTimeField))
            {
                var startTimeSpan = TimeStringConverter.StringToTimeSpan(_timeKeeper.StartTimeField);
                if (startTimeSpan != null)
                {
                    var endLunch = DateTime.Today + startTimeSpan.Value;
                    endLunch = endLunch.AddHours(1);
                    _timeKeeper.EndTimeField = endLunch.ToShortTimeString();
                }
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private void ChkLunch_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null)
                return;

            _timeKeeper.CaseNumberField = string.Empty;
            if (FldCaseNumber != null)
            {
                FldCaseNumber.IsEnabled = true;
                FldCaseNumber.Background = Brushes.White;
            }

            _timeKeeper.EndTimeField = string.Empty;
            _timeKeeper.NotesField = string.Empty;
            if (FldNotes != null)
            {
                FldNotes.IsEnabled = true;
                FldNotes.Background = Brushes.White;
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private void DgTimeRecords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_timeKeeper == null || DgTimeRecords?.SelectedItem == null)
                return;

            _timeKeeper.UpdateSelectedTime();
        }

        private void DgTimeRecords_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Find the DataGridRow that was right-clicked
            var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            
            if (row != null && DgTimeRecords != null)
            {
                // Explicitly set multiple selection properties to ensure it sticks
                row.IsSelected = true;
                row.Focus();
                
                // Update both DataGrid selection properties
                DgTimeRecords.SelectedItem = row.Item;
                DgTimeRecords.CurrentItem = row.Item;
                DgTimeRecords.SelectedIndex = DgTimeRecords.Items.IndexOf(row.Item);
                
                // Update the ViewModel's selected item as well
                if (_timeKeeper != null && row.Item is TimeEntry entry)
                {
                    _timeKeeper.SelectedItem = entry;
                }
                
                // Force immediate visual refresh
                DgTimeRecords.UpdateLayout();
                row.UpdateLayout();
                
                // Mark the event as handled so it doesn't bubble up
                e.Handled = true;
            }
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;
                
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void DgRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow row || row.Item is not TimeEntry entry)
                return;

            var editor = new EditEntryWindow(entry)
            {
                Owner = this
            };

            if (editor.ShowDialog() == true && _timeKeeper != null)
            {
                Database.Update(_timeKeeper.Entries);
                _timeKeeper.UpdateTimeTotals();
                _timeKeeper.UpdateSelectedTime();
                _timeKeeper.SetStartTimeField();
            }
        }

        private void BtnNotesPopOut_Click(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null)
                return;

            var notesEditor = new NotesEditorWindow(_timeKeeper.NotesField)
            {
                Owner = this
            };

            if (notesEditor.ShowDialog() == true)
            {
                _timeKeeper.NotesField = notesEditor.NotesText ?? string.Empty;
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
            if (tb == null || _timeKeeper == null) return;
            
            var ts = TimeStringConverter.StringToTimeSpan(tb.Text);
            if (!ts.HasValue) return;
            
            var formatted = (DateTime.Today + ts.Value).ToString("hh:mm tt", CultureInfo.CurrentCulture);
            tb.Text = formatted;
            
            if (tb == FldStartTime)
                _timeKeeper.StartTimeField = formatted;
            else if (tb == FldEndTime)
                _timeKeeper.EndTimeField = formatted;
        }

        private void BtnProjectInfo_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow { Owner = this };
            aboutWindow.ShowDialog();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuOptions_Click(object sender, RoutedEventArgs e)
        {
            var optionsWindow = new OptionsWindow { Owner = this };
            optionsWindow.ShowDialog();
            ApplyKeyboardShortcuts();
            UpdateMenuGestureTexts();
        }

        public void ApplyKeyboardShortcuts()
        {
            InputBindings.Clear();
            var shortcuts = SettingsManager.GetAllShortcuts();

            void AddBinding(String key, RoutedUICommand command)
            {
                if (shortcuts.TryGetValue(key, out var shortcut))
                {
                    InputBindings.Add(new KeyBinding(command, shortcut.Key, shortcut.Modifiers));
                }
            }

            AddBinding("Export", ExportCommand);
            AddBinding("Insert", InsertCommand);
            AddBinding("Today", TodayCommand);
            AddBinding("PrevDay", PrevDayCommand);
            AddBinding("NextDay", NextDayCommand);
            AddBinding("Options", OptionsCommand);
            AddBinding("About", HelpCommand);  // Add this line

            if (shortcuts.TryGetValue("Submit", out var submitShortcut))
            {
                InputBindings.Add(new KeyBinding(SubmitCommand, submitShortcut.Key, submitShortcut.Modifiers));
                if (submitShortcut.Key == Key.Enter || submitShortcut.Key == Key.Return)
                {
                    var altKey = submitShortcut.Key == Key.Enter ? Key.Return : Key.Enter;
                    InputBindings.Add(new KeyBinding(SubmitCommand, altKey, submitShortcut.Modifiers));
                }
            }

            if (shortcuts.TryGetValue("ToggleAll", out var toggleShortcut))
            {
                InputBindings.Add(new KeyBinding(ToggleAllCommand, toggleShortcut.Key, toggleShortcut.Modifiers));
            }
            else if (shortcuts.TryGetValue("MarkAll", out var markAllShortcut))
            {
                InputBindings.Add(new KeyBinding(ToggleAllCommand, markAllShortcut.Key, markAllShortcut.Modifiers));
            }
        }

        private void ShowStatus(string message, int durationMs = 3000)
        {
            if (StatusText == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: StatusText is null!");
                return;
            }
            
            StatusText.Text = message;
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
            
            // Minutes (00-59)
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
            if (_timeKeeper == null || PopupStartTime == null)
                return;

            var currentTime = TimeStringConverter.StringToTimeSpan(_timeKeeper.StartTimeField);
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
            if (_timeKeeper == null || PopupEndTime == null)
                return;

            var currentTime = TimeStringConverter.StringToTimeSpan(_timeKeeper.EndTimeField);
            if (currentTime.HasValue)
            {
                var dt = DateTime.Today + currentTime.Value;
                int hour = dt.Hour > 12 ? dt.Hour - 12 : (dt.Hour == 0 ? 12 : dt.Hour);
                
                if (CmbEndHour != null)
                    CmbEndHour.SelectedItem = hour.ToString("00");
                if (CmbEndMinute != null)
                    CmbEndMinute.SelectedItem = dt.Minute.ToString("00");
                if (CmbEndPeriod != null)
                    CmbEndPeriod.SelectedItem = dt.Hour >= 12 ? "PM" : "AM";
            }
            else
            {
                // Default to current local time when EndTimeField is empty
                var now = DateTime.Now;
                int hour = now.Hour > 12 ? now.Hour - 12 : (now.Hour == 0 ? 12 : now.Hour);
                
                if (CmbEndHour != null)
                    CmbEndHour.SelectedItem = hour.ToString("00");
                if (CmbEndMinute != null)
                    CmbEndMinute.SelectedItem = now.Minute.ToString("00");
                if (CmbEndPeriod != null)
                    CmbEndPeriod.SelectedItem = now.Hour >= 12 ? "PM" : "AM";
            }

            PopupEndTime.IsOpen = true;
        }

        private void BtnSetStartTime_Click(object sender, RoutedEventArgs e)
        {
            if (CmbStartHour?.SelectedItem == null || 
                CmbStartMinute?.SelectedItem == null || 
                CmbStartPeriod?.SelectedItem == null ||
                _timeKeeper == null || PopupStartTime == null)
                return;

            string timeString = $"{CmbStartHour.SelectedItem}:{CmbStartMinute.SelectedItem} {CmbStartPeriod.SelectedItem}";
            _timeKeeper.StartTimeField = timeString;
            PopupStartTime.IsOpen = false;
        }

        private void BtnSetEndTime_Click(object sender, RoutedEventArgs e)
        {
            if (CmbEndHour?.SelectedItem == null || 
                CmbEndMinute?.SelectedItem == null || 
                CmbEndPeriod?.SelectedItem == null ||
                _timeKeeper == null || PopupEndTime == null)
                return;

            string timeString = $"{CmbEndHour.SelectedItem}:{CmbEndMinute.SelectedItem} {CmbEndPeriod.SelectedItem}";
            _timeKeeper.EndTimeField = timeString;
            PopupEndTime.IsOpen = false;
        }
    }
}