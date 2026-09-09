# Unity 6 Compatibility Fix — Milestone 2 v0.2.0

Baseline remains `MobilePerformanceOptimizer_Milestone2_v0.2.0-m2`.
No architecture redesign and no v0.2.1 changes are included.

Fixed Unity 6 compile/API issues:

- `ParticleAnalyzer.cs`: removed invalid `ParticleSystem.enabled` access and uses active hierarchy state.
- `AudioAnalyzer.cs`: uses `AudioImporterSampleSettings.preloadAudioData` instead of obsolete `AudioImporter.preloadAudioData`.
- `MPOFixEngine.cs`: uses sample settings for audio preload and explicitly stores `sampleRateOverride` as `int`.
- `MPOFixSession.cs`: restores `sampleRateOverride` with an explicit `uint` cast and restores audio preload through sample settings.
- `MPOIgnoreStore.cs`: added the missing `UnityEngine` namespace for `JsonUtility`.
- `MobilePerformanceOptimizerWindow.cs`: replaced the invalid Unity 6 `EditorGUILayout.Foldout` layout-option call with a fixed-width rect and `EditorGUI.Foldout`.

Recommended validation after import:

1. Delete/replace the previous `Assets/MobilePerformanceOptimizer` folder.
2. Import this package/folder into a Unity 6.x URP project.
3. Let Unity finish recompiling.
4. Confirm the Console has no Mobile Performance Optimizer compile errors or obsolete API warnings.
5. Open `Tools > Mobile Performance Optimizer > Open Optimizer` and run the Milestone 2 test checklist.
