# TimeTrack v3

A lightweight WPF desktop application for tracking time entries with instant billable unit calculations. Built for MSP (Managed Service Provider) workflows where tracking time against tickets is essential.

## Screenshots

<img width="402" height="341" alt="image" src="https://github.com/user-attachments/assets/3fa7f5f7-7272-400b-b3fa-09ffdc62251e" />
<img width="249" height="136" alt="image" src="https://github.com/user-attachments/assets/ab3fe080-4042-4b5e-9f8f-d8b8a66ee4f5" />
<img width="403" height="186" alt="image" src="https://github.com/user-attachments/assets/0c225927-1b77-4edd-a76a-e3cc947090df" />
<img width="430" height="324" alt="image" src="https://github.com/user-attachments/assets/f5c751da-7ae2-45d2-a931-3e3423f9655a" />

## Features

### Time Entry Management
- **Start/End times** with flexible input parsing — accepts `7:00 AM`, `700`, `730`, `7`, `1900`, `7.00`, `7;00`, and more
- **Ticket/Reference number** field with preset dropdown for common ticket types
- **Notes** field with character count and undo-to-previous-saved-version
- **Live duration display** showing elapsed time and billable units as you type
- **Split Entry** — split an entry at the current time, creating a new entry starting now
- **Insert blank entry** — insert a placeholder row at a specific position in the day
- **Lunch checkbox** — creates a 1-hour non-billable gap entry that doesn't count toward totals
- **Multiple entry tabs** — work on multiple time entries simultaneously with a tabbed interface
- **Running timer** — start a timer on any entry; elapsed time updates live and persists across tab switches

### Statistics & Calculations
- **Total hours** for the day (formatted as `Xh Ym`)
- **Gaps** — time between entries that isn't accounted for
- **Billable units** — automatic calculation in 6-minute (0.1 hour) increments
- **Selected entry duration** — click any entry to see its individual duration

### Date Navigation
- Calendar picker to jump to any date
- Previous/Next day navigation with keyboard shortcuts
- "Today" button with visual indicator when viewing a non-today date
- Entries auto-load when switching dates

### System Tray Integration
- **Tray icon** appears in the notification area while the app is running
- **Minimize to tray** — hiding the window instead of minimizing to the taskbar
- **Close to tray** (opt-in) — keep the app running in the background when closed
- **Tray context menu** — Show TimeTrack, Settings, About, Exit
- **Left-click tray icon** to instantly restore the window
- **Balloon notification** on first minimize/close to tray

### Recycle Bin
- **Soft-delete** — deleted entries go to the Recycle Bin instead of being permanently destroyed
- **Restore** entries from the Recycle Bin back to their original date
- **Delete permanently** — purge individual entries or empty the entire Recycle Bin
- **Auto-purge** — entries older than 90 days are automatically cleaned up on startup

### Data Management
- **Automatic backups** — database is backed up periodically (configurable interval)
- **Manual backup** — create a backup on demand from Settings > Data
- **Restore from backup** — restore the database from any previous backup (creates a safety backup first)
- **Export database** — export to a standalone `.db` file
- **Import database** — replace the current database from an imported file (with safety backup)
- **Open backup folder** — quick access to the backup directory in File Explorer

### Settings
- **Themes** — Light, Dark, Monokai Dimmed, Kimbie Dark, Solarized Dark, Tomorrow Night Blue, or System Default
- **Start with Windows** — launch TimeTrack automatically on boot (per-user, no admin required)
- **Show system tray icon** — toggle tray icon visibility
- **Minimize to system tray** — hide to tray instead of taskbar
- **Close to system tray** — keep running in background when closed (off by default)
- **Confirm before deleting** — confirmation dialog before deleting entries (on by default)
- **Customizable keyboard shortcuts** — all shortcuts can be rebound in Settings > Keyboard Shortcuts

### Keyboard Shortcuts (defaults, all rebindable)
| Action | Default |
|--------|---------|
| Submit Entry | `Ctrl+Enter` |
| Insert Record | `Ctrl+I` |
| Delete Selected | `Delete` |
| Go to Today | `Ctrl+T` |
| Previous Day | `Ctrl+Left` |
| Next Day | `Ctrl+Right` |
| About TimeTrack | `F1` |
| Settings | `Ctrl+,` |
| Select All | `Ctrl+A` |

### Application Behavior
- **Single-instance enforcement** — only one instance of TimeTrack can run; launching a second instance activates the existing window
- **Hung process detection** — if the existing instance is unresponsive, it's automatically killed and replaced
- **Start Menu shortcut** — automatically created on first launch under "T" in the Start menu
- **Portable mode** — all files (database, settings, logs, backups) are stored next to the executable
- **Modern themed dialogs** — all message boxes use custom themed dialogs matching the app's appearance

## Tech Stack

- **.NET 8** WPF (Windows Presentation Foundation)
- **Microsoft.Data.Sqlite 8.0.10** — raw SQL with SQLite (no EF Core)
- **CommunityToolkit.Mvvm 8.4.0** — MVVM source generators for observable properties
- **H.NotifyIcon.Wpf 2.2.0** — system tray icon integration

## Project Structure

```
TimeTrack/
├── App.xaml / App.xaml.cs          # Application entry point, single-instance gate, startup
├── TimeTrack.csproj                # Project file (.NET 8, WPF, single-file publish)
├── Data/
│   ├── Database.cs                 # SQLite data layer (CRUD, backups, recycle bin)
│   ├── TimeEntry.cs                # Time entry domain model
│   └── DraftEntry.cs               # Observable draft entry for editing tabs
├── ViewModels/
│   └── TimeKeeperViewModel.cs      # Main ViewModel (entries, tabs, timer, stats)
├── Views/
│   ├── MainWindow.xaml / .cs       # Main application window
│   └── Dialogs/
│       ├── SettingsWindow.xaml     # Settings (General, Keyboard, Appearance, Data)
│       ├── AboutWindow.xaml        # About dialog with version and DB info
│       ├── RecycleBinWindow.xaml   # Recycle Bin for deleted entries
│       ├── ModernDialog.xaml       # Reusable themed message dialog
│       ├── ErrorDialog.xaml        # Error dialog with selectable details
│       └── ShortcutInputDialog.xaml # Keyboard shortcut capture dialog
├── Themes/
│   ├── LightTheme.xaml
│   ├── DarkTheme.xaml
│   ├── MonokaiDimmedTheme.xaml
│   ├── KimbieDarkTheme.xaml
│   ├── SolarizedDarkTheme.xaml
│   └── TomorrowNightBlueTheme.xaml
├── Utilities/
│   ├── SettingsManager.cs          # Settings persistence (XML + registry)
│   ├── ThemeManager.cs             # Theme switching
│   ├── ErrorHandler.cs             # Error logging to file
│   ├── AppVersion.cs               # Version info
│   └── TimeStringConverter.cs      # Flexible time string parser
├── Converters/
│   └── ShortcutDisplayConverter.cs # Keyboard shortcut display formatting
└── Resources/
    └── Timetrack.ico               # Application icon
```

## Building

### Prerequisites
- .NET 8 SDK
- Windows (WPF requires Windows)

### Build
```bash
dotnet build
```

### Publish (single-file executable)
```bash
dotnet publish -c Release
```

The published executable is a self-contained single-file binary at:
```
bin/Release/net8.0-windows/win-x64/publish/TimeTrack.exe
```

## Data Storage

All data is stored in **portable mode** — files live next to the executable:

| File | Purpose |
|------|---------|
| `timetrack_v2.db` | SQLite database (all time entries) |
| `timetrack_settings.xml` | Application settings and keyboard shortcuts |
| `time_track_log.txt` | Error log |
| `Backups/` | Automatic and manual database backups |

## Authors

- **Jared Kinnear** — Original developer
- **Richard Moore** — v3 modernization and feature expansion

## License

Copyright © 2020-2026 TimeTrack Project
