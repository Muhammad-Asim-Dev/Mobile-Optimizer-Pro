# Milestone 4.6 — Material Coverage + Quick Fix All

Version: `0.4.6`

## Why this patch exists

Milestone 4.5 could legitimately show an empty Materials category in projects where no loaded-scene material triggered the limited checks. It also exposed only Safe Fix All while most useful mobile changes were intentionally classified Review Required.

## Material analyzer changes

- Material checks now run across the selected scan scope, not only loaded-scene usage.
- Transparent materials are surfaced as review findings even when not currently loaded.
- Common Unity/URP opaque materials with GPU Instancing disabled are surfaced as review candidates.
- Repeated custom materials in loaded scenes can also be surfaced as GPU Instancing candidates.
- Broken shader diagnostics stay limited to loaded-scene usage to avoid forcing expensive third-party shader compilation.
- Duplicate-material signatures remain limited to loaded-scene usage for scan responsiveness.

## New automatic review fixes

- Set the selected mobile platform max texture size to the analyzer recommendation (for example Android 4096 -> 1024) without changing the source/default desktop texture size.
- Enable `Material.enableInstancing` for reviewed material candidates.
- Existing texture/mesh Read-Write, audio streaming and build flag fixes remain available.

Every automatic change is captured by the optimization session and can be reverted. Texture-size and GPU-instancing changes are Review Required, not Safe, because visual/runtime benefit depends on project usage.

## UX changes

- Problems page now shows a visible **Fix All Recommended...** action whenever automatic fixes are available.
- Fixes page uses **Fix All Recommended...** for review-required actions instead of the less obvious Review All wording.
- The batch window still previews all proposed changes and lets the user uncheck individual items before applying.

## Validation focus

After import, test Materials on the same project that showed zero material findings in v0.4.5, then verify:

1. Materials Scanned is non-zero when material assets exist in scope.
2. Transparent/GPU-instancing candidate findings appear where applicable.
3. Fix All Recommended opens a batch preview.
4. A low-end oversized texture can preview/apply a platform max-size recommendation.
5. GPU Instancing can preview/apply on a supported/reviewed material candidate.
6. Revert Last Session restores the previous platform override/max size and material instancing value.
