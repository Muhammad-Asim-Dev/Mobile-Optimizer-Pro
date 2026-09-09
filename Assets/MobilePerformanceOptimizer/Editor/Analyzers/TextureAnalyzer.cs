using System.Collections;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public sealed class TextureAnalyzer : IMPOIncrementalAnalyzer
    {
        public string Name => "Texture Analyzer";
        public MPOCategory Category => MPOCategory.Textures;

        public void Analyze(MPOScanContext context)
        {
            IEnumerator routine = AnalyzeIncremental(context);
            while (routine != null && routine.MoveNext()) { }
        }

        public IEnumerator AnalyzeIncremental(MPOScanContext context)
        {
            MPOCategoryResult result = context.Result.GetOrCreate(Category);
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", MPOAssetScope.GetFindRoots(context.Scope));
            yield return null;

            int scanned = 0;
            int oversized = 0;
            int readable = 0;
            int oversizedWithoutOverride = 0;
            int mipmapConcerns = 0;
            int skipped = 0;
            double rawMemoryMb = 0d;

            for (int i = 0; i < guids.Length; i++)
            {
                if (context.ReportProgress($"{i + 1}/{guids.Length}", guids.Length == 0 ? 1f : i / (float)guids.Length))
                    yield break;

                string path;
                if (!context.TryGet("Texture GUID " + guids[i], () => AssetDatabase.GUIDToAssetPath(guids[i]), out path) ||
                    !MPOAssetScope.ShouldScan(path, context.Scope))
                {
                    if ((i & 7) == 7)
                        yield return null;
                    continue;
                }

                bool ok = context.TryExecute("Texture asset: " + path, () =>
                {
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (importer == null || texture == null)
                    {
                        context.RecordRecoverableWarning(path, "Texture importer or Texture2D could not be loaded and was skipped.");
                        return;
                    }

                    scanned++;
                    int width = Mathf.Max(1, texture.width);
                    int height = Mathf.Max(1, texture.height);
                    int largestSide = Mathf.Max(width, height);
                    int recommendedSize = context.Profile.GetRecommendedTextureSize(path, importer);
                    bool uiLike = importer.textureType == TextureImporterType.Sprite || IsUiLike(path);
                    bool normalMap = importer.textureType == TextureImporterType.NormalMap;

                    TextureImporterPlatformSettings platformSettings = importer.GetPlatformTextureSettings(context.Profile.PlatformTextureSettingsName);
                    bool hasPlatformOverride = platformSettings.overridden;
                    int platformMaxSize = hasPlatformOverride ? platformSettings.maxTextureSize : importer.maxTextureSize;

                    bool isOversized = largestSide > recommendedSize;
                    bool isReadable = importer.isReadable;
                    bool mipmapConcern = uiLike && importer.mipmapEnabled;
                    bool missingUsefulOverride = isOversized && !hasPlatformOverride;

                    if (isOversized) oversized++;
                    if (isReadable) readable++;
                    if (missingUsefulOverride) oversizedWithoutOverride++;
                    if (mipmapConcern) mipmapConcerns++;

                    double bytes = (double)width * height * 4d;
                    if (importer.mipmapEnabled)
                        bytes *= 1.333333333d;
                    double estimatedMb = bytes / (1024d * 1024d);
                    rawMemoryMb += estimatedMb;

                    int issuesAdded = 0;

                    if (isOversized)
                    {
                        float ratio = largestSide / (float)Mathf.Max(1, recommendedSize);
                        bool extremeDimension = largestSide >= 4096 && recommendedSize <= 2048;
                        bool extremeRatio = ratio >= 4f;
                        bool severeOversize = (extremeDimension || extremeRatio) && estimatedMb >= 24d;
                        MPOSeverity severity = severeOversize && context.Profile.DeviceTier != MPODeviceTier.HighEnd
                            ? MPOSeverity.Critical
                            : MPOSeverity.Warning;

                        var details = new StringBuilder();
                        details.AppendLine($"Imported size: {width} × {height}");
                        details.AppendLine($"Recommended mobile max: {recommendedSize} px");
                        details.AppendLine($"Current {context.Profile.PlatformTextureSettingsName} max: {platformMaxSize} px");
                        details.AppendLine($"{context.Profile.PlatformTextureSettingsName} override: {MPOFormatUtility.Bool(hasPlatformOverride)}");
                        details.Append($"Approx. uncompressed upper-bound: {MPOFormatUtility.Megabytes(estimatedMb)}");

                        var recommendations = new StringBuilder();
                        recommendations.AppendLine($"• Set the {context.Profile.PlatformTextureSettingsName} max size to {recommendedSize}px if the texture still looks acceptable on target devices.");
                        recommendations.AppendLine("• This keeps the source texture unchanged and only changes the selected mobile platform import.");
                        if (missingUsefulOverride)
                            recommendations.AppendLine($"• No explicit {context.Profile.PlatformTextureSettingsName} override is currently enabled.");
                        if (!normalMap)
                            recommendations.AppendLine("• Also review mobile compression for the target device range: " + context.Profile.PreferredTextureCompression + ".");

                        result.AddIssue(new MPOIssue(
                            Category,
                            severity,
                            "Oversized texture for selected device profile",
                            details.ToString(),
                            recommendations.ToString().TrimEnd(),
                            severity == MPOSeverity.Critical ? 6 : 4,
                            null,
                            path,
                            "textures.oversized-mobile",
                            MPOFixKind.SetTexturePlatformMaxSize,
                            MPOFixSafety.ReviewRequired,
                            $"{context.Profile.PlatformTextureSettingsName} max size: {platformMaxSize}px → {recommendedSize}px\n\nThe desktop/source texture is not resized. Review image quality after reimport.",
                            fixIntValue: recommendedSize,
                            fixStringValue: context.Profile.PlatformTextureSettingsName,
                            gpuImpact: estimatedMb >= 16d ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                            memoryImpact: estimatedMb >= 16d ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                            buildSizeImpact: MPOImpactLevel.Medium,
                            thermalImpact: context.Profile.DeviceTier == MPODeviceTier.LowEnd ? MPOImpactLevel.Medium : MPOImpactLevel.Low));
                        issuesAdded++;
                    }

                    if (isReadable)
                    {
                        result.AddIssue(new MPOIssue(
                            Category,
                            estimatedMb >= 4d ? MPOSeverity.Warning : MPOSeverity.Suggestion,
                            "Texture Read/Write is enabled",
                            $"Imported size: {width} × {height}\nRead/Write: On\nApprox. uncompressed upper-bound: {MPOFormatUtility.Megabytes(estimatedMb)}",
                            "Disable Read/Write when no runtime system calls GetPixels/SetPixels or otherwise needs CPU-side texture data.",
                            estimatedMb >= 4d ? 3 : 1,
                            null,
                            path,
                            "textures.read-write",
                            MPOFixKind.DisableTextureReadWrite,
                            MPOFixSafety.ReviewRequired,
                            "Read/Write: ON → OFF\n\nThis can reduce duplicate CPU-side texture memory. Runtime code that reads or modifies texture pixels must be checked first.",
                            cpuImpact: MPOImpactLevel.Low,
                            memoryImpact: estimatedMb >= 4d ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                            buildSizeImpact: MPOImpactLevel.Low));
                        issuesAdded++;
                    }

                    if (mipmapConcern)
                    {
                        result.AddIssue(new MPOIssue(
                            Category,
                            MPOSeverity.Suggestion,
                            "UI/Sprite texture uses mipmaps",
                            $"Texture type: {importer.textureType}\nMip Maps: On\nImported size: {width} × {height}",
                            "Disable mipmaps when this UI/Sprite texture is always displayed near native UI size and does not need minification. Keep them when the asset is scaled down or used in world-space UI.",
                            1,
                            null,
                            path,
                            "textures.ui-mipmaps",
                            gpuImpact: MPOImpactLevel.Low,
                            memoryImpact: MPOImpactLevel.Low,
                            buildSizeImpact: MPOImpactLevel.Low));
                        issuesAdded++;
                    }

                    if (issuesAdded == 0)
                        result.AddPass();
                });

                if (!ok) skipped++;

                if ((i & 7) == 7)
                    yield return null;
            }

            result.SetMetric("Textures Scanned", MPOFormatUtility.Number(scanned));
            result.SetMetric("Oversized", MPOFormatUtility.Number(oversized));
            result.SetMetric("Read/Write Enabled", MPOFormatUtility.Number(readable));
            result.SetMetric("Oversized / No Target Override", MPOFormatUtility.Number(oversizedWithoutOverride));
            result.SetMetric("UI Mipmap Concerns", MPOFormatUtility.Number(mipmapConcerns));
            result.SetMetric("Skipped Textures", MPOFormatUtility.Number(skipped));
            result.SetMetric("Project Raw Upper-Bound", MPOFormatUtility.Megabytes(rawMemoryMb));
            context.ReportProgress("Texture scan complete", 1f);
        }

        private static bool IsUiLike(string path)
        {
            string lower = (path ?? string.Empty).ToLowerInvariant();
            return lower.Contains("/ui/") || lower.Contains("/gui/") || lower.Contains("icon") || lower.Contains("hud") || lower.Contains("button") || lower.Contains("panel");
        }
    }
}
