# Troubleshooting

## The scan reports skipped or unreadable items

Open **View Diagnostics**. Broken third-party shaders, missing importers or unreadable assets are skipped so the rest of the scan can continue. Fix the underlying project asset if you need that item analyzed.

## A fixed item still appears after re-scan

Open the finding and confirm whether it contains another independent problem. One asset can have multiple valid findings. If the exact fixed setting did not persist, review the Apply result/Console message and verify the importer is writable.

## GPU Instancing is not offered

The material must be an editable main material asset using a shader workflow Unity can safely enable instancing for. Variants, embedded materials, missing shaders and unsupported shader workflows remain manual.

## A batch skips some assets

Category custom values apply only to compatible assets. Different texture types, formats, importers or pipeline objects may reject the same value; those items are skipped rather than force-written.

## The Editor is compiling/importing during a batch

The batch waits for Unity to finish compilation/importing before continuing.

## UI layout looks compressed

The window supports compact layouts, but a wider dock gives the Problems split view more room. Resize the Editor panel if you want the asset list and detail pane visible side-by-side.

## Reports are disabled or stale

Run a fresh scan after changing platform/profile/project state, then export again.
