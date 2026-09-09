# Milestone 4.4 — Production UX & Prioritization

Version: `0.4.0-m4.4-production-ux`

This pass keeps the non-blocking resilient scanner from M4.2 and the UI Toolkit dashboard from M4.3, while reducing information overload and improving decision quality for large mobile projects.

## Added

- Top 5 **Problems to Fix First** on the Overview page.
- Findings are **grouped by optimization rule by default** so hundreds of similar asset warnings do not dominate the UI.
- Toggle between **Grouped Problems** and **Individual Assets**.
- Group detail view with affected-item count, representative evidence, aggregated impact and safe-fix counts.
- **View Affected Items** drill-down from a group to the underlying asset findings.
- Recommendation **confidence labels**: High Confidence, Review Recommended, Informational.
- Structured key/value presentation for analyzer evidence instead of dense paragraphs where possible.
- Recommendations rendered as short actionable bullets, with technical information placed in a collapsible section.
- Long asset paths compacted in the UI with full-path tooltips.
- Search upgraded to Unity's ToolbarSearchField.
- Full **Scan Diagnostics** window for analyzer failures and safely skipped items.
- **Manage Ignored** window to restore individual ignored rules, assets and folders.
- Score explanation foldout explaining that the score is a static heuristic, not an FPS prediction.
- HTML reports now include Top Problems to Fix First and confidence labels.
- JSON/CSV exports include recommendation confidence.
- Texture severity false-positive reduction: 2K-vs-1K guidance is now a warning/review case rather than automatically critical solely from an uncompressed upper-bound estimate. Critical is reserved for more extreme oversizing.

## Performance behavior retained

- Cooperative `EditorApplication.update` scanning.
- Small per-update scan budget.
- Incremental analyzers.
- Virtualized ListViews.
- Scan pauses during Unity compilation/importing.
- Per-item and per-analyzer error recovery.
- Scan cancellation with partial results preserved.

## Product behavior

Grouping is presentation-only. Analyzer results, scoring, fixes, ignore state and report records remain individual findings underneath. This means users can triage at a high level without losing asset-level control.
