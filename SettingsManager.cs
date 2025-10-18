using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Xml.Linq;

namespace TimeTrack
{
    public class KeyboardShortcut
    {
        public string ActionName { get; set; }
        public string DisplayName { get; set; }
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }

        public string DisplayText
        {
            get
            {
                string result = "";
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
        private static string settingsPath = "timetrack_settings.xml";
        private static Dictionary<string, KeyboardShortcut> shortcuts;

        static SettingsManager()
        {
            InitializeDefaults();
            Load();
        }

        private static void InitializeDefaults()
        {
            shortcuts = new Dictionary<string, KeyboardShortcut>
            {
                { "Export", new KeyboardShortcut { ActionName = "Export", DisplayName = "Export Selected", Key = Key.E, Modifiers = ModifierKeys.Control } },
                { "Submit", new KeyboardShortcut { ActionName = "Submit", DisplayName = "Submit Entry", Key = Key.Enter, Modifiers = ModifierKeys.None } },
                { "Insert", new KeyboardShortcut { ActionName = "Insert", DisplayName = "Insert Record", Key = Key.I, Modifiers = ModifierKeys.Control } },
                { "Delete", new KeyboardShortcut { ActionName = "Delete", DisplayName = "Delete Selected", Key = Key.Delete, Modifiers = ModifierKeys.None } },
                { "Today", new KeyboardShortcut { ActionName = "Today", DisplayName = "Go to Today", Key = Key.T, Modifiers = ModifierKeys.Control } },
                { "PrevDay", new KeyboardShortcut { ActionName = "PrevDay", DisplayName = "Previous Day", Key = Key.Left, Modifiers = ModifierKeys.Control } },
                { "NextDay", new KeyboardShortcut { ActionName = "NextDay", DisplayName = "Next Day", Key = Key.Right, Modifiers = ModifierKeys.Control } },
                { "About", new KeyboardShortcut { ActionName = "About", DisplayName = "About TimeTrack", Key = Key.F1, Modifiers = ModifierKeys.None } },
                { "Options", new KeyboardShortcut { ActionName = "Options", DisplayName = "Options", Key = Key.OemComma, Modifiers = ModifierKeys.Control } },
                { "ToggleAll", new KeyboardShortcut { ActionName = "ToggleAll", DisplayName = "Toggle All", Key = Key.M, Modifiers = ModifierKeys.Control | ModifierKeys.Shift } }
            };
        }

        public static Dictionary<string, KeyboardShortcut> GetAllShortcuts()
        {
            return new Dictionary<string, KeyboardShortcut>(shortcuts);
        }

        public static KeyboardShortcut GetShortcut(string actionName)
        {
            return shortcuts.ContainsKey(actionName) ? shortcuts[actionName] : null;
        }

        public static void UpdateShortcut(string actionName, Key key, ModifierKeys modifiers)
        {
            if (shortcuts.ContainsKey(actionName))
            {
                shortcuts[actionName].Key = key;
                shortcuts[actionName].Modifiers = modifiers;
            }
        }

        public static void Save()
        {
            try
            {
                var root = new XElement("Settings",
                    new XElement("Shortcuts",
                        shortcuts.Values.Select(s =>
                            new XElement("Shortcut",
                                new XAttribute("ActionName", s.ActionName),
                                new XAttribute("Key", s.Key.ToString()),
                                new XAttribute("Modifiers", (int)s.Modifiers)
                            )
                        )
                    )
                );

                var doc = new XDocument(root);
                doc.Save(settingsPath);
            }
            catch (Exception e)
            {
                Error.Handle("Failed to save settings.", e);
            }
        }

        public static void Load()
        {
            if (!File.Exists(settingsPath))
                return;

            try
            {
                var doc = XDocument.Load(settingsPath);
                var shortcutElements = doc.Root?.Element("Shortcuts")?.Elements("Shortcut");

                if (shortcutElements != null)
                {
                    foreach (var element in shortcutElements)
                    {
                        string actionName = element.Attribute("ActionName")?.Value;
                        string keyStr = element.Attribute("Key")?.Value;
                        string modifiersStr = element.Attribute("Modifiers")?.Value;

                        if (actionName != null && keyStr != null && modifiersStr != null)
                        {
                            if (Enum.TryParse<Key>(keyStr, out Key key) &&
                                int.TryParse(modifiersStr, out int modifiersInt))
                            {
                                ModifierKeys modifiers = (ModifierKeys)modifiersInt;
                                if (shortcuts.ContainsKey(actionName))
                                {
                                    shortcuts[actionName].Key = key;
                                    shortcuts[actionName].Modifiers = modifiers;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Error.Handle("Failed to load settings. Using defaults.", e);
            }
        }

        public static void ResetToDefaults()
        {
            InitializeDefaults();
            Save();
        }
    }
}