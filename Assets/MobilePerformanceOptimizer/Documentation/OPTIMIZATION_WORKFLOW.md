# Optimization workflow and verification

The existing analyzer → issue → preview/batch → fix engine → session restore flow is preserved. Existing public fix APIs and enum values remain available. New settings use a reviewable `MPOFixPlan`; scan results do not store mutable custom choices.

## Rescan corrections

- Texture sizing previously compared `Texture2D.width/height` for the active Editor target with a recommendation applied to the selected Android/iOS override. It now reads source dimensions, applies the importer’s NPOT policy, and evaluates the selected platform’s effective maximum. An enabled override takes precedence over defaults. Dimensions displayed for the target are estimates, not claims that a mobile build has been imported.
- Mesh Read/Write detection now uses `ModelImporter.isReadable` for model assets. Native mesh assets retain their existing behavior.
- Audio detection reads the selected platform override when one exists. The load-type fix writes that platform. PCM compression has a separate finding, so resolving load type does not disguise a remaining compression concern.
- Apply and Revert callbacks rescan the original scope/profile and invalidate view/report caches. Native Undo reimports touched dirty importers before notifying the window. Nothing is removed from findings merely because Apply was clicked.
- Bulk action identities include the scene object identity, including an instance ID for unsaved objects whose global IDs are empty.

## Available controls

| Category | Recommended and custom workflow |
| --- | --- |
| Textures | Target maximum size; default maximum/compression/quality; Android and iOS override, maximum, format, compression, quality and Crunch; Read/Write and mipmaps. Format menus use `TextureImporter.IsPlatformTextureFormatValid`. Crunch combinations are checked before mutation. |
| Materials | Instancing when the shader declares support. Standard and supported URP Lit-family materials with the relevant property/keyword can customize emission, its color and GI flags. Disabling emission previews linked black-color and GI changes because shader validation can otherwise restore the emission keyword. |
| Models | Read/Write and mesh compression for actual model importers. |
| Audio | Selected-platform load type, preload, override, mono and background loading; quality for Vorbis content. |
| URP | Findings expose their actual failing settings: render scale, MSAA, shadow distance/cascades, HDR, additional/soft shadows, depth and opaque textures. Recommendations use the analyzer’s existing profile thresholds. |
| Quality | Failing MSAA, shadow-distance and LOD-bias settings on the quality level captured by the scan. |
| Physics | Fixed timestep, with verification tolerance for Unity 6’s rational-time representation. |
| Particles | Maximum particle capacity on eligible saved-scene objects. Independent collision, lighting and other findings remain visible. Scene changes are marked dirty and use normal scene saving. |
| Build flags | Existing reviewed/revertible release-flag fix remains available. |

## Usage

Open a result’s preview, choose Recommended or Custom, and select the settings to change. Current, recommended and selected values are displayed separately. Optional settings initially keep their current values. Custom values that still exceed profile guidance legitimately remain findings after rescan.

The existing batch window lets you configure each action. “Copy First Selected Settings” copies choices to selected actions in the same category; unsupported properties or invalid values cause an explicit skip. Each row identifies its target and has a full configuration/preview. Canceling configuration preserves the previously selected choices.

Before applying, the batch calculates intended changes. Each action validates the live target, value compatibility and preview baseline, captures a persistent restore record, records Undo, applies its selected changes, persists/reimports once, and re-reads every requested value. Unchanged actions skip without reimport. A failed action attempts to restore its original object state; a rollback failure is reported and its restore data remains available. Batch completion reports Applied / Skipped / Failed and logs full reasons. Closing a running batch reports partial progress and leaves session restoration available.

The batch is not an all-or-nothing transaction across the project: successfully verified earlier actions remain applied when a later action fails. Restore records cover each action. Review-required changes always go through preview.

## Restore behavior

“Revert Last Fix Session” restores changed settings in reverse action order, verifies them, and preserves unrelated settings. If a changed setting was edited again after Apply, restoration reports a conflict rather than overwriting that later edit. Failed restore entries remain available for retry. Original snapshot formats remain readable. Backup persistence failures stop new changes instead of silently losing restoration support.

Native Undo is registered for editable assets, importers, project settings and particle objects. Build flags retain the existing session-restore mechanism. Particle changes require a saved scene to establish a persistent restore identity; scene saving remains under the user’s control.

## Intentionally manual

Shader replacement, arbitrary keywords/properties, material merging/render-queue conversion, mesh simplification/bone removal, audio PCM format selection, scene/light/UI count reductions, collision redesign and mipmap-streaming prerequisites remain human decisions. These require content/runtime knowledge or coordinated edits beyond an individual validated setting. No destructive geometry, shader, prefab or content conversion was added.

The cooperative scanner has no per-asset result replacement mechanism. Post-Apply uses a reliable scan of the original scope instead of introducing a second incremental cache. Users can still request a full project scan.

## Validation and release checks

Development tests are in `Assets/MPO_InternalTests`, outside the commercial package. See that folder’s README for execution instructions and the final test report. The suite covers selected-platform detection, recommended/custom persistence, independent findings, repeat scans, idempotent Apply, compatible/incompatible bulk actions, combined settings, native Undo, session restoration, URP, quality, physics, particles and automatic window refresh.

Before a commercial release, manually verify:

- Preview layout, scrolling, keyboard interaction, Cancel, linked material fields, and a large batch in the Unity Editor.
- Switching the active build target to Android and iOS, importing representative content, and checking actual target texture appearance and audio playback.
- Prefab-instance particle overrides, scene saving, domain reload and Undo/Redo with representative project content.
- Runtime systems that require texture/model Read/Write, particle peak capacity, physics timing, and effects depending on URP depth/opaque textures or HDR.
- Device performance, visual/audio quality and player builds. Editor regression results do not substitute for device profiling.

Unity API reference: [platform texture format validation](https://docs.unity.cn/6000.2/Documentation/ScriptReference/TextureImporter.IsPlatformTextureFormatValid.html).
