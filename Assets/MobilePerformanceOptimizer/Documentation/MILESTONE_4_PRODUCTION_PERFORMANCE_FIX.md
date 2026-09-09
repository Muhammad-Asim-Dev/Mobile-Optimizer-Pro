# Milestone 4.2 — Production Performance / Editor Responsiveness Fix

Version: `0.4.0-m4.2-performance`

## Why the previous window could feel frozen

The slowdown was not caused by `EditorWindow` as a concept. The expensive work came from two places:

1. Project analyzers executed long loops and Unity `AssetDatabase` / scene API calls synchronously on the editor main thread.
2. IMGUI rebuilt and drew every issue/fix card on Layout, Repaint, mouse and scroll events. Large scans could therefore make simple scrolling or clicking expensive.

Moving the same implementation into a custom Inspector would not solve either problem. Inspectors also execute Unity editor GUI and most Unity object/asset APIs on the main thread.

## Production architecture used now

The tool remains a dedicated Editor Window, but the heavy work is split into two independent layers:

### 1. Cooperative project scanner

`MPOScanRunner` is driven by `EditorApplication.update` and uses an approximately 4 ms MPO work budget per editor update. Heavy analyzers implement `IMPOIncrementalAnalyzer` and yield between small chunks of assets/components.

This keeps Unity input, repaint and other editor work flowing between chunks while still keeping `AssetDatabase` and `UnityEngine.Object` access on the Unity main thread where it is safe.

Incremental analyzers:

- Scene
- Texture
- Mesh
- Material
- Lighting
- Particle
- Physics
- UI
- Audio

Small settings analyzers remain single-step because their work is short:

- URP
- Build Settings
- Quality Settings

The runner also:

- pauses safely while Unity is compiling scripts or importing/updating assets;
- supports cancellation without discarding already collected partial results;
- continues to the next analyzer if one analyzer throws;
- retains per-asset/per-component recoverable-error handling from the resilient scan patch;
- cancels if the editor enters Play Mode;
- stops hidden work if the optimizer window is closed.

### 2. Cached and paged IMGUI results

The UI no longer draws the full result set on every IMGUI event.

- Issues: 20 cards per page.
- Fixable findings: 30 rows per page.
- Active issue counts are cached.
- Category counts are cached.
- Search/filter/sort results are cached until a filter or scan result actually changes.
- Report snapshot generation is cached instead of rebuilding on every repaint.
- Common GUI styles are cached instead of allocating new styles repeatedly.
- Ignore-rule and ignore-asset checks use hash lookups.

This is intentionally pagination rather than a huge nested scroll view. It gives predictable editor cost even when a project produces thousands of findings.

## Analyzer hot-path reductions

Additional high-cost paths were reduced:

- Shader compiler diagnostics are limited to materials actually used in loaded scenes.
- Duplicate-material signature work is limited to loaded-scene materials.
- Texture/audio/mesh/material issue records avoid retaining unnecessary Unity asset object references; Ping loads the asset only when requested.
- Mesh model paths yield between files and between sub-assets so one large model does not cause a long uninterrupted managed loop.

## Important Unity limitation

No editor extension can safely pre-empt a single Unity native/API call after that call has started. For example, `AssetDatabase.FindAssets`, importing a corrupt asset, or `LoadAllAssetsAtPath` on an exceptionally large model can itself take noticeable time inside Unity.

The optimizer now yields immediately before/after expensive work and never intentionally performs thousands of operations in one GUI event. A brief stall caused by one Unity native call may still occur, but repeated long freezes caused by MPO loops/rendering should no longer occur.

## Performance acceptance checklist

Test in a representative large project:

1. Open **Tools > Mobile Performance Optimizer > Open Optimizer**.
2. Start **Analyze Project**.
3. While scanning, move/resize the window, change tabs and scroll existing results.
4. Verify the progress panel updates without a modal progress window.
5. Press **Cancel Scan** and verify cancellation completes after the current safe chunk.
6. Complete a full scan with hundreds/thousands of findings.
7. Scroll the Issues page and verify only 20 issue cards are rendered at once.
8. Search, filter and sort repeatedly and verify scrolling remains responsive after the result is cached.
9. Open **Fixes & Ignore** and verify only 30 fix rows are rendered at once.
10. Open **Reports & History** repeatedly and verify report data is not rebuilt on every repaint.
11. Keep the known broken third-party shader in the project and verify the remaining analyzers continue.
12. Check the Console for MPO compile errors, exceptions, or obsolete API warnings.

## Release rule

Do not merge this folder over an older MPO folder for final validation. Remove the old `Assets/MobilePerformanceOptimizer` directory and copy/import this version cleanly so deleted/renamed scripts cannot remain behind.
