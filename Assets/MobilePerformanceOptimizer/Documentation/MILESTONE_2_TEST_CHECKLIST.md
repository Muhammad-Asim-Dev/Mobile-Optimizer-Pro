# Milestone 2 Test Checklist

Run after importing `Assets/MobilePerformanceOptimizer/` into a Unity 6 URP project.

## A. Upgrade / compile

- [ ] Remove/replace the old Milestone 1 `MobilePerformanceOptimizer` folder before copying Milestone 2.
- [ ] Unity compiles with 0 MPO errors.
- [ ] No obsolete/deprecated warnings from MPO.
- [ ] Window shows version `0.2.0-m2`.
- [ ] Tabs appear: Overview / Issues / Fixes & Ignore.
- [ ] Existing Milestone 1 analyzers still run.

## B. Editor usability

- [ ] Platform and Device Tier are obvious at the top.
- [ ] First scan lands on Overview.
- [ ] Overview shows score, issue counts, "What should I do next?", top issues, category health.
- [ ] "Show Issues" opens the correct category filter.
- [ ] Search works.
- [ ] Severity buttons work.
- [ ] Fixable-only works.
- [ ] Ping selects affected asset/object.
- [ ] Changing platform/tier after scan shows a Re-scan warning.

## C. Particle Analyzer

Create a particle system and test:

- [ ] `Max Particles` above profile threshold creates a finding.
- [ ] Collision module creates guidance.
- [ ] Lights module creates guidance.
- [ ] Trails/mesh rendering are reported when relevant.
- [ ] Many active particle systems trigger aggregate guidance.

## D. Physics Analyzer

- [ ] Add many Dynamic Rigidbody/Rigidbody2D components; count increases.
- [ ] Add many MeshColliders; count increases.
- [ ] Non-convex MeshCollider count is displayed in the issue.
- [ ] Set a very small Fixed Timestep; warning appears.
- [ ] Restore timestep; warning disappears after re-scan.

## E. UI / Canvas Analyzer

Using uGUI:

- [ ] Canvas count is correct for active loaded scenes.
- [ ] Graphic count looks reasonable.
- [ ] Raycast Target count changes when Image/Text raycastTarget is toggled.
- [ ] Selectable/Button graphics are not treated as obviously non-interactive.
- [ ] LayoutGroup + ContentSizeFitter count is reported.
- [ ] Mask/RectMask count is reported.
- [ ] UI analyzer never automatically disables Raycast Target.

## F. Audio Analyzer

Use a long music/ambience clip:

- [ ] Long Decompress On Load clip creates a warning/critical finding.
- [ ] Preview Fix clearly says `Streaming` and `Preload Off`.
- [ ] Apply reviewed fix changes importer.
- [ ] Re-scan shows updated result.
- [ ] Revert Last Fix Session restores the original audio importer values.

## G. Build Settings Analyzer

Temporarily enable:

- Development Build
- Script Debugging
- Autoconnect Profiler
- Deep Profiling Support

Verify:

- [ ] Analyzer reports them.
- [ ] Finding is marked Safe Fix.
- [ ] Preview shows all changes.
- [ ] Apply disables them.
- [ ] Revert Last Fix Session restores previous values.

## H. Quality Analyzer

On active quality level:

- [ ] High Shadow Distance is detected for strict profile.
- [ ] High AA is detected where applicable.
- [ ] Low-End profile with texture mipmap streaming off produces suggestion.
- [ ] Analyzer does not automatically change quality settings.

## I. Texture / Mesh reviewed fixes

Texture:

- [ ] Read/Write On shows Review Fix.
- [ ] Preview warns runtime CPU texture access can break.
- [ ] Apply sets Read/Write Off.
- [ ] Revert restores original value.

FBX/model:

- [ ] Model Read/Write On shows Review Fix.
- [ ] Apply sets importer Read/Write Off.
- [ ] Revert restores original value.
- [ ] Non-Model mesh assets stay manual if importer cannot be safely changed.

## J. Ignore

For one issue:

- [ ] Ignore this asset hides it.
- [ ] Score/count updates without re-scan.
- [ ] Ignore folder hides relevant findings under that folder.
- [ ] Ignore rule hides that rule across assets.
- [ ] Fixes & Ignore shows ignore counts.
- [ ] Clear All Ignored Items shows findings again.

## K. Stability

- [ ] Cancel scan works.
- [ ] One analyzer failure does not stop remaining analyzers.
- [ ] Enter/exit Play Mode with Domain Reload disabled without MPO errors.
- [ ] Close/reopen the optimizer window.
- [ ] Close/reopen Unity; previous fix session can still be reverted if Library data remains.

## Send back after testing

Send:

1. Exact Unity version.
2. URP version.
3. Console error/warning text, if any.
4. Screenshot of Overview.
5. Screenshot of one incorrect detection, if any.
6. Which automatic fix you tested and whether Revert restored it correctly.
