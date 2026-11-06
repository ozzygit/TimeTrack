# TimeTrack v3.0.6

A small feature release adding an optional Project Code field to time entries.

## ✨ New Features

### Project Code Field

- **New "Project Code" field** next to the Ticket Number field on the entry form, letting you record a project/client code alongside each ticket
- **Recent project codes dropdown**: editable ComboBox suggests the 10 most recently used project codes, matching the Recent Tickets behaviour
- **Full persistence**: project codes are saved per entry (and per draft) with a new `project_code` database column — existing databases are migrated automatically on first launch
- **Entries grid**: a new "Project Code" column displays the code alongside each submitted entry
- The field is fully optional and does not affect submit validation or billable units calculation

## 🗄️ Technical Updates

- New `project_code` columns on `time_entries` and `drafts` tables, applied via automatic schema migration
- `GetRecentProjectCodes()` query mirroring `GetRecentTickets()`
- Default window width increased to 840px to accommodate the new field

## 📦 Installation

Download `TimeTrackv3.0.6.zip` and extract to your desired location. TimeTrack will handle database migration automatically on first run.

## 📝 Notes

For detailed commit history, see: [Commits since v3.0.5](https://github.com/ozzygit/TimeTrack/compare/v3.0.5...v3.0.6)

---

**Questions or issues?** Open an issue on GitHub: https://github.com/ozzygit/TimeTrack/issues
