# Milestone 1 Test Checklist

Use this checklist after importing `Assets/MobilePerformanceOptimizer/` into a Unity 6 URP project.

## A. Import / compile

- [ ] Unity finishes compilation with **0 errors** caused by Mobile Performance Optimizer.
- [ ] No obsolete/deprecated API warnings are produced by Mobile Performance Optimizer.
- [ ] Menu exists: `Tools > Mobile Performance Optimizer > Open Optimizer`.
- [ ] Window opens and shows version `0.1.0-m1`.
- [ ] Analyze button is disabled in Play Mode.

## B. Basic scan

- [ ] Select Android + MidRange.
- [ ] Click **Analyze Project**.
- [ ] Progress bar appears and can be cancelled.
- [ ] Score appears.
- [ ] Scene, Textures, Meshes, Materials, Lighting, URP category cards appear.
- [ ] Search and Critical/Warning/Suggestion filters work.
- [ ] Ping selects the correct asset/object.

## C. Scene Analyzer

Create/enable a gameplay scene and verify:

- [ ] GameObject count looks reasonable.
- [ ] Active renderer count excludes disabled/inactive renderers.
- [ ] Active triangle count changes when a large renderer is disabled.
- [ ] Active camera count excludes disabled cameras.
- [ ] More than the selected profile's renderer/triangle guidance produces a warning.

## D. Texture Analyzer

Test with a texture that has:

- [ ] 4096 size.
- [ ] Read/Write enabled.
- [ ] Sprite/UI texture with mipmaps enabled.
- [ ] Android scan shows Android override state.
- [ ] iOS scan uses the `iPhone` texture platform settings.
- [ ] Editor/Gizmos/MobilePerformanceOptimizer-owned assets are not scanned.

Important: Missing explicit platform override by itself should **not** create a warning if the texture otherwise fits the selected profile.

## E. Mesh Analyzer

Use a high-poly FBX/model:

- [ ] Vertices and triangles display.
- [ ] Read/Write state displays.
- [ ] High-poly mesh produces guidance.
- [ ] A high-poly mesh used by a loaded-scene LODGroup no longer reports the loaded-scene LOD concern.
- [ ] Multiple submeshes/material slots can produce guidance.

## F. Material Analyzer

- [ ] Missing/unsupported shader produces Critical.
- [ ] Transparent material used in an active loaded scene produces Suggestion.
- [ ] Transparent material not used in loaded scene does not produce the transparent-use suggestion.
- [ ] Two genuinely identical material assets can appear as duplicate candidates.
- [ ] Active renderer with many material slots produces Warning.

## G. Lighting Analyzer

- [ ] Fully baked lights are not counted as runtime lights.
- [ ] Realtime and Mixed lights are counted as runtime lights.
- [ ] Fully baked shadow settings are not counted as runtime shadow-light cost.
- [ ] Realtime/ Mixed shadow-casting point or spot lights are reported.

## H. URP Analyzer

- [ ] Active URP asset name and type display.
- [ ] Render Scale is read when supported by the installed URP version.
- [ ] MSAA is read when supported.
- [ ] Shadow Distance is read when supported.
- [ ] Cascade count is read when supported.
- [ ] HDR, Additional Light Shadows, Soft Shadows, Depth Texture, Opaque Texture are read when supported.
- [ ] Low-End profile produces stricter recommendations.
- [ ] If a serialized field is unavailable in a newer URP version, the tool does not use reflection/internal APIs or crash the entire scan.

## I. Robustness

- [ ] Cancel a scan: partial-results warning appears.
- [ ] If one analyzer throws on an unusual asset, other analyzers should continue and the window should show an analyzer-error warning.
- [ ] Enter/exit Play Mode with Domain Reload disabled: the package itself should not throw errors.
- [ ] Close/reopen Unity and open the tool again.

## Send back after testing

If anything fails, send:

1. Unity exact version (for example `6000.0.xx` / `6000.1.xx` / `6000.2.xx` / `6000.3.xx` / `6000.4.xx` / `6000.6.xx`).
2. URP package version.
3. Full Console error/warning text.
4. Screenshot of the optimizer window if the issue is UI/data-related.
5. The type of asset that triggered the issue.
