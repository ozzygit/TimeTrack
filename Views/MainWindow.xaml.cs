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
using H.NotifyIcon;
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

        public static readonly RoutedUICommand SettingsCommand =
            new("Settings", "Settings", typeof(MainWindow));

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
            CommandBindings.Add(new CommandBinding(SettingsCommand, (s, e) => MenuSettings_Click(s, e)));
            CommandBindings.Add(new CommandBinding(HelpCommand, (s, e) => BtnProjectInfo_Click(s, e)));
            CommandBindings.Add(new CommandBinding(SubmitCommand, (s, e) => BtnSubmit(s, e), (s, e) => e.CanExecute = CanSubmit()));
            CommandBindings.Add(new CommandBinding(SelectAllCommand, (s, e) => BtnSelectAll(s, e)));
            CommandBindings.Add(new CommandBinding(DeleteCommand, (s, e) => BtnDelete(s, e), (s, e) => e.CanExecute = (_timeKeeper?.Entries.Any(en => en.Recorded) == true)));

            if (_timeKeeper != null)
            {
                WeakEventManager<TimeKeeperViewModel, PropertyChangedEventArgs>.AddHandler(
                    _timeKeeper,
                    nameof(_timeKeeper.PropertyChanged),
                    TimeKeeper_PropertyChanged);

                _timeKeeper.TrayNotificationRequested += OnTrayNotificationRequested;
                _timeKeeper.IdleNudgeRequested += OnIdleNudgeRequested;
                _timeKeeper.EodReminderRequested += OnEodReminderRequested;
            }

            Closed += MainWindow_Closed;
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;

            // Register user input for idle detection
            this.PreviewMouseMove += (s, e) => _timeKeeper?.RegisterUserInput();
            this.PreviewMouseDown += (s, e) => _timeKeeper?.RegisterUserInput();
            this.PreviewKeyDown += (s, e) => _timeKeeper?.RegisterUserInput();

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
            TrayIcon?.Dispose();
        }

        private bool _closeToTrayRequested = false;
        private bool _hasShownTrayNotification = false;

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && SettingsManager.MinimizeToTray && SettingsManager.ShowTrayIcon)
            {
                Hide();
                ShowTrayNotification();
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // Unsubmitted entry warning on close
            if (SettingsManager.UnsubmittedWarningEnabled && _timeKeeper != null && _timeKeeper.HasUnsubmittedEntry)
            {
                if (ModernDialog.Confirm(
                    "You have an unsubmitted entry. Submit before closing?\n\nClick 'No' to close anyway (the draft will be saved).",
                    "Unsubmitted Entry"))
                {
                    e.Cancel = true;
                    return;
                }
            }

            if (SettingsManager.CloseToTray && SettingsManager.ShowTrayIcon && !_closeToTrayRequested)
            {
                e.Cancel = true;
                Hide();
                ShowTrayNotification();
            }
        }

        private void ShowTrayNotification()
        {
            if (_hasShownTrayNotification || TrayIcon == null) return;
            _hasShownTrayNotification = true;
            try
            {
                TrayIcon?.ShowNotification("TimeTrack", "TimeTrack is still running in the background. Click the tray icon to restore.");
            }
            catch { }
        }

        private void TrayIcon_TrayLeftMouseUp(object sender, RoutedEventArgs e)
        {
            RestoreFromTray();
        }

        private void TrayIcon_TrayRightMouseDown(object sender, RoutedEventArgs e)
        {
            if (TrayIcon?.ContextMenu != null)
            {
                TrayIcon.ContextMenu.IsOpen = true;
            }
        }

        private void TrayMenu_Show_Click(object sender, RoutedEventArgs e)
        {
            RestoreFromTray();
        }

        private void TrayMenu_Settings_Click(object sender, RoutedEventArgs e)
        {
            RestoreFromTray();
            MenuSettings_Click(this, new RoutedEventArgs());
        }

        private void TrayMenu_About_Click(object sender, RoutedEventArgs e)
        {
            RestoreFromTray();
            BtnProjectInfo_Click(this, new RoutedEventArgs());
        }

        private void TrayMenu_Exit_Click(object sender, RoutedEventArgs e)
        {
            _closeToTrayRequested = true;
            Application.Current.Shutdown();
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Focus();
        }

        public void UpdateMenuGestureTexts()
        {
            // Menu bar removed — keyboard shortcuts still work via CommandBindings
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
            bool isPreset = CmbPreset?.SelectedIndex > 0;
            bool hasTicket = !string.IsNullOrWhiteSpace(_timeKeeper.TicketNumberField);
            bool hasNotes = !string.IsNullOrWhiteSpace(_timeKeeper.NotesField);

            if (!hasStart || !hasEnd) return false;
            if (!isPreset && !hasTicket) return false;
            if (!isPreset && !hasNotes) return false;
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

            var settings = SettingsManager.GetShortcut("Settings");
            if (MatchesShortcut(e, settings))
            {
                MenuSettings_Click(this, new RoutedEventArgs());
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

            if (TrayIcon != null && SettingsManager.ShowTrayIcon)
                TrayIcon.Visibility = Visibility.Visible;

            ApplyAccessibilitySettings();
        }

        private void ApplyAccessibilitySettings()
        {
            // Apply font size scale
            var scale = SettingsManager.FontSize switch
            {
                FontSizeScale.Small => 0.85,
                FontSizeScale.Medium => 1.0,
                FontSizeScale.Large => 1.15,
                FontSizeScale.ExtraLarge => 1.3,
                _ => 1.0
            };
            if (scale != 1.0)
            {
                Application.Current.Resources["GlobalFontSizeScale"] = scale;
            }

            // Apply font family
            var fontFamily = SettingsManager.FontFamily switch
            {
                FontFamilyOption.AtkinsonHyperlegible => "Atkinson Hyperlegible",
                FontFamilyOption.Lexend => "Lexend",
                FontFamilyOption.OpenDyslexic => "OpenDyslexic",
                _ => "Segoe UI"
            };
            if (fontFamily != "Segoe UI")
            {
                Application.Current.Resources["GlobalFontFamily"] = new System.Windows.Media.FontFamily(fontFamily);
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
                ShowStatus("Please enter start, end, ticket number (unless Preset), and notes", 5000);

                if ((CmbPreset == null || CmbPreset.SelectedIndex <= 0) &&
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

                if (CmbPreset != null)
                    CmbPreset.SelectedIndex = 0;

                if (DgTimeRecords != null && _timeKeeper.Entries.Count > 0)
                {
                    DgTimeRecords.SelectedIndex = _timeKeeper.Entries.Count - 1;
                    DgTimeRecords.ScrollIntoView(_timeKeeper.Entries.Last());
                }

                FldEndTime?.Focus();

                Database.Update(_timeKeeper.Entries);
                UpdateSelectAllHeaderState();

                // Completion Feedback
                if (SettingsManager.CompletionFeedbackEnabled)
                {
                    var duration = _timeKeeper.EntryDurationDisplay;
                    ShowStatus(string.IsNullOrWhiteSpace(duration)
                        ? "✓ Entry submitted successfully"
                        : $"✓ Entry submitted ({duration})");
                }
                else
                {
                    ShowStatus("Entry submitted successfully");
                }
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

        private void DateButton_Click(object sender, RoutedEventArgs e)
        {
            if (DatePopup != null)
            {
                if (DateCalendar != null && _timeKeeper != null)
                    DateCalendar.SelectedDate = _timeKeeper.Date;
                if (CalendarTodayButton != null)
                    CalendarTodayButton.Content = $"Today: {DateTime.Today.ToShortDateString()}";
                DatePopup.IsOpen = !DatePopup.IsOpen;
            }
        }

        private void DateCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateCalendar?.SelectedDate != null && _timeKeeper != null)
            {
                _timeKeeper.Date = DateCalendar.SelectedDate.Value;
                CalLoadDate(sender, e);
            }
            if (DatePopup != null)
                DatePopup.IsOpen = false;
        }

        private void CalendarToday_Click(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper != null)
            {
                _timeKeeper.Date = DateTime.Today;
                CalLoadDate(sender, e);
            }
            if (DatePopup != null)
                DatePopup.IsOpen = false;
        }

        private void BtnGotoToday(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper != null)
            {
                _timeKeeper.Date = DateTime.Today;
                CalLoadDate(sender, e);
            }
        }

        private void BtnGoForward(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper != null)
            {
                _timeKeeper.Date = _timeKeeper.Date.AddDays(1);
                CalLoadDate(sender, e);
            }
        }

        private void BtnGoBack(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper != null)
            {
                _timeKeeper.Date = _timeKeeper.Date.AddDays(-1);
                CalLoadDate(sender, e);
            }
        }

        private void CmbPreset_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_timeKeeper == null || CmbPreset == null)
                return;

            // Avoid running during initial setup
            if (!CmbPreset.IsLoaded)
                return;

            int idx = CmbPreset.SelectedIndex;
            if (idx <= 0)
            {
                // Reset — re-enable ticket and notes fields
                if (FldTicketNumber != null)
                {
                    FldTicketNumber.IsEnabled = true;
                    FldTicketNumber.Background = (Brush)FindResource("BackgroundBrush");
                }
                if (FldNotes != null)
                {
                    FldNotes.IsEnabled = true;
                    FldNotes.Background = (Brush)FindResource("BackgroundBrush");
                }
                _timeKeeper.NotesField = string.Empty;
                CommandManager.InvalidateRequerySuggested();
                return;
            }

            string presetName = (CmbPreset.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;

            // Auto-fill ticket number (editable)
            _timeKeeper.TicketNumberField = presetName;

            // Dim notes field (not required when preset is selected)
            if (FldNotes != null)
            {
                FldNotes.IsEnabled = false;
                FldNotes.Background = (Brush)FindResource("DisabledInputBrush");
            }

            // For Lunch preset, auto-set end time to start + 1 hour
            if (presetName == "Lunch" && string.IsNullOrEmpty(_timeKeeper.EndTimeField))
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

            if (_timeKeeper == null) return;

            _timeKeeper.EditEntry(entry);
            FldTicketNumber?.Focus();
            ShowStatus("Editing entry — submit to save changes");
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

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow { Owner = this };
            settingsWindow.ShowDialog();
            ApplyKeyboardShortcuts();
            UpdateMenuGestureTexts();
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow { Owner = this };
            aboutWindow.ShowDialog();
        }

        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow { Owner = this };
            settingsWindow.ShowDialog();
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
            AddBinding("Settings", SettingsCommand);
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

        private void BtnUndoNotes_Click(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;
            _timeKeeper.UndoNotes();
            ShowStatus("Notes restored to previous version", 3000);
        }

        private void BtnRecycleBin_Click(object sender, RoutedEventArgs e)
        {
            var recycleBin = new RecycleBinWindow { Owner = this };
            recycleBin.ShowDialog();

            // Refresh entries in case anything was restored
            if (_timeKeeper != null)
                CalLoadDate(sender, e);
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
            if (_timeKeeper == null) return;

            int recordedCount = _timeKeeper.Entries.Count(en => en.Recorded);
            if (recordedCount == 0) return;

            if (SettingsManager.ConfirmDelete)
            {
                if (!ModernDialog.Confirm(
                    $"Are you sure you want to delete {recordedCount} {(recordedCount == 1 ? "entry" : "entries")}?\n" +
                    "Deleted entries can be restored from the Recycle Bin.",
                    "Confirm Delete"))
                    return;
            }

            int count = _timeKeeper.RemoveRecordedEntries();
            if (count > 0)
            {
                ShowStatus($"{count} {(count == 1 ? "entry" : "entries")} deleted");
                UpdateSelectAllHeaderState();
            }
        }

        private void BtnMainTab(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;

            // Unsubmitted entry warning
            if (SettingsManager.UnsubmittedWarningEnabled && _timeKeeper.HasUnsubmittedEntry)
            {
                var ticket = string.IsNullOrWhiteSpace(_timeKeeper.TicketNumberField) ? "entry" : _timeKeeper.TicketNumberField;
                if (!ModernDialog.Confirm(
                    $"You have an entry ready to submit on {ticket}. Submit now?",
                    "Unsubmitted Entry"))
                {
                    return;
                }
            }

            _timeKeeper.FocusMainTab();
        }

        private void BtnNewEntry(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;
            if (_timeKeeper.FocusedEntry != null && string.IsNullOrWhiteSpace(_timeKeeper.TicketNumberField))
            {
                ModernDialog.ShowWarning(
                    "Please enter a ticket number before opening a new entry.",
                    "Ticket Number Required");
                return;
            }
            if (_timeKeeper.FocusedEntry != null && string.IsNullOrWhiteSpace(_timeKeeper.EndTimeField))
            {
                ModernDialog.ShowWarning(
                    "Please set a finish time before opening a new entry.",
                    "Finish Time Required");
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

        private void BtnFocusMode_Click(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;
            _timeKeeper.IsFocusMode = !_timeKeeper.IsFocusMode;
            ShowStatus(_timeKeeper.IsFocusMode ? "Focus mode enabled" : "Focus mode disabled");
        }

        private void BtnQuickStart_Click(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;
            if (_timeKeeper.IsMainTabFocused)
            {
                _timeKeeper.NewEntry();
                FldTicketNumber?.Focus();
            }
            _timeKeeper.StartTimer();
            ShowStatus("Quick start: timer running");
        }

        private void OnTrayNotificationRequested(string title, string message)
        {
            try { TrayIcon?.ShowNotification(title, message); }
            catch { }
        }

        private void OnIdleNudgeRequested(string title, string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (SettingsManager.NotificationStyleMode == NotificationStyle.Calm)
                    ShowStatus($"{title} — {message}", 10000);
                else
                    ModernDialog.ShowInfo($"{title}\n\n{message}", "Idle Nudge");
            });
        }

        private void OnEodReminderRequested()
        {
            Dispatcher.Invoke(() =>
            {
                if (_timeKeeper == null) return;
                var summary = _timeKeeper.DayAtAGlanceSummary;
                try { TrayIcon?.ShowNotification("End of Day Review", summary); }
                catch { }
                ShowStatus($"EOD: {summary}", 8000);
            });
        }

        private void BtnParkingLot_Click(object sender, RoutedEventArgs e)
        {
            if (_timeKeeper == null) return;
            var dialog = new ParkingLotDialog(_timeKeeper) { Owner = this };
            dialog.ShowDialog();
        }
    }
}