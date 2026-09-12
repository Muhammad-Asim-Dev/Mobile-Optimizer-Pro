# User Guide

## Scan page

Choose the target platform, device tier and scan scope. Device tiers change how aggressive the recommendations are. A low-end profile intentionally recommends more conservative mobile settings than a high-end profile.

### Scan scopes

**Full Project** scans the complete supported asset scope.

**Build Scenes** focuses on scenes included in Build Settings and their supported scan context.

**Current Scene** is useful while optimizing one level or gameplay scene.

**Selected Folder** is useful for an art/audio folder without scanning the whole project.

**Selected Assets** is useful for a small manual selection in the Project window.

## Problems page

Choose a category tab first. The category fix panel and batch values update to match that category.

Each finding explains why the setting matters and exposes a reviewable fix when the optimizer can change it safely.

### Recommended values

Recommended mode uses values calculated for the current platform and device tier.

### Custom for this category

Switch **Category Mode** to **Custom for this category** to set reusable values once for every compatible fix in that category. Unsupported assets are not force-written.

Examples:

- Textures: one mobile Max Size for compatible oversized textures
- Materials: GPU Instancing for compatible materials
- Meshes / Models: Read/Write or Mesh Compression where supported
- Audio: Load Type, Preload and compatible importer settings
- URP / Quality: supported detected numeric/boolean settings

## Fixes page

Fixes are grouped by category. There is intentionally no project-wide Fix All across unrelated categories.

Before Apply, the batch window lists the affected items. Use Select All/None and review the category values before confirming.

## Apply and verification

For supported fixes the optimizer:

1. Reads the current Unity value
2. Validates the requested value
3. Captures restorable state
4. Applies through Unity APIs
5. Saves/reimports when required
6. Reads the value again
7. Verifies persistence
8. Refreshes the original scan scope

If the requested value is incompatible, the item is skipped or reported instead of being force-written.

## Revert

The last supported fix session can be restored from the Fixes page. Revert is conflict-aware so later manual edits are not silently overwritten when the stored state no longer matches safely.

## Reports

HTML is intended for readable sharing, CSV for spreadsheets, and JSON for machine-readable workflows. Reports reflect the latest valid scan state.
