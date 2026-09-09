using System.Text;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public sealed class QualitySettingsAnalyzer : IMPOAnalyzer
    {
        public string Name => "Quality Settings Analyzer";
        public MPOCategory Category => MPOCategory.Quality;

        public void Analyze(MPOScanContext context)
        {
            MPOCategoryResult result = context.Result.GetOrCreate(Category);

            context.TryGet("Quality level index", () => QualitySettings.GetQualityLevel(), out int qualityLevel);
            context.TryGet("Quality level names", () => QualitySettings.names, out string[] names);
            names = names ?? new string[0];
            string qualityName = qualityLevel >= 0 && qualityLevel < names.Length ? names[qualityLevel] : "Unknown";

            context.TryGet("Quality MSAA", () => QualitySettings.antiAliasing, out int aa);
            context.TryGet("Quality VSync", () => QualitySettings.vSyncCount, out int vSync);
            context.TryGet("Quality shadow distance", () => QualitySettings.shadowDistance, out float shadowDistance);
            context.TryGet("Quality LOD bias", () => QualitySettings.lodBias, out float lodBias);
            context.TryGet("Quality mip streaming", () => QualitySettings.streamingMipmapsActive, out bool mipStreaming);

            bool aaConcern = aa > context.Profile.MaxQualityMsaaSamples;
            bool shadowConcern = shadowDistance > context.Profile.MaxQualityShadowDistance;
            bool lodConcern = lodBias > Mathf.Max(1f, context.Profile.MinLodBias * 1.75f) && context.Profile.DeviceTier == MPODeviceTier.LowEnd;

            if (aaConcern || shadowConcern || lodConcern)
            {
                var details = new StringBuilder();
                details.AppendLine("Active Quality Level: " + qualityName);
                details.AppendLine("Anti Aliasing: " + (aa <= 0 ? "Off" : aa + "x"));
                details.AppendLine("Shadow Distance: " + shadowDistance.ToString("0.#"));
                details.AppendLine("LOD Bias: " + lodBias.ToString("0.##"));
                details.AppendLine("VSync Count: " + vSync);
                details.Append("Texture Streaming: " + MPOFormatUtility.Bool(mipStreaming));

                var recommendation = new StringBuilder();
                if (aaConcern) recommendation.AppendLine("• Active QualitySettings MSAA exceeds the selected mobile guidance. URP assets can also control MSAA, so verify which setting your pipeline actually uses.");
                if (shadowConcern) recommendation.AppendLine("• Reduce quality shadow distance if distant realtime shadows are not visually important.");
                if (lodConcern) recommendation.AppendLine("• High LOD Bias keeps detailed LODs visible farther away. Review it on low-end targets.");

                int concernCount = (aaConcern ? 1 : 0) + (shadowConcern ? 1 : 0) + (lodConcern ? 1 : 0);
                result.AddIssue(new MPOIssue(
                    Category,
                    concernCount >= 2 && context.Profile.DeviceTier == MPODeviceTier.LowEnd ? MPOSeverity.Critical : MPOSeverity.Warning,
                    "Active quality level is expensive for selected target",
                    details.ToString(),
                    recommendation.ToString().TrimEnd(),
                    concernCount >= 2 ? 6 : 4,
                    ruleId: "quality.active-level",
                    cpuImpact: lodConcern ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                    gpuImpact: concernCount >= 2 ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                    memoryImpact: lodConcern ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                    thermalImpact: concernCount >= 2 ? MPOImpactLevel.High : MPOImpactLevel.Medium));
            }
            else result.AddPass();

            if (context.Profile.DeviceTier == MPODeviceTier.LowEnd && !mipStreaming)
            {
                result.AddIssue(new MPOIssue(
                    Category,
                    MPOSeverity.Suggestion,
                    "Texture mipmap streaming is disabled",
                    "QualitySettings.streamingMipmapsActive is Off for the active quality level.",
                    "Texture streaming can reduce resident texture memory in suitable 3D projects, but it requires appropriate mipmapped textures and should be validated for your content. UI-heavy/2D projects may not benefit.",
                    2,
                    ruleId: "quality.mipmap-streaming",
                    cpuImpact: MPOImpactLevel.Low,
                    gpuImpact: MPOImpactLevel.Low,
                    memoryImpact: MPOImpactLevel.Medium));
            }
            else result.AddPass();

            result.SetMetric("Active Level", qualityName);
            result.SetMetric("MSAA", aa <= 0 ? "Off" : aa + "x");
            result.SetMetric("Shadow Distance", shadowDistance.ToString("0.#"));
            result.SetMetric("LOD Bias", lodBias.ToString("0.##"));
            result.SetMetric("VSync", vSync.ToString());
            result.SetMetric("Mip Streaming", MPOFormatUtility.Bool(mipStreaming));
            context.ReportProgress("Quality settings scan complete", 1f);
        }
    }
}
