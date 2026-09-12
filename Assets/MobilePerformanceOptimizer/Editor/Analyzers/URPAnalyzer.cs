using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MobilePerformanceOptimizer
{
    public sealed class URPAnalyzer : IMPOAnalyzer
    {
        public string Name => "URP Analyzer";
        public MPOCategory Category => MPOCategory.URP;

        public void Analyze(MPOScanContext context)
        {
            MPOCategoryResult result = context.Result.GetOrCreate(Category);
            if (!context.TryGet("Active render pipeline", () => GraphicsSettings.currentRenderPipeline, out RenderPipelineAsset pipelineAsset))
            {
                result.SetMetric("Active Pipeline", "Unreadable");
                return;
            }

            if (pipelineAsset == null)
            {
                result.AddIssue(new MPOIssue(
                    Category,
                    MPOSeverity.Critical,
                    "URP is not the active render pipeline",
                    "GraphicsSettings.currentRenderPipeline is null, which indicates the Built-in Render Pipeline is active for the current quality level.",
                    "This Mobile Performance Optimizer release targets Unity 6 + URP. Assign a Universal Render Pipeline Asset before relying on URP-specific recommendations.",
                    10,
                    ruleId: "urp.not-active",
                    gpuImpact: MPOImpactLevel.High));
                result.SetMetric("Active Pipeline", "Built-in / None");
                context.ReportProgress("URP scan complete", 1f);
                return;
            }

            string typeName = "Unknown";
            context.TryGet("URP pipeline type", () => pipelineAsset.GetType().FullName ?? pipelineAsset.GetType().Name, out typeName);
            if (string.IsNullOrEmpty(typeName)) typeName = "Unknown";

            string pipelineName = SafeObjectName(pipelineAsset);
            bool looksLikeUrp = typeName.Contains("UniversalRenderPipelineAsset");
            result.SetMetric("Active Pipeline", pipelineName);
            result.SetMetric("Pipeline Type", typeName);

            if (!looksLikeUrp)
            {
                result.AddIssue(new MPOIssue(
                    Category,
                    MPOSeverity.Critical,
                    "Active render pipeline is not URP",
                    $"Active pipeline asset type: {typeName}",
                    "This release currently provides URP-specific render-pipeline checks. Those checks were skipped for the active pipeline.",
                    10,
                    pipelineAsset,
                    SafeAssetPath(pipelineAsset),
                    "urp.unsupported-pipeline",
                    gpuImpact: MPOImpactLevel.High));
                context.ReportProgress("URP scan complete", 1f);
                return;
            }

            if (!context.TryGet("URP serialized settings", () => new SerializedObject(pipelineAsset), out SerializedObject serialized) || serialized == null)
            {
                result.AddIssue(new MPOIssue(
                    Category,
                    MPOSeverity.Suggestion,
                    "URP settings could not be read safely",
                    "The active URP asset exists, but its serialized settings could not be opened by the analyzer.",
                    "The rest of the project scan continued. Re-scan after Unity finishes importing/compiling, and verify the URP asset in the Inspector.",
                    1,
                    pipelineAsset,
                    SafeAssetPath(pipelineAsset),
                    "urp.serialized-read-failed"));
                context.ReportProgress("URP scan complete", 1f);
                return;
            }

            var findings = new StringBuilder();
            var recommendations = new StringBuilder();
            MPOSeverity severity = MPOSeverity.Suggestion;
            int penalty = 0;
            int checks = 0;
            int passed = 0;
            int gpuConcernWeight = 0;
            int memoryConcernWeight = 0;

            CheckFloat(context, serialized, "m_RenderScale", "Render Scale", context.Profile.MaxUrpRenderScale, value => value <= context.Profile.MaxUrpRenderScale,
                value => $"{value:0.##}",
                "Consider lowering Render Scale for this target profile if visual quality remains acceptable.",
                ref checks, ref passed, ref severity, ref penalty, findings, recommendations, 3, ref gpuConcernWeight, 3);

            CheckInt(context, serialized, "m_MSAA", "MSAA", context.Profile.MaxUrpMsaaSamples, value => value <= context.Profile.MaxUrpMsaaSamples,
                value => value + "x",
                "Review MSAA level; lower values can reduce GPU bandwidth/fill cost on constrained devices.",
                ref checks, ref passed, ref severity, ref penalty, findings, recommendations, 2, ref gpuConcernWeight, 2);

            CheckFloat(context, serialized, "m_ShadowDistance", "Shadow Distance", context.Profile.MaxShadowDistance, value => value <= context.Profile.MaxShadowDistance,
                value => value.ToString("0.#"),
                "Reduce shadow distance if distant realtime shadows are not visually important.",
                ref checks, ref passed, ref severity, ref penalty, findings, recommendations, 4, ref gpuConcernWeight, 3);

            CheckInt(context, serialized, "m_ShadowCascadeCount", "Shadow Cascades", context.Profile.MaxShadowCascades, value => value <= context.Profile.MaxShadowCascades,
                value => value.ToString(),
                "Review cascade count for mobile; fewer cascades can reduce shadow rendering work.",
                ref checks, ref passed, ref severity, ref penalty, findings, recommendations, 2, ref gpuConcernWeight, 2);

            if (TryReadBool(context, serialized, "m_SupportsHDR", "HDR", out bool hdr))
            {
                checks++;
                bool fail = context.Profile.RecommendHdrOff && hdr;
                result.SetMetric("HDR", MPOFormatUtility.Bool(hdr));
                if (!fail) passed++;
                else
                {
                    severity = MaxSeverity(severity, MPOSeverity.Warning);
                    penalty += 2;
                    gpuConcernWeight += 1;
                    memoryConcernWeight += 1;
                    findings.AppendLine("HDR: On (review for Low-End profile)");
                    recommendations.AppendLine("• Disable HDR if the project does not require HDR-dependent effects on the selected low-end profile.");
                }
            }

            if (TryReadBool(context, serialized, "m_AdditionalLightShadowsSupported", "Additional Light Shadows", out bool additionalShadows))
            {
                checks++;
                result.SetMetric("Additional Light Shadows", MPOFormatUtility.Bool(additionalShadows));
                bool fail = context.Profile.DeviceTier == MPODeviceTier.LowEnd && additionalShadows;
                if (!fail) passed++;
                else
                {
                    severity = MaxSeverity(severity, MPOSeverity.Warning);
                    penalty += 3;
                    gpuConcernWeight += 3;
                    findings.AppendLine("Additional Light Shadows: On");
                    recommendations.AppendLine("• Low-End profile: consider disabling additional-light shadows unless they are essential.");
                }
            }

            if (TryReadBool(context, serialized, "m_SoftShadowsSupported", "Soft Shadows", out bool softShadows))
            {
                checks++;
                result.SetMetric("Soft Shadows", MPOFormatUtility.Bool(softShadows));
                bool fail = context.Profile.DeviceTier == MPODeviceTier.LowEnd && softShadows;
                if (!fail) passed++;
                else
                {
                    severity = MaxSeverity(severity, MPOSeverity.Warning);
                    penalty += 2;
                    gpuConcernWeight += 2;
                    findings.AppendLine("Soft Shadows: On");
                    recommendations.AppendLine("• Low-End profile: review soft shadows and disable them if the visual trade-off is acceptable.");
                }
            }

            if (TryReadBool(context, serialized, "m_RequireDepthTexture", "Depth Texture", out bool depth))
            {
                checks++;
                result.SetMetric("Depth Texture", MPOFormatUtility.Bool(depth));
                if (!depth) passed++;
                else
                {
                    severity = MaxSeverity(severity, MPOSeverity.Suggestion);
                    penalty += 1;
                    gpuConcernWeight += 1;
                    memoryConcernWeight += 1;
                    findings.AppendLine("Depth Texture: On");
                    recommendations.AppendLine("• Depth Texture has a bandwidth/memory cost. Keep it enabled only when renderer features/effects require it.");
                }
            }

            if (TryReadBool(context, serialized, "m_RequireOpaqueTexture", "Opaque Texture", out bool opaque))
            {
                checks++;
                result.SetMetric("Opaque Texture", MPOFormatUtility.Bool(opaque));
                if (!opaque) passed++;
                else
                {
                    severity = MaxSeverity(severity, MPOSeverity.Suggestion);
                    penalty += 1;
                    gpuConcernWeight += 1;
                    memoryConcernWeight += 1;
                    findings.AppendLine("Opaque Texture: On");
                    recommendations.AppendLine("• Opaque Texture adds camera-color copy/bandwidth work. Disable it when no effect needs the camera color texture.");
                }
            }

            for (int i = 0; i < passed; i++)
                result.AddPass();

            int failed = Mathf.Max(0, checks - passed);
            if (failed > 0)
            {
                if (failed >= 4 && context.Profile.DeviceTier == MPODeviceTier.LowEnd)
                    severity = MaxSeverity(severity, MPOSeverity.Critical);

                var issue = new MPOIssue(
                    Category,
                    severity,
                    "URP settings need mobile review",
                    findings.ToString().TrimEnd(),
                    recommendations.ToString().TrimEnd() + "\n\n" + context.Profile.PlatformGuidance,
                    Mathf.Clamp(penalty, 1, 10),
                    pipelineAsset,
                    SafeAssetPath(pipelineAsset),
                    "urp.settings-review",
                    fixKind: MPOFixKind.ReviewSettings,
                    fixSafety: MPOFixSafety.ReviewRequired,
                    cpuImpact: gpuConcernWeight >= 6 ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                    gpuImpact: gpuConcernWeight >= 6 ? MPOImpactLevel.High : gpuConcernWeight >= 2 ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                    memoryImpact: memoryConcernWeight >= 2 ? MPOImpactLevel.Medium : memoryConcernWeight == 1 ? MPOImpactLevel.Low : MPOImpactLevel.None,
                    thermalImpact: gpuConcernWeight >= 5 ? MPOImpactLevel.High : MPOImpactLevel.Medium);
                AddRecommendation(issue, serialized, "m_RenderScale", context.Profile.MaxUrpRenderScale);
                AddRecommendation(issue, serialized, "m_MSAA", context.Profile.MaxUrpMsaaSamples);
                AddRecommendation(issue, serialized, "m_ShadowDistance", context.Profile.MaxShadowDistance);
                AddRecommendation(issue, serialized, "m_ShadowCascadeCount", context.Profile.MaxShadowCascades);
                if (context.Profile.RecommendHdrOff) AddRecommendation(issue, serialized, "m_SupportsHDR", false);
                if (context.Profile.DeviceTier == MPODeviceTier.LowEnd)
                {
                    AddRecommendation(issue, serialized, "m_AdditionalLightShadowsSupported", false);
                    AddRecommendation(issue, serialized, "m_SoftShadowsSupported", false);
                }
                AddRecommendation(issue, serialized, "m_RequireDepthTexture", false);
                AddRecommendation(issue, serialized, "m_RequireOpaqueTexture", false);
                result.AddIssue(issue);
            }

            if (checks == 0)
            {
                result.AddIssue(new MPOIssue(
                    Category,
                    MPOSeverity.Suggestion,
                    "URP asset detected but no known settings were readable",
                    "The active pipeline is URP, but this version did not expose the serialized property names used by the current analyzer, or Unity could not safely read them during this scan.",
                    "The analyzer intentionally avoids Unity internal/reflection APIs. Re-scan after imports finish; if the issue persists, record the Unity/URP version so compatibility mappings can be extended safely.",
                    1,
                    pipelineAsset,
                    SafeAssetPath(pipelineAsset),
                    "urp.serialized-properties-unavailable"));
            }

            context.ReportProgress("URP scan complete", 1f);
        }

        private static void AddRecommendation(MPOIssue issue, SerializedObject target, string name, object value)
        {
            var p = target.FindProperty(name);
            if (p == null) return;
            bool needed = value is bool b ? p.boolValue != b : value is int i ? p.intValue > i : p.floatValue > (float)value;
            if (needed) issue.SettingRecommendations[name] = value;
        }

        private static void CheckFloat(
            MPOScanContext context,
            SerializedObject serialized,
            string propertyName,
            string displayName,
            float threshold,
            Func<float, bool> isPass,
            Func<float, string> formatter,
            string recommendation,
            ref int checks,
            ref int passed,
            ref MPOSeverity severity,
            ref int penalty,
            StringBuilder findings,
            StringBuilder recommendations,
            int penaltyOnFail,
            ref int gpuConcernWeight,
            int gpuWeightOnFail)
        {
            try
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property == null)
                    return;

                checks++;
                float value = property.floatValue;
                if (isPass(value))
                {
                    passed++;
                    return;
                }

                severity = MaxSeverity(severity, MPOSeverity.Warning);
                penalty += penaltyOnFail;
                gpuConcernWeight += gpuWeightOnFail;
                findings.AppendLine($"{displayName}: {formatter(value)} (guideline ≤ {threshold:0.##})");
                recommendations.AppendLine("• " + recommendation);
            }
            catch (Exception exception)
            {
                context.RecordRecoverableError("URP property: " + displayName, exception);
            }
        }

        private static void CheckInt(
            MPOScanContext context,
            SerializedObject serialized,
            string propertyName,
            string displayName,
            int threshold,
            Func<int, bool> isPass,
            Func<int, string> formatter,
            string recommendation,
            ref int checks,
            ref int passed,
            ref MPOSeverity severity,
            ref int penalty,
            StringBuilder findings,
            StringBuilder recommendations,
            int penaltyOnFail,
            ref int gpuConcernWeight,
            int gpuWeightOnFail)
        {
            try
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property == null)
                    return;

                checks++;
                int value = property.intValue;
                if (isPass(value))
                {
                    passed++;
                    return;
                }

                severity = MaxSeverity(severity, MPOSeverity.Warning);
                penalty += penaltyOnFail;
                gpuConcernWeight += gpuWeightOnFail;
                findings.AppendLine($"{displayName}: {formatter(value)} (guideline ≤ {threshold})");
                recommendations.AppendLine("• " + recommendation);
            }
            catch (Exception exception)
            {
                context.RecordRecoverableError("URP property: " + displayName, exception);
            }
        }

        private static bool TryReadBool(MPOScanContext context, SerializedObject serialized, string propertyName, string displayName, out bool value)
        {
            value = false;
            try
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property == null)
                    return false;
                value = property.boolValue;
                return true;
            }
            catch (Exception exception)
            {
                context.RecordRecoverableError("URP property: " + displayName, exception);
                return false;
            }
        }

        private static MPOSeverity MaxSeverity(MPOSeverity a, MPOSeverity b)
        {
            return (MPOSeverity)Mathf.Max((int)a, (int)b);
        }

        private static string SafeObjectName(UnityEngine.Object obj)
        {
            try { return obj != null && !string.IsNullOrEmpty(obj.name) ? obj.name : "Unknown"; }
            catch { return "Unknown"; }
        }

        private static string SafeAssetPath(UnityEngine.Object obj)
        {
            try { return obj != null ? AssetDatabase.GetAssetPath(obj) : string.Empty; }
            catch { return string.Empty; }
        }
    }
}
