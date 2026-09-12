# Change report: Apply/rescan and custom optimization

## Root causes corrected

1. Texture detection used dimensions imported for the active Editor platform while Apply changed the selected mobile platform. Detection now uses source dimensions and the selected target’s effective importer maximum.
2. Audio checks ignored platform overrides and combined independently resolvable load-type and PCM concerns. Target settings and independent findings are now respected.
3. Model Read/Write checks could disagree with importer configuration. Model importers are now authoritative.
4. Apply callbacks refreshed cached views without a fresh analysis. They now invalidate caches and rescan the captured scope/profile; Undo reimports touched dirty importers first.
5. Material emission keyword-only edits could be rebuilt by shader validation. Disabling emission now previews and captures its linked color/GI changes, with both native Undo and session restoration tested.
6. Bulk actions on unsaved scene objects could share an empty global identity. Action keys distinguish instances.

New settings also handle Unity 6 URP enum properties and rational-time rounding during verification.

## Files changed

Under `Assets/MobilePerformanceOptimizer/Editor`:

- `Analyzers/TextureAnalyzer.cs`
- `Analyzers/AudioAnalyzer.cs`
- `Analyzers/MeshAnalyzer.cs`
- `Analyzers/MaterialAnalyzer.cs`
- `Analyzers/URPAnalyzer.cs`
- `Analyzers/QualitySettingsAnalyzer.cs`
- `Analyzers/PhysicsAnalyzer.cs`
- `Analyzers/ParticleAnalyzer.cs`
- `Core/MPOEnums.cs`
- `Core/MPOIssue.cs`
- `Fixes/MPOFixEngine.cs`
- `Fixes/MPOFixSession.cs`
- `Fixes/MPOFixPreviewWindow.cs`
- `Fixes/MPOBatchFixWindow.cs`
- `UI/MobilePerformanceOptimizerWindow.cs`
- New: `Fixes/MPOFixPlan.cs`, `Fixes/MPOFixPlanUI.cs`, `Fixes/MPOFixUndo.cs`, with Unity metadata.

Additional files:

- `Assets/MobilePerformanceOptimizer/Documentation/OPTIMIZATION_WORKFLOW.md`, with metadata.
- `Assets/MPO_InternalTests/Editor/MPOApplyRegressionTests.cs` and `MPO.InternalTests.Editor.asmdef`, with metadata.
- Internal test README, this report, and folder metadata. Internal QA remains outside the commercial package.

## Features

The existing recommended workflow remains available. Single and batch previews support current/recommended/selected values, optional custom choices, compatible selection copying and cancellation without changing the prior configuration. Changes are validated against live objects, persisted/reimported, and verified. Batches report Applied/Skipped/Failed; unchanged actions avoid a reimport. Failed actions attempt rollback, and restoration detects conflicts with later edits.

Controls cover textures, eligible material instancing/emission, model import settings, audio loading/quality, failing URP and quality settings, physics timestep and particle capacity. The workflow document contains the detailed control matrix and safety semantics.

## Tests

Environment: Unity 6000.0.68f1, URP 17.0.4, isolated project at `.utmp/UnityValidation`.

- All relevant EditMode tests: **22 passed, 0 failed** (`.utmp/editmode-final.xml`).
- Complete optimizer suite rerun: **22 passed, 0 failed** (`.utmp/optimizer-regressions-final.xml`).
- Package C# compilation against installed Unity assemblies: **0 errors**. Seven CS0649 warnings concern preserved legacy snapshot fields and pre-existing UI fields.
- `git diff --check`: passed.

Coverage includes Android/iOS detect → recommended Apply → re-read → rescan → restore; custom sizes and formats; combined texture settings; independent remaining findings; repeat scans; idempotent Apply; two compatible bulk targets and an incompatible target; stale-preview protection; unrelated-edit preservation; model/audio importer checks; material native Undo and session restore; URP recommended/custom/restore; quality custom values; physics rounding; particle capacity with remaining collision concerns; distinct scene batch identities; importer native Undo; and actual window callback rescan.

## Manual validation still required

Review preview/batch layout and interaction in the Editor; prefab-instance particle overrides and scene saving; domain reload and Undo/Redo with real content; switching active targets to Android/iOS; actual mobile imports/player builds; device visual/audio quality and profiling. No Android/iOS player builds or physical-device measurements were performed.

Shader conversion, arbitrary material properties/keywords, geometry reduction, scene/light/UI count reductions, PCM codec selection, and coordinated streaming/collision changes remain manual where content knowledge is required. The existing scanner supports reliable scope rescans; a separate targeted per-asset cache was not introduced.

