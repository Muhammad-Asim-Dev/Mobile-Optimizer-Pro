# Mobile Performance Optimizer — Milestone 1

## Scope

Milestone 1 is **analysis-only**. It does not modify project assets, import settings, URP settings, scenes, or quality settings.

Implemented analyzers:

- Scene Analyzer (loaded scenes; performance thresholds use active/enabled renderers and cameras)
- Texture Analyzer
- Mesh Analyzer
- Material Analyzer
- Lighting Analyzer (runtime-cost checks distinguish baked vs realtime/mixed lights)
- URP Analyzer
- Android / iOS target profiles
- Low-End / Mid-Range / High-End device tiers
- Static performance score
- Critical / Warning / Suggestion severity system
- Search and filters
- Ping affected asset/object

## Supported target

- Unity 6.x
- Universal Render Pipeline (URP)
- Android and iOS advisory profiles

The package intentionally has no direct compile-time dependency on the URP assembly. It reads the active RenderPipelineAsset through public Unity APIs and uses SerializedObject for known serialized URP asset fields. Unknown versions fail gracefully instead of using reflection/internal Unity APIs.

## Installation

Copy this folder into your project exactly as:

`Assets/MobilePerformanceOptimizer/`

Wait for Unity to compile.

Open:

`Tools > Mobile Performance Optimizer > Open Optimizer`

## First test

1. Open a normal gameplay scene.
2. Select `Android` and `MidRange`.
3. Click **Analyze Project**.
4. Confirm the scan completes without Console errors.
5. Review the score and category cards.
6. Click **Ping** on texture/mesh/material issues and confirm Unity selects the correct asset.

## Deliberately bad test scene

Create or use a scene containing several of these:

- 4096 texture
- Texture Read/Write enabled
- No Android platform texture override
- High-poly mesh without a loaded-scene LODGroup
- Multiple realtime shadow-casting lights
- Several cameras
- URP asset with high shadow distance
- Low-End target with HDR enabled

Run the scan and verify the relevant warnings appear.

## Important interpretation notes

- The score is an **advisory static score**, not an FPS estimate.
- Triangle thresholds are guidelines and can be validly exceeded depending on visibility, batching, GPU, shaders, resolution, and game design.
- Texture memory shown in Milestone 1 is an **uncompressed upper-bound approximation across scanned project textures**, not exact runtime GPU/resident memory.
- “No LOD” means no LODGroup use was found for the mesh in the **currently loaded scenes**. It does not claim the mesh is never used by an LOD elsewhere.
- Transparent materials are suggestions, not automatically considered errors.
- Assets inside `Editor/`, `Gizmos/`, and the optimizer's own root folder are excluded from project-asset scans.
- Actual performance must be validated with Unity Profiler/GPU profiling on representative target devices.

## Safety

Milestone 1 performs no automatic fixes. This is intentional so the analysis model can be validated before Milestone 2 introduces previewed and reversible fixes.

## Folder structure

```
Assets/MobilePerformanceOptimizer/
├── Editor/
│   ├── Analyzers/
│   ├── Core/
│   ├── Scoring/
│   ├── UI/
│   └── MobilePerformanceOptimizer.Editor.asmdef
└── Documentation/
    └── MILESTONE_1_README.md
```

## Known Milestone 1 boundaries

Not included yet:

- Particle Analyzer
- Physics Analyzer
- UI/Canvas Analyzer
- Audio Analyzer
- Build Settings Analyzer
- Quality Settings Analyzer
- Safe Auto Fix
- Preview/Undo/Ignore
- Before/After report
- HTML export

These belong to Milestones 2 and 3.
