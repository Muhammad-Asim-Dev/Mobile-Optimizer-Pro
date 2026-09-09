# Mobile Performance Optimizer — Milestone 2

Version: `0.2.0-m2`

## What Milestone 2 adds

Milestone 2 keeps all Milestone 1 analyzers and adds:

- Particle Analyzer
- Physics Analyzer (3D + 2D)
- UI / Canvas Analyzer (uGUI)
- Audio Analyzer
- Build Settings Analyzer
- Quality Settings Analyzer
- Safe/Review/Manual fix classifications
- Fix Preview window
- Revert Last Fix Session
- Ignore Rule / Asset / Folder
- Easier Overview / Issues / Fixes editor workflow
- Search, category filters, severity filters, fixable-only filter
- Re-scan reminder after changes

## Editor workflow

Open:

`Tools > Mobile Performance Optimizer > Open Optimizer`

Recommended flow:

1. Choose **Android** or **iOS**.
2. Choose **Low End**, **Mid Range**, or **High End**.
3. Click **Analyze Project**.
4. Start on **Overview** and review "What should I do next?".
5. Open **Issues** for detailed explanations.
6. Use **Preview Fix** only after reading what will change.
7. Open **Fixes & Ignore** to apply safe fixes, revert the last session, or manage ignored findings.
8. Re-scan after applying/reverting changes.

## Fix safety model

### Safe

Currently includes build/debug flags:

- Development Build Off
- Script Debugging Off
- Autoconnect Profiler Off
- Deep Profiling Support Off

These are useful before release-performance validation. They can still be intentionally enabled while profiling, so the analyzer explains the context.

### Review Required

Currently includes:

- Texture Read/Write On → Off
- Imported model Read/Write On → Off
- Long audio load type → Streaming + preload off

These can reduce memory, but runtime systems may depend on the current behavior. They always use Preview Fix and are snapshotted for Revert Last Fix Session.

### Manual

The optimizer intentionally does **not** blindly change:

- Particle capacities/modules
- Physics colliders/timestep
- UI raycast/layout behavior
- URP visual settings
- Quality settings
- Mesh topology / LOD setup
- Material/shader architecture
- Light/camera deletion

These can change gameplay, visuals, or rendering behavior.

## Revert system

Before an automatic fix is applied, the original supported setting/import value is captured in:

`Library/MobilePerformanceOptimizer/LastFixSession.json`

This file is project-local generated data and is not part of the Asset Store package.

Use:

**Fixes & Ignore > Revert Last Fix Session**

Revert covers all automatic fix types implemented in Milestone 2.

## Ignore system

Every finding can expose:

- Ignore this rule
- Ignore this asset
- Ignore this folder

Ignored findings are hidden and do not reduce the advisory score. Ignore preferences are local Editor preferences and can be cleared from **Fixes & Ignore**.

## UI / Canvas analyzer dependency

Milestone 2 analyzes Unity uGUI (`com.unity.ugui`) components such as Graphic, GraphicRaycaster, LayoutGroup, ContentSizeFitter, Mask, and RectMask2D. Unity 6 projects that use the standard uGUI package satisfy this dependency.

## Interpretation

- Score is advisory static analysis, not an FPS estimate.
- Thresholds are target-profile guidance, not universal hard limits.
- UI "potentially non-interactive raycast target" detection is conservative; custom interaction can still make a target necessary, so no automatic raycast fix is provided.
- Audio Streaming is not universally best for every clip; the fix is Review Required.
- Quality Analyzer inspects the currently active quality level in Milestone 2.
- Real performance must be profiled on representative devices.

## Milestone 3 remains

Planned Milestone 3 work includes:

- Before/After report snapshots
- HTML report export
- Scan history
- packaged demonstration/sample scene/assets
- editor test suite
- full release documentation / Asset Store submission cleanup
