# Mobile Performance Optimizer — Milestone 4

Version: `0.4.0-m4`

Milestone 4 builds on Milestone 3 and focuses on **analyzer intelligence, lower-noise recommendations, estimated impact guidance, and professional Editor UX**. The architecture and fix-safety model remain unchanged.

## Main additions

### 1. Estimated Performance Impact

Every finding can now describe estimated impact across:

- CPU
- GPU
- Memory
- Build Size
- Thermal / sustained-performance risk

Impact values are `None`, `Low`, `Medium`, or `High`.

These values are **static heuristic estimates**, not profiler measurements and not promised FPS gains. They are intended to help users decide what to profile first.

Impact data is visible in:

- Overview impact map
- Issue cards
- Compact top-issue rows
- Fix preview window
- HTML reports
- JSON reports
- CSV reports

### 2. Smarter Texture Analysis

Texture checks now consider:

- Device-profile texture-size guidance
- Texture role heuristics (UI, character, environment, other)
- Read/Write memory risk
- UI/Sprite mipmap usage
- Android/iOS platform override presence when an oversized texture is found
- Approximate uncompressed memory upper bound
- Platform-specific compression guidance

Large textures are no longer promoted to Critical solely because their dimension is above the profile limit; severity also considers oversize ratio, estimated memory footprint and selected device tier.

### 3. Smarter Mesh Analysis

Mesh checks now distinguish between project assets and meshes that are actually used by active renderers in loaded scenes.

Improvements include:

- High-poly severity is stronger when the mesh is active in loaded scenes
- Missing-LOD guidance is only raised when a high-detail mesh is actively used and not found in a loaded LODGroup
- Skinned-mesh bone guidance
- Blend-shape guidance
- Submesh/material-slot awareness
- Read/Write review remains available through the existing review-required fix

This reduces the previous false positive where an unused high-poly project asset could be treated like an active scene LOD problem.

### 4. Smarter Material Analysis

Material analysis now includes:

- Loaded-scene material usage counts
- Transparent-material severity based partly on repeated loaded-scene usage
- Duplicate-material candidate grouping
- Duplicate groups are recommendation-only; the tool never deletes or merges materials
- Active renderer material-slot guidance

### 5. Lighting Accuracy

Lighting checks continue to keep fully baked lights out of runtime-light cost.

Runtime metrics now separate:

- Realtime
- Mixed
- Baked
- Runtime shadow lights
- Point shadow lights
- Spot shadow lights
- Directional shadow lights

Point/Spot runtime-shadow guidance is device-tier aware.

### 6. Particle Cost Heuristics

Particle analysis no longer relies only on `maxParticles`.

The heuristic now considers:

- Max particle capacity
- Approximate rate-over-time × lifetime population
- Collision
- Trails
- Particle lights
- Sub emitters
- Mesh rendering
- Active particle-system count

The result is still a static estimate; representative effects should be validated on device.

### 7. UI False-Positive Reduction

UI analysis is more conservative about Raycast Target warnings.

The raycast rule now requires a combination of:

- High total raycast-target count
- High likely-non-interactive count
- Meaningful likely-non-interactive ratio

Additional UI context includes:

- Nested LayoutGroups
- ContentSizeFitters inside layout hierarchies
- UI mesh effects
- Mask count

Raw component count by itself is less likely to generate a strong warning.

### 8. Physics Context Improvements

Physics analysis now includes:

- Dynamic body count
- Active MeshCollider count
- Non-convex MeshCollider count
- MeshColliders attached to dynamic/non-kinematic Rigidbody objects
- Fixed timestep frequency

### 9. URP / Quality Guidance

URP findings now aggregate their likely GPU/memory impact and include selected-platform guidance.

The URP analyzer continues to use serialized public Editor data and intentionally avoids Unity internal/reflection API hacks.

Quality guidance continues to review:

- MSAA
- Shadow Distance
- LOD Bias
- Mipmap streaming

Severity is more profile-aware when multiple expensive settings are active together.

### 10. Professional Issues UX

The Issues tab now supports:

- Search
- Category filter
- Severity filters
- Fixable-only filter
- High-impact-only filter
- Sort by Severity
- Sort by Estimated Impact
- Sort by Category
- Sort by Asset Name

Issue cards show the five impact dimensions directly.

### 11. Dashboard Improvements

The Overview page now contains an **Estimated Impact Map** showing how many active findings have High CPU, GPU, Memory, Build Size or Thermal impact.

Top-issue prioritization now considers:

1. Severity
2. Estimated impact score
3. Penalty

Category metrics are rendered in rows instead of one unbounded horizontal line, improving readability in categories with many metrics.

### 12. Scan Progress

The Unity cancelable progress dialog now shows the current analyzer position, for example:

`Analyzer 4/12 — Material Analyzer`

Existing per-analyzer progress is preserved.

## Safety model

Milestone 4 does not broaden automatic destructive changes.

### Safe

- Disable Development Build / Script Debugging / Autoconnect Profiler / Deep Profiling flags

### Review Required

- Texture Read/Write
- Model Read/Write
- Long-audio Streaming

### Manual

- Geometry reduction
- LOD creation
- Collider replacement
- Lighting deletion/change
- Particle visual changes
- Material merging/deletion
- UI hierarchy changes
- URP quality changes

## Important interpretation rule

`High GPU Impact` means **the static finding is likely worth GPU profiling first**. It does not mean the tool measured a high GPU frame time.

The final decision should always be validated with Unity Profiler, GPU tools and representative Android/iOS devices.

## Development status after Milestone 4

Milestone 4 is the final planned feature-development milestone before the first Asset Store release. The next phase is full Unity 6 module validation using `MILESTONE_4_TEST_CHECKLIST.md`, followed by Asset Store packaging/polish (documentation review, sample/demo content, screenshots/video, changelog, listing copy and clean-project release validation). New feature work should be added only if module testing reveals a real gap.

## Resilient Scan Hotfix (m4.1)

Milestone 4 now includes fail-open scanning for recoverable asset/component/importer errors. A bad asset is skipped and reported instead of aborting subsequent project analysis. See `MILESTONE_4_RESILIENT_SCAN_FIX.md`.
