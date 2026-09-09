# Milestone 4.3 Premium UI — Quick Acceptance Checklist

Run after a clean import in Unity 6:

1. Open `Tools > Mobile Performance Optimizer > Open Optimizer`.
2. Confirm top target bar, left navigation and themed cards render without USS warnings.
3. Resize/dock the window and confirm the main content remains usable.
4. Run a project scan and interact with the window while scanning; progress/cancel must remain responsive.
5. Open Findings with 1,000+ findings and scroll rapidly. Only virtualized ListView rows should render.
6. Test Search, Category, Sort, Critical/Warning/Suggestion, Fixable Only and High Impact filters.
7. Select findings and verify Ping Asset, Preview Fix and Ignore actions.
8. Open Fix & Ignore and test safe-fix preview, batch preview, ignore clearing and session revert.
9. Open Reports & History and test HTML, JSON, CSV, comparison, history and clear history.
10. Verify broken/unreadable project assets appear only as diagnostics/skipped items and do not stop the scan.
11. Verify both Unity Dark and Light Editor themes are readable.
12. Console target: no MPO compile errors, exceptions, obsolete API warnings or USS import warnings.
