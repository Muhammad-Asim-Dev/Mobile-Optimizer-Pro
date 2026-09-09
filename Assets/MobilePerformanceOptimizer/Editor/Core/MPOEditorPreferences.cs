using UnityEditor;

namespace MobilePerformanceOptimizer
{
    public static class MPOEditorPreferences
    {
        private const string PlatformKey = "MPO.TargetPlatform";
        private const string TierKey = "MPO.DeviceTier";
        private const string ScopeKey = "MPO.ScanScope";
        private const string FolderKey = "MPO.SelectedFolder";

        public static MPOTargetPlatform TargetPlatform
        {
            get => (MPOTargetPlatform)EditorPrefs.GetInt(PlatformKey, (int)MPOTargetPlatform.Android);
            set => EditorPrefs.SetInt(PlatformKey, (int)value);
        }

        public static MPODeviceTier DeviceTier
        {
            get => (MPODeviceTier)EditorPrefs.GetInt(TierKey, (int)MPODeviceTier.MidRange);
            set => EditorPrefs.SetInt(TierKey, (int)value);
        }

        public static MPOScanScopeMode ScanScope
        {
            get => (MPOScanScopeMode)EditorPrefs.GetInt(ScopeKey, (int)MPOScanScopeMode.FullProject);
            set => EditorPrefs.SetInt(ScopeKey, (int)value);
        }

        public static string SelectedFolder
        {
            get => EditorPrefs.GetString(FolderKey, "Assets");
            set => EditorPrefs.SetString(FolderKey, value ?? string.Empty);
        }
    }
}
