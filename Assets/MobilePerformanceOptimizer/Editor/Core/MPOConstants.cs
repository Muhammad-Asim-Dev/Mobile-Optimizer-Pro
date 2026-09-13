namespace MobilePerformanceOptimizer
{
    public static class MPOConstants
    {
        public const string ProductName = "Mobile Performance Optimizer";
        public const string Version = "1.0.1";
        public const string ProductRoot = "Assets/MobilePerformanceOptimizer/";
        public const string SessionFile = "Library/MobilePerformanceOptimizer/LastFixSession.json";
        public const string HistoryFile = "Library/MobilePerformanceOptimizer/ScanHistory.json";

        // Focused Asset Store release: ship only the highest-value mobile optimization workflow.
        // Advanced systems stay in the codebase so they can be re-enabled later without another rewrite.
        public const bool FocusedRelease = true;
        public static readonly bool EnableReports = true;
        public static readonly bool EnableScanHistory = false;
        public static readonly bool EnableIgnoreUi = false;
        public static readonly bool EnableAdvancedCustomSettings = false;
        public static readonly bool EnableAdvancedAdvisoryAnalyzers = false;

        public static bool IsCoreReleaseCategory(MPOCategory category)
        {
            switch (category)
            {
                case MPOCategory.Textures:
                case MPOCategory.Materials:
                case MPOCategory.Meshes:
                case MPOCategory.Audio:
                case MPOCategory.URP:
                case MPOCategory.Quality:
                case MPOCategory.Build:
                    return true;
                default:
                    return EnableAdvancedAdvisoryAnalyzers;
            }
        }

        public static string FriendlyCategoryName(MPOCategory category)
        {
            switch (category)
            {
                case MPOCategory.Textures: return "Textures";
                case MPOCategory.Materials: return "Materials";
                case MPOCategory.Meshes: return "Meshes / Models";
                case MPOCategory.Audio: return "Audio";
                case MPOCategory.URP: return "URP";
                case MPOCategory.Quality: return "Quality";
                case MPOCategory.Build: return "Build Settings";
                default: return category.ToString();
            }
        }
    }
}
