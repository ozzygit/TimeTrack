using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Xml.Linq;

namespace TimeTrack.Utilities
{
    public enum ThemeMode
    {
        SystemDefault,
        Light,
        Dark,
        MonokaiDimmed,
        KimbieDark,
        SolarizedDark,
        TomorrowNightBlue,
        HighContrast
    }

    public enum FontSizeScale
    {
        Small,
        Medium,
        Large,
        ExtraLarge
    }

    public enum FontFamilyOption
    {
        SegoeUI,
        AtkinsonHyperlegible,
        Lexend,
        OpenDyslexic
    }

    public enum NotificationStyle
    {
        Calm,
        Standard
    }

    public class KeyboardShortcut
    {
        public string ActionName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }

        public string DisplayText
        {
            get
            {
                if (Key == Key.None)
                    return "None";

                string result = string.Empty;
                if (Modifiers.HasFlag(ModifierKeys.Control))
                    result += "Ctrl+";
                if (Modifiers.HasFlag(ModifierKeys.Alt))
                    result += "Alt+";
                if (Modifiers.HasFlag(ModifierKeys.Shift))
                    result += "Shift+";
                if (Modifiers.HasFlag(ModifierKeys.Windows))
                    result += "Win+";

                result += Key.ToString();
                return result;
            }
        }
    }

    public static class SettingsManager
    {
        private static readonly string SettingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "timetrack_settings.xml");
        private static Dictionary<string, KeyboardShortcut> _shortcuts = new();
        public static ThemeMode Theme { get; set; } = ThemeMode.Light;
        public static bool MinimizeToTray { get; set; } = true;
        public static bool CloseToTray { get; set; } = false;
        public static bool ShowTrayIcon { get; set; } = true;
        public static bool StartWithWindows { get; set; } = false;
        public static bool ConfirmDelete { get; set; } = true;

        // ── Accessibility / Neurodiverse-friendly settings (33 properties) ──

        // 0a. First-run prompt
        public static bool AccessibilityPromptShown { get; set; } = false;

        // 1a. Day Timeline Bar
        public static bool DayTimelineEnabled { get; set; } = true;

        // 1b. Elapsed Time Colour Coding
        public static bool TimerColourCodingEnabled { get; set; } = true;
        public static int TimerCautionMinutes { get; set; } = 120;
        public static int TimerWarningMinutes { get; set; } = 240;

        // 1c. Time of Day Context Label
        public static bool TimeOfDayLabelEnabled { get; set; } = true;

        // 1d. Visual Progress Ring / Bar
        public static bool SessionProgressEnabled { get; set; } = true;
        public static int ExpectedSessionMinutes { get; set; } = 15;

        // 1e. Overtime Mode
        public static bool OvertimeModeEnabled { get; set; } = true;

        // 2a. Periodic Check-in Notifications
        public static bool CheckInEnabled { get; set; } = true;
        public static int CheckInIntervalMinutes { get; set; } = 30;

        // 2b. Idle Detection Nudge
        public static bool IdleDetectionEnabled { get; set; } = true;
        public static int IdleThresholdMinutes { get; set; } = 15;
        public static int AutoPauseThresholdMinutes { get; set; } = 0;

        // 2c. End-of-Day Summary Prompt
        public static bool EodReminderEnabled { get; set; } = true;
        public static string EodReminderTime { get; set; } = "17:00";

        // 2d. Unsubmitted Entry Warning
        public static bool UnsubmittedWarningEnabled { get; set; } = true;

        // 2e. "What Was I Doing?" Context Recovery
        public static bool ContextSummaryEnabled { get; set; } = true;

        // 3a. Continue from Last Entry
        public static bool ContinueFromLastEntry { get; set; } = true;

        // 3b. Recent Tickets Dropdown
        public static bool RecentTicketsEnabled { get; set; } = true;

        // 3c. One-Click Quick Start
        public static bool QuickStartEnabled { get; set; } = true;

        // 3d. Smart Preset Expansion
        public static bool SmartPresetsEnabled { get; set; } = true;

        // 3e. Distraction Parking Lot
        public static bool ParkingLotEnabled { get; set; } = true;

        // 3f. Park, Don't Punish
        public static bool ParkEntriesEnabled { get; set; } = true;

        // 4a. Focus Mode
        public static bool FocusModeEnabled { get; set; } = true;

        // 4b. Adjustable Font Size
        public static FontSizeScale FontSize { get; set; } = FontSizeScale.Medium;

        // 4c. High-Contrast Theme (handled via ThemeMode enum extension)

        // 4d. Reduced Motion
        public static bool ReduceMotion { get; set; } = false;

        // 4e. Entry Count Badge
        public static bool EntryCountBadgeEnabled { get; set; } = true;

        // 4f. Calm Notification Style
        public static NotificationStyle NotificationStyleMode { get; set; } = NotificationStyle.Calm;

        // 4g. Dyslexia-Friendly Font
        public static FontFamilyOption FontFamily { get; set; } = FontFamilyOption.SegoeUI;

        // 5a. Session Completion Feedback
        public static bool CompletionFeedbackEnabled { get; set; } = true;

        // 5b. Daily Streak Counter
        public static bool StreakCounterEnabled { get; set; } = true;

        // 5c. Day at a Glance Summary
        public static bool DayAtAGlanceEnabled { get; set; } = true;

        private static readonly string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "TimeTrack";

        public static void ApplyStartWithWindows()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return;

                if (StartWithWindows)
                {
                    var exePath = Environment.ProcessPath;
                    if (exePath != null)
                        key.SetValue(AppName, $"\"{exePath}\"");
                }
                else
                {
                    if (key.GetValue(AppName) != null)
                        key.DeleteValue(AppName, false);
                }
            }
            catch { }
        }

        public static bool IsStartWithWindowsEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch { return false; }
        }

        static SettingsManager()
        {
            InitializeDefaults();
            Load();
        }

        private static void InitializeDefaults()
        {
            _shortcuts = new Dictionary<string, KeyboardShortcut>
            {
                { "Submit", new KeyboardShortcut { ActionName = "Submit", DisplayName = "Submit Entry", Key = Key.Enter, Modifiers = ModifierKeys.Control } },
                { "Insert", new KeyboardShortcut { ActionName = "Insert", DisplayName = "Insert Record", Key = Key.I, Modifiers = ModifierKeys.Control } },
                { "Delete", new KeyboardShortcut { ActionName = "Delete", DisplayName = "Delete Selected", Key = Key.Delete, Modifiers = ModifierKeys.None } },
                { "Today", new KeyboardShortcut { ActionName = "Today", DisplayName = "Go to Today", Key = Key.T, Modifiers = ModifierKeys.Control } },
                { "PrevDay", new KeyboardShortcut { ActionName = "PrevDay", DisplayName = "Previous Day", Key = Key.Left, Modifiers = ModifierKeys.Control } },
                { "NextDay", new KeyboardShortcut { ActionName = "NextDay", DisplayName = "Next Day", Key = Key.Right, Modifiers = ModifierKeys.Control } },
                { "About", new KeyboardShortcut { ActionName = "About", DisplayName = "About TimeTrack", Key = Key.F1, Modifiers = ModifierKeys.None } },
                { "Settings", new KeyboardShortcut { ActionName = "Settings", DisplayName = "Settings", Key = Key.OemComma, Modifiers = ModifierKeys.Control } },
                { "SelectAll", new KeyboardShortcut { ActionName = "SelectAll", DisplayName = "Select All", Key = Key.A, Modifiers = ModifierKeys.Control } }
            };
        }

        public static Dictionary<string, KeyboardShortcut> GetAllShortcuts()
        {
            return new Dictionary<string, KeyboardShortcut>(_shortcuts);
        }

        public static KeyboardShortcut? GetShortcut(string actionName)
        {
            return _shortcuts.TryGetValue(actionName, out var shortcut) ? shortcut : null;
        }

        public static void UpdateShortcut(string actionName, Key key, ModifierKeys modifiers)
        {
            if (_shortcuts.ContainsKey(actionName))
            {
                _shortcuts[actionName].Key = key;
                _shortcuts[actionName].Modifiers = modifiers;
            }
        }

        public static void Save()
        {
            try
            {
                var root = new XElement("Settings",
                    new XElement("Appearance",
                        new XAttribute("Theme", Theme.ToString())
                    ),
                    new XElement("Tray",
                        new XAttribute("MinimizeToTray", MinimizeToTray),
                        new XAttribute("CloseToTray", CloseToTray),
                        new XAttribute("ShowTrayIcon", ShowTrayIcon)
                    ),
                    new XElement("Behavior",
                        new XAttribute("ConfirmDelete", ConfirmDelete)
                    ),
                    new XElement("Shortcuts",
                        _shortcuts.Values.Select(s =>
                            new XElement("Shortcut",
                                new XAttribute("ActionName", s.ActionName),
                                new XAttribute("Key", s.Key.ToString()),
                                new XAttribute("Modifiers", (int)s.Modifiers)
                            )
                        )
                    ),
                    new XElement("Accessibility",
                        new XAttribute("PromptShown", AccessibilityPromptShown),
                        new XAttribute("DayTimelineEnabled", DayTimelineEnabled),
                        new XAttribute("TimerColourCodingEnabled", TimerColourCodingEnabled),
                        new XAttribute("TimerCautionMinutes", TimerCautionMinutes),
                        new XAttribute("TimerWarningMinutes", TimerWarningMinutes),
                        new XAttribute("TimeOfDayLabelEnabled", TimeOfDayLabelEnabled),
                        new XAttribute("SessionProgressEnabled", SessionProgressEnabled),
                        new XAttribute("ExpectedSessionMinutes", ExpectedSessionMinutes),
                        new XAttribute("OvertimeModeEnabled", OvertimeModeEnabled),
                        new XAttribute("CheckInEnabled", CheckInEnabled),
                        new XAttribute("CheckInIntervalMinutes", CheckInIntervalMinutes),
                        new XAttribute("IdleDetectionEnabled", IdleDetectionEnabled),
                        new XAttribute("IdleThresholdMinutes", IdleThresholdMinutes),
                        new XAttribute("AutoPauseThresholdMinutes", AutoPauseThresholdMinutes),
                        new XAttribute("EodReminderEnabled", EodReminderEnabled),
                        new XAttribute("EodReminderTime", EodReminderTime),
                        new XAttribute("UnsubmittedWarningEnabled", UnsubmittedWarningEnabled),
                        new XAttribute("ContextSummaryEnabled", ContextSummaryEnabled),
                        new XAttribute("ContinueFromLastEntry", ContinueFromLastEntry),
                        new XAttribute("RecentTicketsEnabled", RecentTicketsEnabled),
                        new XAttribute("QuickStartEnabled", QuickStartEnabled),
                        new XAttribute("SmartPresetsEnabled", SmartPresetsEnabled),
                        new XAttribute("ParkingLotEnabled", ParkingLotEnabled),
                        new XAttribute("ParkEntriesEnabled", ParkEntriesEnabled),
                        new XAttribute("FocusModeEnabled", FocusModeEnabled),
                        new XAttribute("FontSize", FontSize.ToString()),
                        new XAttribute("FontFamily", FontFamily.ToString()),
                        new XAttribute("ReduceMotion", ReduceMotion),
                        new XAttribute("NotificationStyle", NotificationStyleMode.ToString()),
                        new XAttribute("EntryCountBadgeEnabled", EntryCountBadgeEnabled),
                        new XAttribute("CompletionFeedbackEnabled", CompletionFeedbackEnabled),
                        new XAttribute("StreakCounterEnabled", StreakCounterEnabled),
                        new XAttribute("DayAtAGlanceEnabled", DayAtAGlanceEnabled)
                    )
                );

                var doc = new XDocument(root);
                doc.Save(SettingsPath);
            }
            catch (Exception e)
            {
                ErrorHandler.Handle("Failed to save settings.", e);
            }
        }

        public static void Load()
        {
            if (!File.Exists(SettingsPath))
                return;

            try
            {
                var doc = XDocument.Load(SettingsPath);
                var shortcutElements = doc.Root?.Element("Shortcuts")?.Elements("Shortcut");

                var themeStr = doc.Root?.Element("Appearance")?.Attribute("Theme")?.Value;
                if (themeStr != null && Enum.TryParse<ThemeMode>(themeStr, out var savedTheme))
                    Theme = savedTheme;

                var trayElem = doc.Root?.Element("Tray");
                if (trayElem != null)
                {
                    if (bool.TryParse(trayElem.Attribute("MinimizeToTray")?.Value, out var minTray))
                        MinimizeToTray = minTray;
                    if (bool.TryParse(trayElem.Attribute("CloseToTray")?.Value, out var closeTray))
                        CloseToTray = closeTray;
                    if (bool.TryParse(trayElem.Attribute("ShowTrayIcon")?.Value, out var showIcon))
                        ShowTrayIcon = showIcon;
                }

                var behaviorElem = doc.Root?.Element("Behavior");
                if (behaviorElem != null)
                {
                    if (bool.TryParse(behaviorElem.Attribute("ConfirmDelete")?.Value, out var confirmDel))
                        ConfirmDelete = confirmDel;
                }

                if (shortcutElements != null)
                {
                    foreach (var element in shortcutElements)
                    {
                        string? actionName = element.Attribute("ActionName")?.Value;
                        string? keyStr = element.Attribute("Key")?.Value;
                        string? modifiersStr = element.Attribute("Modifiers")?.Value;

                        if (actionName != null && keyStr != null && modifiersStr != null)
                        {
                            if (Enum.TryParse<Key>(keyStr, out Key key) &&
                                int.TryParse(modifiersStr, out int modifiersInt))
                            {
                                ModifierKeys modifiers = (ModifierKeys)modifiersInt;
                                if (_shortcuts.ContainsKey(actionName))
                                {
                                    _shortcuts[actionName].Key = key;
                                    _shortcuts[actionName].Modifiers = modifiers;
                                }
                            }
                        }
                    }
                }

                // ── Load Accessibility settings ──
                var acc = doc.Root?.Element("Accessibility");
                if (acc != null)
                {
                    if (bool.TryParse(acc.Attribute("PromptShown")?.Value, out var promptShown))
                        AccessibilityPromptShown = promptShown;
                    if (bool.TryParse(acc.Attribute("DayTimelineEnabled")?.Value, out var dayTimeline))
                        DayTimelineEnabled = dayTimeline;
                    if (bool.TryParse(acc.Attribute("TimerColourCodingEnabled")?.Value, out var timerColour))
                        TimerColourCodingEnabled = timerColour;
                    if (int.TryParse(acc.Attribute("TimerCautionMinutes")?.Value, out var cautionMin))
                        TimerCautionMinutes = cautionMin;
                    if (int.TryParse(acc.Attribute("TimerWarningMinutes")?.Value, out var warningMin))
                        TimerWarningMinutes = warningMin;
                    if (bool.TryParse(acc.Attribute("TimeOfDayLabelEnabled")?.Value, out var timeOfDay))
                        TimeOfDayLabelEnabled = timeOfDay;
                    if (bool.TryParse(acc.Attribute("SessionProgressEnabled")?.Value, out var sessionProgress))
                        SessionProgressEnabled = sessionProgress;
                    if (int.TryParse(acc.Attribute("ExpectedSessionMinutes")?.Value, out var expectedMin))
                        ExpectedSessionMinutes = expectedMin;
                    if (bool.TryParse(acc.Attribute("OvertimeModeEnabled")?.Value, out var overtime))
                        OvertimeModeEnabled = overtime;
                    if (bool.TryParse(acc.Attribute("CheckInEnabled")?.Value, out var checkIn))
                        CheckInEnabled = checkIn;
                    if (int.TryParse(acc.Attribute("CheckInIntervalMinutes")?.Value, out var checkInMin))
                        CheckInIntervalMinutes = checkInMin;
                    if (bool.TryParse(acc.Attribute("IdleDetectionEnabled")?.Value, out var idleDetect))
                        IdleDetectionEnabled = idleDetect;
                    if (int.TryParse(acc.Attribute("IdleThresholdMinutes")?.Value, out var idleMin))
                        IdleThresholdMinutes = idleMin;
                    if (int.TryParse(acc.Attribute("AutoPauseThresholdMinutes")?.Value, out var autoPauseMin))
                        AutoPauseThresholdMinutes = autoPauseMin;
                    if (bool.TryParse(acc.Attribute("EodReminderEnabled")?.Value, out var eodEnabled))
                        EodReminderEnabled = eodEnabled;
                    var eodTime = acc.Attribute("EodReminderTime")?.Value;
                    if (!string.IsNullOrEmpty(eodTime))
                        EodReminderTime = eodTime;
                    if (bool.TryParse(acc.Attribute("UnsubmittedWarningEnabled")?.Value, out var unsubmittedWarn))
                        UnsubmittedWarningEnabled = unsubmittedWarn;
                    if (bool.TryParse(acc.Attribute("ContextSummaryEnabled")?.Value, out var contextSummary))
                        ContextSummaryEnabled = contextSummary;
                    if (bool.TryParse(acc.Attribute("ContinueFromLastEntry")?.Value, out var continueLast))
                        ContinueFromLastEntry = continueLast;
                    if (bool.TryParse(acc.Attribute("RecentTicketsEnabled")?.Value, out var recentTickets))
                        RecentTicketsEnabled = recentTickets;
                    if (bool.TryParse(acc.Attribute("QuickStartEnabled")?.Value, out var quickStart))
                        QuickStartEnabled = quickStart;
                    if (bool.TryParse(acc.Attribute("SmartPresetsEnabled")?.Value, out var smartPresets))
                        SmartPresetsEnabled = smartPresets;
                    if (bool.TryParse(acc.Attribute("ParkingLotEnabled")?.Value, out var parkingLot))
                        ParkingLotEnabled = parkingLot;
                    if (bool.TryParse(acc.Attribute("ParkEntriesEnabled")?.Value, out var parkEntries))
                        ParkEntriesEnabled = parkEntries;
                    if (bool.TryParse(acc.Attribute("FocusModeEnabled")?.Value, out var focusMode))
                        FocusModeEnabled = focusMode;
                    var fontSizeStr = acc.Attribute("FontSize")?.Value;
                    if (fontSizeStr != null && Enum.TryParse<FontSizeScale>(fontSizeStr, out var fontSize))
                        FontSize = fontSize;
                    var fontFamilyStr = acc.Attribute("FontFamily")?.Value;
                    if (fontFamilyStr != null && Enum.TryParse<FontFamilyOption>(fontFamilyStr, out var fontFamily))
                        FontFamily = fontFamily;
                    if (bool.TryParse(acc.Attribute("ReduceMotion")?.Value, out var reduceMotion))
                        ReduceMotion = reduceMotion;
                    var notifStyleStr = acc.Attribute("NotificationStyle")?.Value;
                    if (notifStyleStr != null && Enum.TryParse<NotificationStyle>(notifStyleStr, out var notifStyle))
                        NotificationStyleMode = notifStyle;
                    if (bool.TryParse(acc.Attribute("EntryCountBadgeEnabled")?.Value, out var entryBadge))
                        EntryCountBadgeEnabled = entryBadge;
                    if (bool.TryParse(acc.Attribute("CompletionFeedbackEnabled")?.Value, out var completionFeedback))
                        CompletionFeedbackEnabled = completionFeedback;
                    if (bool.TryParse(acc.Attribute("StreakCounterEnabled")?.Value, out var streakCounter))
                        StreakCounterEnabled = streakCounter;
                    if (bool.TryParse(acc.Attribute("DayAtAGlanceEnabled")?.Value, out var dayAtAGlance))
                        DayAtAGlanceEnabled = dayAtAGlance;
                }
            }
            catch (Exception e)
            {
                ErrorHandler.Handle("Failed to load settings. Using defaults.", e);
            }
        }

        public static void ResetToDefaults()
        {
            InitializeDefaults();
            Save();
        }
    }
}