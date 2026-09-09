# Milestone 3 — Unity 6 Test Checklist

Target: `0.3.0-m3`

## A. Import / Compilation

- [ ] Import into a clean Unity 6 URP project.
- [ ] Confirm 0 compile errors.
- [ ] Confirm 0 obsolete API warnings caused by Mobile Performance Optimizer.
- [ ] Confirm menu opens: `Tools > Mobile Performance Optimizer > Open Optimizer`.

## B. Baseline Scan

- [ ] Select Android / Mid Range.
- [ ] Run Analyze Project.
- [ ] Scan completes without unhandled exception.
- [ ] Overview score and category cards render.
- [ ] Issues tab renders findings.
- [ ] Fixes & Ignore renders safety counts.
- [ ] Reports & History tab renders.

## C. Scan History

- [ ] First completed scan appears as CURRENT in history.
- [ ] Cancelled scan is not stored.
- [ ] Run a second scan with the same platform/profile.
- [ ] Before/After comparison appears.
- [ ] Switch profile, re-scan, and verify comparison only uses the same profile.
- [ ] Clear History works without affecting the current in-memory scan.
- [ ] Restart Unity and verify saved history reloads from Library.

## D. Reports

- [ ] Export HTML.
- [ ] Open HTML in a browser and verify score, categories and issues.
- [ ] Export JSON and confirm valid structured data.
- [ ] Export CSV and confirm it opens cleanly in a spreadsheet application.
- [ ] Verify quotes, commas and line breaks in issue text do not corrupt CSV rows.
- [ ] Verify asset paths and recommendations are escaped correctly in HTML.
- [ ] Reveal Last Export opens the saved file location.
- [ ] Canceling a Save File dialog does not throw an exception.

## E. Batch Safe Fixes

- [ ] Create/enable a safe Build Settings finding.
- [ ] Open Preview All Safe Fixes.
- [ ] Select All / Select None works.
- [ ] Apply selected safe fix.
- [ ] Fix page marks project as needing a re-scan.
- [ ] Optimization session captures the original values.
- [ ] Equivalent duplicate fix actions are only shown/applied once.

## F. Review Required Fixes

- [ ] Texture Read/Write preview still works.
- [ ] Mesh Read/Write preview still works.
- [ ] Long Audio Streaming preview still works with Unity 6 sample settings.
- [ ] Review-required fixes are not silently included in the safe batch.

## G. Revert Session

- [ ] Apply more than one supported automatic fix.
- [ ] Fix page shows session count and captured change descriptions.
- [ ] Revert Last Fix Session restores original values.
- [ ] Re-scan after revert reflects restored state.
- [ ] Revert session data clears after successful restore.

## H. Ignore System

- [ ] Ignore once hides one finding.
- [ ] Clear Ignore Once restores it.
- [ ] Ignore rule persists.
- [ ] Ignore asset persists.
- [ ] Ignore folder persists.
- [ ] Clear All Ignored Items restores persistent and one-time findings.
- [ ] Ignored findings do not reduce the displayed score.
- [ ] Exported reports correctly mark ignored findings.

## I. Regression

- [ ] Particle Analyzer works in Unity 6.
- [ ] Audio Analyzer has no obsolete preloadAudioData calls on AudioImporter itself.
- [ ] MPOIgnoreStore JsonUtility compiles.
- [ ] Audio sample-rate override conversions compile.
- [ ] Category foldouts compile/render in Unity 6.
- [ ] All Milestone 1 and Milestone 2 analyzers still run.

## Exit criteria

Milestone 3 is accepted when:

- 0 compile errors
- 0 obsolete API warnings caused by MPO
- Reports export correctly
- History persists and comparison is accurate
- Safe batch preview works
- Revert remains reliable
- Existing analyzers regressions are not observed
