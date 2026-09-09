using System.Text;
using UnityEditor;

namespace MobilePerformanceOptimizer
{
    public sealed class BuildSettingsAnalyzer : IMPOAnalyzer
    {
        public string Name => "Build Settings Analyzer";
        public MPOCategory Category => MPOCategory.Build;

        public void Analyze(MPOScanContext context)
        {
            MPOCategoryResult result = context.Result.GetOrCreate(Category);

            bool development = SafeBool(context, "Development Build", () => EditorUserBuildSettings.development);
            bool debugging = SafeBool(context, "Script Debugging", () => EditorUserBuildSettings.allowDebugging);
            bool profiler = SafeBool(context, "Autoconnect Profiler", () => EditorUserBuildSettings.connectProfiler);
            bool deepProfiler = SafeBool(context, "Deep Profiling Support", () => EditorUserBuildSettings.buildWithDeepProfilingSupport);
            int enabledCount = (development ? 1 : 0) + (debugging ? 1 : 0) + (profiler ? 1 : 0) + (deepProfiler ? 1 : 0);

            if (enabledCount > 0)
            {
                var details = new StringBuilder();
                details.AppendLine("Development Build: " + MPOFormatUtility.Bool(development));
                details.AppendLine("Script Debugging: " + MPOFormatUtility.Bool(debugging));
                details.AppendLine("Autoconnect Profiler: " + MPOFormatUtility.Bool(profiler));
                details.Append("Deep Profiling Support: " + MPOFormatUtility.Bool(deepProfiler));

                result.AddIssue(new MPOIssue(
                    Category,
                    deepProfiler ? MPOSeverity.Critical : MPOSeverity.Warning,
                    "Profiling/debug build flags are enabled",
                    details.ToString(),
                    "Keep these enabled while profiling when needed, but turn them off before measuring release performance or making a production build.",
                    deepProfiler ? 6 : 4,
                    ruleId: "build.debug-flags",
                    fixKind: MPOFixKind.DisableDevelopmentBuildFlags,
                    fixSafety: MPOFixSafety.Safe,
                    fixPreview: "Development Build → Off\nScript Debugging → Off\nAutoconnect Profiler → Off\nDeep Profiling Support → Off\n\nThis does not build the player; it only updates the current Editor build flags.",
                    cpuImpact: deepProfiler ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                    memoryImpact: deepProfiler ? MPOImpactLevel.High : MPOImpactLevel.Low,
                    buildSizeImpact: MPOImpactLevel.Medium,
                    thermalImpact: deepProfiler ? MPOImpactLevel.High : MPOImpactLevel.Medium));
            }
            else result.AddPass();

            result.SetMetric("Target", context.Profile.TargetPlatform.ToString());
            result.SetMetric("Development", MPOFormatUtility.Bool(development));
            result.SetMetric("Debugging", MPOFormatUtility.Bool(debugging));
            result.SetMetric("Profiler", MPOFormatUtility.Bool(profiler));
            result.SetMetric("Deep Profiling", MPOFormatUtility.Bool(deepProfiler));
            context.ReportProgress("Build settings scan complete", 1f);
        }

        private static bool SafeBool(MPOScanContext context, string label, System.Func<bool> getter)
        {
            return context.TryGet("Build setting: " + label, getter, out bool value) && value;
        }
    }
}
