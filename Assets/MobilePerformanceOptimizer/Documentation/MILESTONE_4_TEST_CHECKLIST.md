# Milestone 4 — Unity 6 Test Checklist

This checklist is for the full module test after the Milestone 4 development build imports successfully.

## A. Import / Compile Gate

- [ ] Remove or back up the previous `Assets/MobilePerformanceOptimizer` folder.
- [ ] Import the Milestone 4 folder.
- [ ] Unity finishes script compilation.
- [ ] 0 compile errors.
- [ ] 0 obsolete API warnings caused by Mobile Performance Optimizer.
- [ ] 0 missing assembly-reference errors.
- [ ] Tool opens from `Tools > Mobile Performance Optimizer > Open Optimizer`.

## B. Profile / Platform

For Android and iOS, test Low End, Mid Range and High End:

- [ ] Profile selection persists.
- [ ] Changing profile marks an existing scan as needing a re-scan.
- [ ] Profile hint changes with tier/platform.
- [ ] Low End is stricter than Mid Range.
- [ ] High End is less aggressive than Mid Range.

## C. Scene Analyzer

- [ ] Active triangle count is plausible.
- [ ] Active renderer count is plausible.
- [ ] Full-screen camera count does not get inflated by disabled cameras.
- [ ] RenderTexture / partial viewport cameras do not alone trigger the full-screen camera rule.

## D. Texture Analyzer

Test textures with:

- [ ] 4096/8192 source sizes.
- [ ] Read/Write On and Off.
- [ ] Sprite/UI mipmaps On and Off.
- [ ] Android override.
- [ ] iPhone override.
- [ ] Environment, character and UI-style paths.
- [ ] Estimated memory text is plausible.
- [ ] Review-required Read/Write fix still previews/applies/reverts.

## E. Mesh Analyzer

- [ ] High-poly active mesh reports correctly.
- [ ] High-poly unused project mesh does not receive the same LOD warning as an active mesh.
- [ ] LODGroup removes the loaded-scene missing-LOD concern for referenced LOD meshes.
- [ ] Skinned bone guidance appears only when loaded usage exceeds the tier threshold.
- [ ] Blend-shape guidance is plausible.
- [ ] Model Read/Write preview/apply/revert still works.

## F. Material Analyzer

- [ ] Missing/unsupported shader is Critical.
- [ ] Transparent loaded materials show usage-based guidance.
- [ ] Duplicate-material candidates are plausible.
- [ ] Similar but intentionally different materials are not incorrectly grouped where serialized properties differ.
- [ ] No material is automatically deleted or merged.

## G. Lighting Analyzer

- [ ] Fully baked lights do not increase runtime-light count.
- [ ] Mixed lights are included in runtime-capable count.
- [ ] Realtime lights are included.
- [ ] Runtime shadow count excludes fully baked lights.
- [ ] Point/Spot shadow metrics are correct.

## H. URP Analyzer

- [ ] Active URP asset is detected.
- [ ] Render Scale check works.
- [ ] MSAA check works.
- [ ] Shadow Distance check works.
- [ ] Shadow Cascades check works.
- [ ] HDR check works for Low End.
- [ ] Additional Light Shadows / Soft Shadows checks are readable when available.
- [ ] Depth Texture / Opaque Texture checks are readable when available.
- [ ] Missing serialized properties do not crash the scan.

## I. Particle Analyzer

Create/test effects with combinations of:

- [ ] High maxParticles.
- [ ] High rate over time + long lifetime.
- [ ] Collision.
- [ ] Trails.
- [ ] Lights.
- [ ] Sub emitters.
- [ ] Mesh renderer mode.
- [ ] Low-cost effects remain Pass where expected.

## J. Physics Analyzer

- [ ] Dynamic Rigidbody/Rigidbody2D counts are plausible.
- [ ] MeshCollider count is plausible.
- [ ] Dynamic Rigidbody + MeshCollider case is detected.
- [ ] Fixed timestep warning changes with device profile as expected.

## K. UI Analyzer

- [ ] Canvas count is plausible.
- [ ] Raycast Target count is plausible.
- [ ] Interactive Selectables are not counted as likely-unneeded raycasts.
- [ ] Many non-interactive Graphics with Raycast Target can trigger guidance.
- [ ] Small UI does not trigger a noisy raycast warning.
- [ ] Nested LayoutGroup count is plausible.
- [ ] ContentSizeFitter-in-layout count is plausible.
- [ ] Mask / mesh-effect metrics are plausible.

## L. Audio Analyzer

- [ ] Long Decompress On Load clip is detected.
- [ ] Very long non-streaming clip is detected.
- [ ] Long PCM clip is detected.
- [ ] Streaming review fix previews/applies/reverts.
- [ ] No obsolete `AudioImporter.preloadAudioData` API warning appears.

## M. Build / Quality

- [ ] Development/debug/profiler flags are detected.
- [ ] Safe build-flags fix previews/applies/reverts.
- [ ] Quality MSAA guidance works.
- [ ] Shadow Distance guidance works.
- [ ] Low-End LOD Bias guidance works.
- [ ] Low-End mipmap-streaming suggestion is reasonable for test project content.

## N. Issues UX

- [ ] Search works.
- [ ] Category filter works.
- [ ] Severity toggles work.
- [ ] Fixable Only works.
- [ ] High Impact works.
- [ ] Sort: Severity works.
- [ ] Sort: Estimated Impact works.
- [ ] Sort: Category works.
- [ ] Sort: Asset Name works.
- [ ] Issue cards display CPU/GPU/Memory/Build/Thermal impact.
- [ ] Ping works where a target exists.
- [ ] Ignore Once / Rule / Asset / Folder still work.

## O. Dashboard / Reports

- [ ] Overall score renders.
- [ ] Estimated Impact Map renders.
- [ ] Top issues prioritize severity + impact.
- [ ] Category metric rows stay within the window.
- [ ] HTML export includes impact data.
- [ ] JSON export includes impact fields.
- [ ] CSV export includes impact columns.
- [ ] Scan history still loads older history files without crashing.
- [ ] Before/After comparison still works for matching platform/profile scans.

## P. Fix Safety / Undo

- [ ] Batch Safe Fix preview still works.
- [ ] Review-required changes are not silently included in Safe batch.
- [ ] Revert Last Fix Session restores supported settings.
- [ ] Missing/locked assets do not erase failed revert snapshots.

## Final Gate

Do not begin Asset Store packaging until:

- [ ] 0 compile errors.
- [ ] 0 MPO obsolete warnings.
- [ ] No repeatable analyzer exception.
- [ ] All 12 analyzers complete a scan.
- [ ] Reports export successfully.
- [ ] Safe + review fixes have been reverted successfully in test data.

## Resilient Scan / Error Isolation

- [ ] Add/import a shader with a compiler error; Material Analyzer reports/skips it and later analyzers still run.
- [ ] Run scan while Unity is compiling shaders; shader validation can be skipped without stopping scan.
- [ ] Temporarily use a missing shader material; remaining materials still scan.
- [ ] Delete/move an asset during scan (where practical); asset is skipped and scan continues.
- [ ] Include missing/destroyed scene references; scene/component analyzers continue.
- [ ] Use malformed/unreadable model/texture/audio importer content; only the affected item is skipped.
- [ ] Verify URP serialized-property mismatch records a recoverable warning instead of aborting the scan.
- [ ] Verify `Recoverable Scan Warnings` appears in HTML/JSON/CSV metadata.
- [ ] Verify analyzer-level exception still allows all later analyzers to execute.
- [ ] Verify Cancel button still intentionally stops the scan.
- [ ] Verify no progress bar remains stuck after scan completion/error/cancel.
