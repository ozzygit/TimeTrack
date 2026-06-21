using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TimeTrack.Data;
using TimeTrack.ViewModels;
using TimeTrack.Utilities;
using TimeTrack.Views.Dialogs;

namespace TimeTrack.Views
{
    public partial class MainWindow : Window
    {
        private readonly TimeKeeperViewModel? _timeKeeper;
        private readonly System.Windows.Threading.DispatcherTimer _statusTimer = new();

        // Routed commands
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
            
        public static readonly RoutedUICommand SelectAllCommand =
            new("Select All", "SelectAll", typeof(MainWindow));

        public static readonly RoutedUICommand DeleteCommand =
            new("Delete Selected", "Delete", typeof(MainWindow));


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

            LoadEntriesForDate(DateTime.Today);
            InitializeWindow();
            
            ApplyKeyboardShortcuts();
            UpdateMenuGestureTexts();

            this.PreviewKeyDown += OnGlobalPreviewKeyDown;

            // Bind command handlers
            CommandBindings.Add(new CommandBinding(InsertCommand, (s, e) => BtnInsert(s, e)));
            CommandBindings.Add(new CommandBinding(TodayCommand, (s, e) => BtnGotoToday(s, e), (s, e) => e.CanExecute = (_timeKeeper?.IsMainTabFocused == true)));
            CommandBindings.Add(new CommandBinding(PrevDayCommand, (s, e) => BtnGoBack(s, e), (s, e) => e.CanExecute = (_timeKeeper?.IsMainTabFocused == true)));
            CommandBindings.Add(new CommandBinding(NextDayCommand, (s, e) => BtnGoForward(s, e), (s, e) => e.CanExecute = (_timeKeeper?.IsMainTabFocused == true)));
            CommandBindings.Add(new CommandBinding(OptionsCommand, (s, e) => MenuOptions_Click(s, e)));
            CommandBindings.Add(new CommandBinding(HelpCommand, (s, e) => BtnProjectInfo_Click(s, e)));
            CommandBindings.Add(new CommandBinding(SubmitCommand, (s, e) => BtnSubmit(s, e), (s, e) => e.CanExecute = CanSubmit()));
            CommandBindings.Add(new CommandBinding(SelectAllCommand, (s, e) => BtnSelectAll(s, e)));
            CommandBindings.Add(new CommandBinding(DeleteCommand, (s, e) => BtnDelete(s, e), (s, e) => e.CanExecute = (_timeKeeper?.SelectedItem != null)));

            if (_timeKeeper != null)
            {
                WeakEventManager<TimeKeeperViewModel, PropertyChangedEventArgs>.AddHandler(
                    _timeKeeper, 
                    nameof(_timeKeeper.PropertyChanged), 
                    TimeKeeper_PropertyChanged);
            }

            Closed += MainWindow_Closed;

            _statusTimer.Tick += (s, e) =>
            {
                if (StatusText != null)
                    StatusText.Text = "Ready";
                _statusTimer.Stop();
            };
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            Closed -= MainWindow_Closed;
            _statusTimer.Stop();
            _timeKeeper?.Dispose();
        }

        public void UpdateMenuGestureTexts()
        {
            try
            {
                static void SetText(MenuItem? mi, string action)
                {
                    var sc = SettingsManager.GetShortcut(action);
                    if (mi != null)
                        mi.InputGestureText = sc?.DisplayText ?? string.Empty;
                }

                SetText(SubmitMenuItem, "Submit");
                SetText(InsertMenuItem, "Insert");
                SetText(DeleteMenuItem, "Delete");
                SetText(SelectAllMenuItem, "SelectAll");
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
                or nameof(TimeKeeperViewModel.TicketNumberField)
                or nameof(TimeKeeperViewModel.NotesField)
                or nameof(TimeKeeperViewModel.SelectedItem)
                or nameof(TimeKeeperViewModel.FocusedEntry)
                or nameof(TimeKeeperViewModel.IsMainTabFocused))
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
            bool hasTicket = !string.IsNullOrWhiteSpace(_timeKeeper.TicketNumberField);
            bool hasNotes = !string.IsNullOrWhiteSpace(_timeKeeper.NotesField);
            
            if (!hasStart || !hasEnd) return false;
            if (!isLunch && !hasTicket) return false;
            if (!hasNotes) return false;
            return true;
        }

        private static bool MatchesShortcut(KeyEventArgs e, KeyboardShortcut? shortcut)
        {
            if (shortcut is null || shortcut.Key == Key.None) return false;
            
            // When Alt is pressed, WPF reports e.Key as Key.System and the actual key is in e.SystemKey
            Key actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            
            return actualKey == shortcut.Key && Keyboard.Modifiers == shortcut.Modifiers;
        }

        private void OnGlobalPreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool isEnter = e.Key == Key.Enter || e.Key == Key.Return;

            // Let multi-line TextBoxes handle Enter natively (e.g. Notes field inserts newlines),
            // unless the pressed combo matches the configured Submit shortcut — in that case
            // fall through so the submit logic below can fire.
            if (isEnter && Keyboard.FocusedElement is TextBox { AcceptsReturn: true })
            {
                var submitSc = SettingsManager.GetShortcut("Submit");
                bool matchesSubmit = submitSc != null && submitSc.Key != Key.None &&
                    (e.Key == submitSc.Key ||
                     (submitSc.Key == Key.Enter && e.Key == Key.Return) ||
                     (submitSc.Key == Key.Return && e.Key == Key.Enter)) &&
                    Keyboard.Modifiers == submitSc.Modifiers;

                if (!matchesSubmit)
                    return;
            }

            // Block Enter/Return globally until Notes has data
            if (isEnter &&
                (_timeKeeper == null || string.IsNullOrWhiteSpace(_timeKeeper.NotesField)))
            {
                e.Handled = true;
                return;
            }

            // Dynamic Prev/Next day from settings - only applicable on the main tab
            if (_timeKeeper?.IsMainTabFocused == true)
            {
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
            UpdateSelectAllHeaderState();
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
                ShowStatus("Please enter start, end, ticket number (unless Lunch), and notes", 5000);
                
                if ((ChkLunch == null || ChkLunch.IsChecked != true) && 
                    string.IsNullOrWhiteSpace(_timeKeeper.TicketNumberField))
                {
                    FldTicketNumber?.Focus();
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
                var submitted = _timeKeeper.FocusedEntry;
                if (submitted != null)
                    _timeKeeper.CloseEntry(submitted);

                if (ChkLunch != null)
                    ChkLunch.IsChecked = false;

                if (DgTimeRecords != null && _timeKeeper.Entries.Count > 0)
                {
                    DgTimeRecords.SelectedIndex = _timeKeeper.Entries.Count - 1;
                    DgTimeRecords.ScrollIntoView(_timeKeeper.Entries.Last());
                }

                FldEndTime?.Focus();

                Database.Update(_timeKeeper.Entries);
                UpdateSelectAllHeaderState();
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

        private void BtnSelectAll(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null)
                return;

            // Select all = tick every entry's checkbox; if all are already ticked, untick them.
            bool newStatus = !_timeKeeper.Entries.Any(entry => entry.Recorded);
            SetAllRecorded(newStatus);
        }

        private void SetAllRecorded(bool newStatus)
        {
            if (_timeKeeper == null)
                return;

            foreach (var entry in _timeKeeper.Entries)
            {
                // Don't mark blank entries as recorded
                if (newStatus && string.IsNullOrWhiteSpace(entry.TicketNumber))
                    continue;

                entry.Recorded = newStatus;
            }

            Database.Update(_timeKeeper.Entries);
            UpdateSelectAllHeaderState();
        }

        private void UpdateSelectAllHeaderState()
        {
            if (ChkSelectAllHeader == null || _timeKeeper == null)
                return;

            var selectable = _timeKeeper.Entries.Where(en => !string.IsNullOrWhiteSpace(en.TicketNumber)).ToList();
            _suppressHeaderCheck = true;
            ChkSelectAllHeader.IsChecked = selectable.Count > 0 && selectable.All(en => en.Recorded);
            _suppressHeaderCheck = false;
        }

        private bool _suppressHeaderCheck;

        private void ChkSelectAllHeader_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressHeaderCheck || sender is not CheckBox cb)
                return;

            // Ticked = select all, unticked = deselect all.
            SetAllRecorded(cb.IsChecked == true);
        }

        private void CalLoadDate(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null)
                return;

            var date = _timeKeeper.Date;

            LoadEntriesForDate(date);
            _timeKeeper.UpdateTimeTotals();
            _timeKeeper.UpdateSelectedTime();
            _timeKeeper.SetStartTimeField();
            UpdateSelectAllHeaderState();
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

            _timeKeeper.TicketNumberField = string.Empty;
            
            if (FldTicketNumber != null)
            {
                FldTicketNumber.IsEnabled = false;
                FldTicketNumber.Background = (Brush)FindResource("DisabledInputBrush");
            }

            _timeKeeper.NotesField = "Lunch";
            
            if (FldNotes != null)
            {
                FldNotes.IsEnabled = false;
                FldNotes.Background = (Brush)FindResource("DisabledInputBrush");
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

            _timeKeeper.TicketNumberField = string.Empty;
            if (FldTicketNumber != null)
            {
                FldTicketNumber.IsEnabled = true;
                FldTicketNumber.Background = (Brush)FindResource("BackgroundBrush");
            }

            _timeKeeper.EndTimeField = string.Empty;
            _timeKeeper.NotesField = string.Empty;
            if (FldNotes != null)
            {
                FldNotes.IsEnabled = true;
                FldNotes.Background = (Brush)FindResource("BackgroundBrush");
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private void DgTimeRecords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_timeKeeper == null)
                return;

            // Only update if there are actual selection changes
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is TimeEntry selectedEntry)
            {
                System.Diagnostics.Debug.WriteLine($"Selection changed: Selected entry ID {selectedEntry.ID} at {selectedEntry.StartTime}");
                _timeKeeper.SelectedItem = selectedEntry;
                _timeKeeper.UpdateSelectedTime();
            }
            else if (e.RemovedItems.Count > 0 && e.AddedItems.Count == 0)
            {
                // Something was deselected and nothing was selected
                if (DgTimeRecords?.SelectedItem == null)
                {
                    System.Diagnostics.Debug.WriteLine("Selection changed: Cleared selection");
                    _timeKeeper.SelectedItem = null;
                }
            }
        }

        private void DgTimeRecords_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Find the DataGridRow that was right-clicked
            var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            
            if (row != null && DgTimeRecords != null)
            {
                row.IsSelected = true;
                DgTimeRecords.SelectedItem = row.Item;
                DgTimeRecords.CurrentItem = row.Item;
                DgTimeRecords.SelectedIndex = DgTimeRecords.Items.IndexOf(row.Item);
                
                if (_timeKeeper != null && row.Item is TimeEntry entry)
                    _timeKeeper.SelectedItem = entry;
                
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
            
            // Update selected time display after formatting
            _timeKeeper.UpdateSelectedTime();
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
                if (shortcuts.TryGetValue(key, out var shortcut) && shortcut.Key != Key.None)
                {
                    InputBindings.Add(new KeyBinding(command, shortcut.Key, shortcut.Modifiers));
                }
            }

            AddBinding("Insert", InsertCommand);
            AddBinding("Today", TodayCommand);
            AddBinding("PrevDay", PrevDayCommand);
            AddBinding("NextDay", NextDayCommand);
            AddBinding("Options", OptionsCommand);
            AddBinding("About", HelpCommand);
            AddBinding("Delete", DeleteCommand);

            if (shortcuts.TryGetValue("Submit", out var submitShortcut) && submitShortcut.Key != Key.None)
            {
                InputBindings.Add(new KeyBinding(SubmitCommand, submitShortcut.Key, submitShortcut.Modifiers));
                if (submitShortcut.Key == Key.Enter || submitShortcut.Key == Key.Return)
                {
                    var altKey = submitShortcut.Key == Key.Enter ? Key.Return : Key.Enter;
                    InputBindings.Add(new KeyBinding(SubmitCommand, altKey, submitShortcut.Modifiers));
                }
            }

            if (shortcuts.TryGetValue("SelectAll", out var selectAllShortcut) && selectAllShortcut.Key != Key.None)
            {
                InputBindings.Add(new KeyBinding(SelectAllCommand, selectAllShortcut.Key, selectAllShortcut.Modifiers));
            }
        }

        internal void ShowStatus(string message, int durationMs = 3000)
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

        private void BtnNowStart_Click(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;
            ClearGridSelection();
            _timeKeeper.SetStartTimeToNow();
        }

        private void BtnNowEnd_Click(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;
            ClearGridSelection();
            _timeKeeper.SetEndTimeToNow();
        }

        private void BtnStartTimer_Click(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;
            ClearGridSelection();
            _timeKeeper.StartTimer();
            ShowStatus("Timer started");
        }

        private void BtnStopTimer_Click(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;
            _timeKeeper.StopTimer();
            ShowStatus("Timer stopped");
        }

        private void BtnSplit_Click(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;
            ClearGridSelection();
            _timeKeeper.SplitEntry();
            ShowStatus("Entry split - new entry created");
        }

        private void ClearGridSelection()
        {
            if (DgTimeRecords != null)
            {
                DgTimeRecords.SelectedItem = null;
            }
            if (_timeKeeper != null)
            {
                _timeKeeper.SelectedItem = null;
            }
        }

        private void InputField_GotFocus(object sender, RoutedEventArgs e)
        {
            // Clear grid selection when user starts entering a new time entry
            ClearGridSelection();
        }

        private void BtnDelete(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper?.SelectedItem == null)
            {
                System.Diagnostics.Debug.WriteLine("BtnDelete: No item selected");
                return;
            }

            var itemToDelete = _timeKeeper.SelectedItem;
            System.Diagnostics.Debug.WriteLine($"BtnDelete: Deleting entry ID {itemToDelete.ID} at {itemToDelete.StartTime}");
            
            _timeKeeper.RemoveCommand.Execute(null);
            ShowStatus("Entry deleted");
        }

        private void BtnMainTab(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;
            _timeKeeper.FocusMainTab();
        }

        private void BtnNewEntry(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;
            if (_timeKeeper.FocusedEntry != null && string.IsNullOrWhiteSpace(_timeKeeper.TicketNumberField))
            {
                System.Windows.MessageBox.Show(
                    "Please enter a ticket number before opening a new entry.",
                    "Ticket Number Required",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }
            if (_timeKeeper.FocusedEntry != null && string.IsNullOrWhiteSpace(_timeKeeper.EndTimeField))
            {
                System.Windows.MessageBox.Show(
                    "Please set a finish time before opening a new entry.",
                    "Finish Time Required",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }
            _timeKeeper.NewEntry();
            FldTicketNumber?.Focus();
            ShowStatus("New entry started");
        }

        private void BtnFocusTab(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;
            if (sender is not FrameworkElement { Tag: TimeTrack.Data.DraftEntry draft }) return;
            if (_timeKeeper.FocusedEntry == draft) return;
            _timeKeeper.SetFocusEntry(draft);
            FldTicketNumber?.Focus();
        }

        private void BtnCloseTab(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;
            if (sender is not FrameworkElement { Tag: TimeTrack.Data.DraftEntry draft }) return;
            _timeKeeper.CloseEntry(draft);
        }
    }
}