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
        Dark
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
                    new XElement("Shortcuts",
                        _shortcuts.Values.Select(s =>
                            new XElement("Shortcut",
                                new XAttribute("ActionName", s.ActionName),
                                new XAttribute("Key", s.Key.ToString()),
                                new XAttribute("Modifiers", (int)s.Modifiers)
                            )
                        )
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