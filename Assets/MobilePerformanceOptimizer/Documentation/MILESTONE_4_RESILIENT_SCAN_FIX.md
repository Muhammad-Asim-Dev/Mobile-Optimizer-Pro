# Milestone 4 Resilient Scan Fix

Build: `0.4.0-m4.1-resilient`

## Goal

A broken project asset must not abort the remaining Mobile Performance Optimizer scan.

The scanner now uses two protection levels:

1. **Per-item protection** — malformed/unreadable assets and scene components are skipped while the current analyzer continues.
2. **Per-analyzer protection** — if an analyzer still hits an unexpected exception, the runner records it and continues with the next analyzer.

## Covered recoverable scenarios

- Broken or missing material shaders
- Shader compiler errors in third-party shaders
- Shader compilation already in progress
- Missing/destroyed scene components
- Scene hierarchy read failures
- Assets deleted or changed while scanning
- AssetDatabase load/importer failures
- Unreadable texture/audio/model assets
- Mesh sub-asset or topology read failures
- Invalid LODGroup renderer references
- Particle module read failures
- Physics component read failures
- UI hierarchy/custom behaviour read failures
- URP serialized property/version mismatches
- Quality/build-setting property read failures
- Progress UI failures
- Report/history snapshot failure after a completed scan

Recoverable warnings are stored in the scan result and shown in the Editor UI and exported reports. The warning list is capped for memory/readability while the total skipped count remains accurate.

## Shader handling change

The Material Analyzer no longer uses `Shader.isSupported` for project material validation. It uses Unity Editor shader compiler diagnostics (`ShaderUtil.ShaderHasError` / `GetShaderMessages`) and avoids shader validation while Unity is already compiling shaders. This reduces the chance that scanning a broken third-party shader blocks the rest of the analysis.

## Intentional limits

The scanner cannot safely continue through catastrophic Editor/process failures such as a Unity crash, process termination, or a true out-of-memory condition. It also cannot interrupt a Unity native API call that itself becomes permanently blocked. These are outside the recoverable asset-error layer.
