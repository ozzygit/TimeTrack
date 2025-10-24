# Feature Recommendations Backlog

A prioritized list of feature ideas. Grouped by risk and scope. Use checkboxes to track decisions.

Low-risk (in-app only)
- [ ] System tray mini-timer + global start/stop hotkeys (quick control without opening main window) — Effort: M
- [ ] Idle detection with smart prompts (auto-split or discard idle time) — Effort: M
- [ ] Per-client rounding/units rules with live preview and “why” explanation — Effort: M
- [ ] Tags, saved filters, and fast search for entries — Effort: M
- [ ] Notes editor upgrades: spellcheck, basic Markdown preview, templates/snippets, auto-link ticket IDs — Effort: M
- [ ] Clipboard watcher: detect ticket URLs/IDs and pre-fill fields — Effort: S
- [ ] Reports/exports: weekly/monthly, per-client/tag totals, CSV/Excel export, “copy invoice lines” — Effort: M
- [ ] Dashboard: simple charts for daily/weekly totals and billable vs non-billable — Effort: M
- [ ] Auto backups with retention + DB maintenance (integrity check, compact) — Effort: S
- [ ] Portable mode toggle and DB location wizard — Effort: M
- [ ] Log viewer actions: “Open logs” and “Export diagnostics” — Effort: S
- [ ] Shortcut customization UI: expand hotkey coverage — Effort: S
- [ ] Attachments: drag/drop screenshots stored locally, linked from entries — Effort: M
- [ ] Time goals/Pomodoro: daily target, focus sessions, gentle alerts — Effort: S
- [ ] Audit trail: track edits per entry (who/when/what) locally — Effort: M
- [ ] Bulk edit: multi-select to tag/categorize or adjust billable flags — Effort: M
- [ ] First-run/onboarding tips; quick tour; open quick reference from Help — Effort: S

Medium risk (may add a package or external call)
- [ ] PSA/issue tracker helpers: parse IDs from clipboard, deep-link to Autotask/ConnectWise/Jira (no data pull initially) — Effort: M
- [ ] Theming: light/dark/high-contrast presets with app-level toggle — Effort: M

High value later (broader changes)
- [ ] Refactor to Generic Host + DI + configuration (for cleaner composition and testing) — Effort: L
- [ ] `DbContextFactory` + async command patterns to improve robustness with DB work — Effort: M

Notes
- Keep changes UI-only for now to minimize deployment risk.
- Schedule broader refactors with pre-approval when Airlock impact is acceptable.
