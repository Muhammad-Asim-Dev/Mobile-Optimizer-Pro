# Milestone 4.4 Production UX Test Checklist

## Compile / import
- [ ] Freshly delete old `Assets/MobilePerformanceOptimizer` and import M4.4.
- [ ] 0 compile errors caused by MPO.
- [ ] 0 obsolete API warnings caused by MPO.
- [ ] UI Toolkit USS loads without warnings.

## Overview
- [ ] Score card renders correctly in Dark theme.
- [ ] Score card renders correctly in Light theme.
- [ ] `How this score is calculated` opens/closes.
- [ ] Top 5 Problems to Fix First render after a scan.
- [ ] Repeated texture/mesh/etc. findings appear as grouped problems.
- [ ] Review Group opens Findings with the correct group filter.

## Findings
- [ ] Grouped Problems is the default view.
- [ ] Individual Assets view works.
- [ ] Switching views does not freeze or allocate thousands of visual elements.
- [ ] Search works in both views.
- [ ] Category, severity, fixable and high-impact filters work in both views.
- [ ] Sort works in both views.
- [ ] Clear group filter returns to all groups.
- [ ] Group detail shows affected counts and sample asset paths.
- [ ] View Affected Items shows only underlying findings from that rule.
- [ ] Asset path tooltip shows the full path.
- [ ] Confidence badge is visible.
- [ ] Structured evidence rows render correctly.
- [ ] Technical Details foldout opens without layout issues.

## Diagnostics
- [ ] A broken shader/importer does not stop the scan.
- [ ] Informational skipped-items banner has View Diagnostics.
- [ ] Diagnostics window opens with virtualized rows.
- [ ] Failed analyzers and skipped items are distinguishable.
- [ ] Diagnostics window stays responsive with 100 retained warnings.

## Ignore management
- [ ] Ignore Rule persists.
- [ ] Ignore Asset persists.
- [ ] Ignore Folder persists.
- [ ] Manage Ignored opens.
- [ ] Restoring one ignored item only removes that item.
- [ ] Clear All works.
- [ ] Ignore Once still resets on the next scan.

## Texture false-positive sanity
- [ ] A normal 2048 texture on Low End is not automatically Critical only because the guideline is 1024.
- [ ] Extremely oversized 4096+ textures can still become Critical when appropriate.
- [ ] Read/Write warnings still appear independently.

## Reports
- [ ] HTML contains Top Problems to Fix First.
- [ ] HTML contains confidence labels.
- [ ] JSON contains confidence/confidenceName.
- [ ] CSV contains Confidence column.
- [ ] Diagnostics totals remain in reports.

## Performance stress test
- [ ] 1,000+ findings: scrolling remains smooth.
- [ ] 5,000+ findings: grouped view remains usable.
- [ ] Switching Findings/Overview does not hang.
- [ ] Search/filter changes do not re-run project scanning.
- [ ] Scan can be cancelled.
- [ ] Editor remains interactive during scan except for unavoidable individual Unity native calls.
