# FAQ

## Does this replace Unity Profiler?
No. It analyzes project/import/settings problems and suggests or applies reviewable fixes. Runtime CPU/GPU behavior still needs Unity Profiler and device testing.

## Does it modify gameplay code?
No. The focused release targets supported Editor/import/pipeline/build settings.

## Can I fix everything at once?
No global cross-category Fix All is provided. Fixes are category-specific so changes remain understandable and safer.

## Can I use my own texture size instead of the recommendation?
Yes. Select Textures, choose **Custom for this category**, and set the compatible batch value once.

## Are custom values forced on every asset?
No. Each target is validated before Apply. Incompatible values/assets are skipped.

## Can I undo changes?
The optimizer captures supported original values for the latest fix session and provides Revert. Unity native Undo is also used where technically appropriate.

## Does the package add runtime code to my build?
No. The package is Editor-only.

## Which render pipeline is targeted?
Version 1.0.0 targets Unity 6 URP mobile workflows.
