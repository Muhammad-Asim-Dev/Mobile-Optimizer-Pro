using System;

namespace MobilePerformanceOptimizer
{
    public static class MPOAssetScope
    {
        public static bool ShouldScan(string assetPath, MPOScanScope scope = null)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            string normalized = assetPath.Replace('\\', '/');

            if (normalized.StartsWith(MPOConstants.ProductRoot, StringComparison.OrdinalIgnoreCase))
                return false;

            if (normalized.StartsWith("Assets/Editor/", StringComparison.OrdinalIgnoreCase) ||
                normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            if (normalized.StartsWith("Assets/Gizmos/", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                !normalized.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                return false;

            return scope == null || scope.ShouldScanAsset(normalized);
        }

        public static string[] GetFindRoots(MPOScanScope scope)
        {
            if (scope != null && scope.SearchFolders != null && scope.SearchFolders.Length > 0)
                return scope.SearchFolders;
            return new[] { "Assets" };
        }
    }
}
