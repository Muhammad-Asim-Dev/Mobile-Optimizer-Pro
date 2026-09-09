# Mobile Performance Optimizer — Milestone 3

Version: `0.3.0-m3`

Milestone 3 builds directly on the Unity 6-fixed Milestone 2 baseline. The analyzer architecture is unchanged.

## Main additions

### Reports

A new **Reports & History** tab can export the current scan as:

- HTML
- JSON
- CSV

Reports include project/Unity metadata, target platform, device profile, overall score, category scores, metrics, issue severity, asset path, recommendation, fix safety, rule ID, penalty and ignored state.

HTML is intended for human-readable review. JSON is intended for tooling/automation. CSV is intended for spreadsheet/QA workflows.

### Scan History

Completed non-cancelled scans are stored locally at:

`Library/MobilePerformanceOptimizer/ScanHistory.json`

Up to 25 recent scans are retained. This is generated project-local data and is not shipped as an Asset Store content file.

### Before / After Comparison

The Reports & History tab automatically compares the current scan with the previous completed scan that used the same:

- Target platform
- Device profile

Comparison shows:

- Before score
- Current score
- Score delta
- Critical delta
- Warning delta
- Resolved active findings
- New active findings

The score remains advisory static analysis and is not an FPS prediction.

### Batch Safe-Fix Preview

**Fixes & Ignore > Preview All Safe Fixes** opens a selection window before changes are applied.

The window:

- Deduplicates equivalent fix actions
- Lets the user select/deselect actions
- Shows the planned change
- Applies only selected fixes
- Uses the existing revert snapshot system

Review-required fixes continue to use individual previews rather than blind batch application.

### Optimization Session Improvements

The Fixes & Ignore page now shows:

- Captured change count
- Session start time
- A short list of captured changes
- Revert Last Fix Session

Original supported values are still stored in:

`Library/MobilePerformanceOptimizer/LastFixSession.json`

### Ignore Once

Findings now support:

- Ignore once
- Ignore this rule
- Ignore this asset
- Ignore this folder

**Ignore once** applies only to the current scan result. It is cleared automatically when a new scan starts and is not persisted to EditorPrefs. Persistent rule/asset/folder ignores continue to work as before.

## Recommended workflow

1. Open `Tools > Mobile Performance Optimizer > Open Optimizer`.
2. Select Android/iOS and device profile.
3. Analyze Project.
4. Review Overview and Issues.
5. Preview individual review-required fixes.
6. Use Preview All Safe Fixes for batch-safe changes.
7. Re-scan after changes.
8. Open Reports & History to compare before/after.
9. Export HTML, JSON or CSV as needed.
10. Revert the current optimization session if a supported automatic change needs to be restored.

## Safety model carried forward

### Safe

Currently includes release/debug build flag cleanup.

### Review Required

Currently includes texture Read/Write, imported model Read/Write and long-audio streaming changes.

### Manual

Gameplay-, rendering- or content-sensitive changes remain recommendation-only.

## Important boundaries

- Reports represent static scan findings, not runtime profiler captures.
- Scan history is stored under Library and can be deleted safely.
- A cancelled scan is not added to history.
- Comparison requires two completed scans with matching platform/profile.
- Review-required changes are intentionally not included in the one-click safe batch.
- Runtime performance must still be validated on representative devices with Unity Profiler and platform GPU tools.
