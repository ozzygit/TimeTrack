# Future Changes Backlog

This backlog lists potential improvements. For each item: why, pros, cons/risks, expected Airlock impact, and estimated effort.

Build and packaging
1) [ ] Remove custom DLL moves and `additionalProbingPaths` (defer to stock layout)
- Why: Simpler, less fragile; aligns with .NET runtime expectations.
- Pros: Fewer surprises, easier support.
- Cons/Risks: None if not in use (current publish is self-contained).
- Airlock impact: Medium (new build hash).
- Effort: Small.

2) [ ] Deterministic build metadata and provenance
- Why: Reproducibility and traceability in CI.
- Pros: Deterministic builds; SourceLink and provenance.
- Cons/Risks: None functionally.
- Airlock impact: Medium (new hash).
- Effort: Small.

3) [ ] Enable PublishReadyToRun
- Why: Faster cold start.
- Pros: Perceptible startup improvement.
- Cons/Risks: Larger binaries; different PE layout; longer publish time.
- Airlock impact: High.
- Effort: Small.

4) [ ] Single-file publishing (self-contained, no self-extract)
- Why: Simplifies distribution.
- Pros: Fewer files; fewer missing-DLL issues.
- Cons/Risks: Larger exe; some native load edge cases.
- Airlock impact: High.
- Effort: Small.

Runtime and performance
5) [ ] TieredPGO=true
- Why: Better JIT optimization under real usage.
- Pros: Throughput gains with minimal work.
- Cons/Risks: Warmup variance.
- Airlock impact: Low (behavioral only).
- Effort: Small.

6) [ ] `GenerateResourceUsePreserializedResources=true` (WPF)
- Why: Faster resource loading at startup.
- Pros: Startup performance gains.
- Cons/Risks: Slightly longer build; larger resources.
- Airlock impact: Medium (artifact changes).
- Effort: Small.

Architecture and DI
7) [ ] Adopt Generic Host + DI + configuration
- Why: Consistent wiring for logging, DbContext, and ViewModels.
- Pros: Testability, extensibility, cleaner composition root.
- Cons/Risks: Broad refactor; learning curve.
- Airlock impact: Medium (rebuild output changes).
- Effort: Medium–Large.

8) [ ] Replace custom error handling with `ILogger` and global handlers
- Why: Centralized, structured error logging.
- Pros: Better diagnostics; fewer ad-hoc patterns.
- Cons/Risks: Initial wiring and testing.
- Airlock impact: Medium.
- Effort: Medium.

EF Core and SQLite
9) [ ] Use `AddDbContextFactory` and short?lived contexts
- Why: Avoid long?lived WPF DbContext issues.
- Pros: Fewer locks, safer threading, easier testing.
- Cons/Risks: Refactor call sites; lifetime awareness.
- Airlock impact: Medium.
- Effort: Medium.

10) [ ] Run `Database.Migrate()` on startup
- Why: Keep schema current.
- Pros: Smooth rollouts; fewer manual steps.
- Cons/Risks: Guard for first?run/readonly scenarios; error handling.
- Airlock impact: Low.
- Effort: Small.

11) [ ] SQLite tuning (WAL, busy_timeout, Cache=Shared, Pooling=true)
- Why: Fewer "database is locked" errors; better perf.
- Pros: Stability; write concurrency.
- Cons/Risks: Validate behavior on network/roaming paths.
- Airlock impact: Low.
- Effort: Small.

WPF/MVVM and UX
12) [ ] Use CommunityToolkit.Mvvm attributes (`[ObservableProperty]`, `[RelayCommand]`, `AsyncRelayCommand`)
- Why: Reduce boilerplate; async?safe UI.
- Pros: Cleaner ViewModels; fewer bugs; consistent patterns.
- Cons/Risks: Refactor effort; attribute generator adoption.
- Airlock impact: Medium.
- Effort: Medium.

13) [ ] Enable UI virtualization and app?level rendering defaults
- Why: Smooth scrolling and lower memory on large lists.
- Pros: Better perf with large datasets.
- Cons/Risks: Template tweaks if custom panels.
- Airlock impact: Low.
- Effort: Small.

14) [ ] Use `ICollectionView` for sorting/filtering
- Why: Avoid re?creating collections; better bindings.
- Pros: Performance and simpler XAML/VM interactions.
- Cons/Risks: Some XAML/VM changes required.
- Airlock impact: Low.
- Effort: Medium.

Validation and resilience
15) [ ] Implement `INotifyDataErrorInfo` for forms
- Why: Inline validation and user feedback.
- Pros: Fewer bad saves; better UX; testable rules.
- Cons/Risks: Write validation rules and error surfaces.
- Airlock impact: Low.
- Effort: Medium.

16) [ ] Startup self?checks (paths, DB access, config)
- Why: Fail fast with actionable messages.
- Pros: Reduced support time; clearer troubleshooting.
- Cons/Risks: UX for failure flows.
- Airlock impact: Low.
- Effort: Small.

Logging and diagnostics
17) [ ] Structured file logging under `%LocalAppData%` with retention
- Why: Supportability.
- Pros: Rotating logs; controlled verbosity; structured data.
- Cons/Risks: Add sink dependency (e.g., Serilog) if desired.
- Airlock impact: Medium (package update changes hashes).
- Effort: Small–Medium.

18) [ ] In?app "Open logs" and "Export diagnostics"
- Why: Faster issue triage.
- Pros: Better support flow; fewer reproduction steps.
- Cons/Risks: Minor UI work.
- Airlock impact: Low.
- Effort: Small.

Quality and tooling
19) [ ] Enable .NET analyzers and add repo `.editorconfig`
- Why: Catch issues early; consistent code quality.
- Pros: Safer refactors; fewer regressions; clearer style.
- Cons/Risks: Initial warning cleanup; dev workflow adjustment.
- Airlock impact: Medium (rebuild).
- Effort: Small–Medium.

20) [ ] Unit tests for time rounding/billable units
- Why: Lock down critical calculations.
- Pros: Confidence to change safely; prevent regressions.
- Cons/Risks: Test maintenance.
- Airlock impact: Low (not shipped).
- Effort: Small–Medium.

Security and compliance
21) [ ] Strong?name and Authenticode sign (with timestamp)
- Why: Trust and compliance; may ease Airlock via publisher rules.
- Pros: Publisher?based allow?listing; provenance.
- Cons/Risks: Cert management; pipeline updates.
- Airlock impact: Medium (publisher approval flow).
- Effort: Medium.

22) [ ] SourceLink + `PublishRepositoryUrl`
- Why: Build provenance and accurate symbols.
- Pros: Better debugging; traceability.
- Cons/Risks: None significant.
- Airlock impact: Medium (artifact metadata changes).
- Effort: Small.

Operations
23) [ ] Detect cloud?synced DB paths (OneDrive/Dropbox) and warn
- Why: Prevent sync conflicts and locks.
- Pros: Fewer data issues and support cases.
- Cons/Risks: Edge case detection/false positives.
- Airlock impact: Low.
- Effort: Small.

24) [ ] Tune log levels in Release (e.g., EF at Warning)
- Why: Reduce log noise and size.
- Pros: Clearer diagnostics; smaller log files.
- Cons/Risks: Might miss verbose traces for rare issues.
- Airlock impact: Low.
- Effort: Small.

Notes
- To avoid Airlock churn now, schedule High?impact items with a pre?approval build.
- Start with Low/Medium?impact, Small?effort items for immediate value.
