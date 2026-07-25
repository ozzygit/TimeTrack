# TimeTrack v3.0.5 - Pre-Release

A major redesign and modernization of TimeTrack with a completely redesigned user interface, enhanced accessibility, and powerful new productivity features.

## 🎨 New Redesigned User Interface

TimeTrack v3.0.5 introduces a **complete UI overhaul** built on modern WPF principles with a cleaner, more intuitive layout:

- **Streamlined Main Window**: Simplified timer interface with better visual hierarchy and improved entry management
- **Modern Dialog System**: Context-aware dialogs (ModernDialog, ErrorDialog) for a consistent user experience
- **Enhanced Theme Support**: 7 professional themes including a new **High Contrast theme** for improved accessibility
- **Visual Time Awareness**: New day timeline visualization, color-coded timer states (caution/warning), and progress indicators
- **System Tray Integration**: Minimize to tray and close-to-tray functionality for seamless background operation
- **Improved Focus Mode**: Now prominently displayed in the top bar for better visibility
- **Vector Graphics**: Replaced legacy PNG icons with crisp vector graphics

## ✨ Major Features & Enhancements

### Accessibility Overhaul (Phased Rollout)

#### Phase 0: Foundations
- Comprehensive accessibility settings infrastructure (33+ configurable options)
- First-run welcome prompt to help new users get started

#### Phase 1: Quick Wins
- Continue from last entry functionality
- Quick start button for rapid entry creation
- Focus mode for distraction-free tracking
- Entry status badges

#### Phase 2: Visual Time Awareness
- Day timeline visualization
- Timer color coding system (normal → caution → warning states)
- Progress bars for entry duration tracking
- Day-at-a-glance dashboard

#### Phase 3: Nudges & Reminders
- Smart check-in notifications
- Idle time detection
- End-of-day summaries
- Unsubmitted entry warnings

#### Phase 4: Cognitive Load & Personalization
- Recent tickets dropdown for quick access
- Parking Lot feature for capturing distractions
- Day streak counter for motivation
- Font size customization for readability

### Productivity Features

- **Draft Entries**: Save in-progress entries and return to them later
- **Split Entry**: Divide a single entry into multiple time segments
- **Parking Lot**: Capture and manage distraction items without losing focus
- **Recent Tickets**: Quick-access dropdown for frequently-used tickets
- **Day Streak Counter**: Visual motivation tracker for daily tracking

### Data Management Improvements

- **Portable Mode**: Store database in the app directory for easy portability
- **Recycle Bin**: Soft-delete entries with full restore capability
- **Raw SQL Architecture**: Migrated from EF Core to optimized raw SQL with Microsoft.Data.Sqlite
- **Database Backup/Restore**: Full backup and restore functionality
- **Export/Import**: Move your data between instances
- **WAL Mode Optimization**: Enhanced database performance
- **Automatic Cleanup**: Old backups are automatically managed

### Timer Enhancements

- **Timer Persistence**: Timer state is preserved when switching between entries
- **Color-Coded States**: Visual feedback with brush colors for caution and warning states
- **Smart Handling**: Proper handling of zero-duration entries (equal start/end times)

## 🗄️ Technical Updates

- **Database Layer**: Complete rewrite using raw SQL for better performance and flexibility
- **Modern App Infrastructure**: Single-instance enforcement with hung process detection
- **Auto-Launcher**: Automatic Start Menu shortcut creation on first run
- **Error Handling**: Improved exception handling with user-friendly error dialogs
- **Notes History**: Track historical changes to entry notes

## 🐛 Bug Fixes

- Fixed NullReferenceException in SettingsWindow constructor
- Fixed ProgressBar binding mode (now OneWay)
- Corrected column name mappings (ticket_number → case_number)
- Enhanced 0-duration entry handling
- Resolved focus mode visibility issues

## 📦 Installation

Download `TimeTrackv3.0.5.zip` and extract to your desired location. TimeTrack will handle database initialization automatically on first run.

## ⚠️ Breaking Changes

- Entity Framework Core has been removed; database schema is now managed via raw SQL
- Legacy UI windows (MainWindow, AboutWindow, EditEntryWindow, NotesEditorWindow) have been redesigned
- Previous migration files are no longer needed

## 📝 Notes

This is a **pre-release** version. While all major features have been thoroughly developed and tested, we recommend backing up your data before upgrading.

For detailed commit history, see: [Commits since v2.5](https://github.com/ozzygit/TimeTrack/compare/v2.5...v3.0.5)

---

**Questions or issues?** Open an issue on GitHub: https://github.com/ozzygit/TimeTrack/issues
