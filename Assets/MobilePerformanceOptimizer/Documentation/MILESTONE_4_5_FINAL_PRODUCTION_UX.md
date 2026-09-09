# Milestone 4.5 — Final Production UX

Version: `0.4.5`

This build is the simplified production UX pass for Mobile Performance Optimizer Pro.
It keeps the responsive/resilient Milestone 4 scanner while reducing the amount of
technical information shown before the user needs it.

## Final visual direction

The Editor uses the **Midnight Amber** theme:

- warm near-black surfaces
- amber primary actions and selected controls
- green only for safe/positive actions
- red/yellow reserved for actual severity states
- compact, readable cards instead of large raw-data blocks

The main workflow is intentionally limited to four destinations:

1. **Scan**
2. **Problems**
3. **Fixes**
4. **Reports**

## Scan scopes

The Scan page supports five explicit targets:

### Full Project
Scans supported project assets plus currently loaded scene content.

### Build Scenes
Scans assets referenced by enabled Build Settings scenes. Scene-object analyzers use
build scenes that are already loaded. MPO does not open, close, save, or otherwise
change the user's scene setup just to scan.

### Current Scene
Scans the active scene and its referenced assets. An unsaved scene can still receive
loaded-component analysis, but it must be saved before referenced project assets can
be resolved through the AssetDatabase.

### Selected Folder
Scans supported Texture, Mesh, Material and Audio assets inside the selected Assets
folder and its subfolders.

### Selected Assets
Scans the current Project-window selection and its dependencies for supported asset
categories.

Focused folder/asset scans deliberately do not run unrelated project-setting or
scene-object checks.

## Simplified Problems UX

- category selection is visible as tabs/chips instead of a category dropdown
- similar findings remain grouped by default
- each problem first answers:
  - **Why it matters**
  - **What you should do**
- raw importer/rule/impact data is placed under **Technical details**
- large groups can still drill down to individual affected assets through a virtualized list
- advanced severity/sort filters are available under **More Filters**, not in the main path

## Fix workflow

The Fixes page separates findings into three safety levels:

### Safe Fixes
`Fix All Safe Issues` applies only actions classified `MPOFixSafety.Safe`.
Safe actions are deduplicated before execution and are recorded in the optimization
session so they can be reverted.

### Recommended Fixes
`Review All` opens a batch preview. These actions are never included in Fix All Safe.
The user chooses which changes to apply after reviewing them.

### Manual
MPO never automatically changes findings classified Manual. The user is sent back to
Problems with guidance.

## Safety rules retained

- broken/unreadable assets are skipped without stopping the remaining scan
- analyzer failures are isolated so later analyzers can continue
- scan work is split across Editor updates with a small time budget
- compile/import activity pauses scanning safely
- large result lists remain virtualized
- automatic fixes are revertible through the optimization session
- reports and history include the scan scope
- before/after comparison only compares matching Platform + Device Tier + Scan Scope

## Production note

The score and impact indicators are static project-health heuristics. They are not an
FPS prediction and should be validated with Unity Profiler and representative devices.
