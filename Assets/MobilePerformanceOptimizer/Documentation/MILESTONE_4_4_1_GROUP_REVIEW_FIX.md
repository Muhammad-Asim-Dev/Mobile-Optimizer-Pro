# Milestone 4.4.1 — Group Review Fix

Version: `0.4.0-m4.4.1-group-review-fix`

## Problem fixed

`Review Group` previously navigated to the grouped Findings view and filtered only by `RuleId`. That could display one or two summary rows instead of the hundreds/thousands of underlying findings the user expected to review. Existing search/category/severity filters could also silently hide group members.

## New behavior

- `Review Group` now opens **Individual assets** view.
- The filter uses the group's exact `Category + RuleId + Title` key, not RuleId alone.
- Search, category, severity, fixable-only and high-impact filters are reset before group review so no group member is silently hidden.
- A dedicated **Group Review** banner shows the selected group title and total underlying finding count.
- `Back to all findings` exits the group drill-down cleanly.
- `View Affected Items` inside group details uses the same exact drill-down path.
- Large groups remain safe because the Findings ListView stays virtualized.

## Regression checks

1. Open Overview.
2. Click `Review Group` on a group with 100+ findings.
3. Findings should switch to `Individual assets`.
4. The result count should equal the group's finding count.
5. Scroll through the list; rows should remain responsive.
6. Click `Back to all findings`; grouped view should return.
7. Repeat with groups that share the same RuleId but have different titles/categories; only the selected exact group should appear.
