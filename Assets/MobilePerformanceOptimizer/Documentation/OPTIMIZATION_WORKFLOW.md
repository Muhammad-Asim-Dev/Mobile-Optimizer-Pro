# Optimization Workflow

The release workflow is intentionally focused:

**Scan → Category → Recommended or Category Custom Values → Preview → Apply → Verify → Re-scan**

## Recommended mode

Uses values produced by the active Android/iOS + device-tier analyzer rules.

## Custom for this category

On the Problems page, select a category and switch **Category Mode** to **Custom for this category**. Set a value once and the optimizer reuses it for every compatible fix in that category.

Compatibility is validated again immediately before Apply. Unsupported settings are never force-written.

## Apply verification

Every supported change follows the same rule:

1. Re-read the target/importer
2. Validate the selected value
3. Capture restorable state
4. Apply using Unity APIs
5. Save/reimport when required
6. Re-read the actual value
7. Verify persistence
8. Refresh the original scan scope

## Batch behavior

Category batch fixing is incremental: selected actions are processed between Editor updates so large importer batches do not run as one uninterrupted loop.
