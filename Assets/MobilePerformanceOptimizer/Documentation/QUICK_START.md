# Quick Start

1. Copy `Assets/MobilePerformanceOptimizer` into your Unity project.
2. Let Unity finish compiling.
3. Open **Tools > Mobile Performance Optimizer > Open Optimizer**.
4. Choose **Android** or **iOS** and the target device tier.
5. Choose a scan scope. Start with **Current Scene** or **Selected Folder** for a fast focused check, or **Full Project** for a complete pass.
6. Click **Start Scan**.
7. Open **Problems** and select a category such as Textures or Materials.
8. Use the recommended values, or switch to **Custom for this category** and set one batch value for compatible assets.
9. Click **Fix All <Category>...**, review the selected assets, then apply.
10. The optimizer saves/reimports, verifies the setting, and refreshes the same scan scope.
11. Use **Fixes > Revert Last Session** if you need to restore the last supported batch.
12. Export a report from **Reports** when required.

## Recommended first pass

For most mobile projects, review categories in this order:

1. Textures
2. Materials
3. Meshes / Models
4. Audio
5. URP
6. Quality
7. Build Settings

Always profile representative gameplay with Unity Profiler and real devices before shipping.
