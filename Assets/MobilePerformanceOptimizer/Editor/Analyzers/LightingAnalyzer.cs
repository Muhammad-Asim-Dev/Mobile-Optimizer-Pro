using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public sealed class LightingAnalyzer : IMPOIncrementalAnalyzer
    {
        public string Name => "Lighting Analyzer";
        public MPOCategory Category => MPOCategory.Lighting;

        public void Analyze(MPOScanContext context)
        {
            IEnumerator routine = AnalyzeIncremental(context);
            while (routine != null && routine.MoveNext()) { }
        }

        public IEnumerator AnalyzeIncremental(MPOScanContext context)
        {
            MPOCategoryResult result = context.Result.GetOrCreate(Category);
            List<Light> lights = MPOSceneUtility.GetComponentsInLoadedScenes<Light>(context);

            int realtime = 0;
            int baked = 0;
            int mixed = 0;
            int runtimeLights = 0;
            int runtimeShadowLights = 0;
            int pointRuntimeShadowLights = 0;
            int spotRuntimeShadowLights = 0;
            int directionalRuntimeShadowLights = 0;
            int skippedLights = 0;

            for (int i = 0; i < lights.Count; i++)
            {
                Light light = lights[i];
                bool ok = context.TryExecute("Light: " + SafeObjectName(light), () =>
                {
                    if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
                        return;

                    bool fullyBaked = false;
                    switch (light.lightmapBakeType)
                    {
                        case LightmapBakeType.Baked:
                            baked++;
                            fullyBaked = true;
                            break;
                        case LightmapBakeType.Mixed:
                            mixed++;
                            runtimeLights++;
                            break;
                        default:
                            realtime++;
                            runtimeLights++;
                            break;
                    }

                    if (fullyBaked || light.shadows == LightShadows.None)
                        return;

                    runtimeShadowLights++;
                    switch (light.type)
                    {
                        case LightType.Point:
                            pointRuntimeShadowLights++;
                            break;
                        case LightType.Spot:
                            spotRuntimeShadowLights++;
                            break;
                        case LightType.Directional:
                            directionalRuntimeShadowLights++;
                            break;
                    }
                });

                if (!ok) skippedLights++;

                if ((i & 31) == 31)
                {
                    if (context.ReportProgress("Reading lights", (i + 1f) / Mathf.Max(1, lights.Count))) yield break;
                    yield return null;
                }
            }

            result.SetMetric("Enabled Lights", MPOFormatUtility.Number(realtime + baked + mixed));
            result.SetMetric("Runtime Lights", MPOFormatUtility.Number(runtimeLights));
            result.SetMetric("Realtime", MPOFormatUtility.Number(realtime));
            result.SetMetric("Mixed", MPOFormatUtility.Number(mixed));
            result.SetMetric("Baked", MPOFormatUtility.Number(baked));
            result.SetMetric("Runtime Shadow Lights", MPOFormatUtility.Number(runtimeShadowLights));
            result.SetMetric("Point Shadow Lights", MPOFormatUtility.Number(pointRuntimeShadowLights));
            result.SetMetric("Spot Shadow Lights", MPOFormatUtility.Number(spotRuntimeShadowLights));
            result.SetMetric("Directional Shadow Lights", MPOFormatUtility.Number(directionalRuntimeShadowLights));
            result.SetMetric("Skipped Lights", MPOFormatUtility.Number(skippedLights));

            if (runtimeLights <= context.Profile.MaxRealtimeLights)
            {
                result.AddPass();
            }
            else
            {
                bool severe = runtimeLights > context.Profile.MaxRealtimeLights * 2;
                result.AddIssue(new MPOIssue(
                    Category,
                    severe ? MPOSeverity.Critical : MPOSeverity.Warning,
                    "Runtime light count is high",
                    $"{runtimeLights} enabled realtime/mixed lights were found. Fully baked lights are excluded from this runtime count. The selected profile guideline is {context.Profile.MaxRealtimeLights}.",
                    "Bake suitable lighting, restrict light ranges/culling masks, and keep only runtime lights that materially affect the mobile presentation. Mixed lights are treated as runtime-capable because part of their cost can remain dynamic.",
                    severe ? 9 : 5,
                    ruleId: "lighting.runtime-light-count",
                    cpuImpact: severe ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                    gpuImpact: severe ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                    thermalImpact: severe ? MPOImpactLevel.High : MPOImpactLevel.Medium));
            }

            if (runtimeShadowLights <= context.Profile.MaxRealtimeShadowLights)
            {
                result.AddPass();
            }
            else
            {
                bool severe = runtimeShadowLights > context.Profile.MaxRealtimeShadowLights * 2;
                result.AddIssue(new MPOIssue(
                    Category,
                    severe ? MPOSeverity.Critical : MPOSeverity.Warning,
                    "Too many runtime lights cast shadows",
                    $"{runtimeShadowLights} enabled realtime/mixed lights cast shadows. The selected profile guideline is {context.Profile.MaxRealtimeShadowLights}. Baked-only lights are not counted.",
                    "Disable shadows on secondary lights where visually acceptable, reduce shadow distance/resolution, limit shadow-casting objects, or bake suitable lights.",
                    severe ? 10 : 6,
                    ruleId: "lighting.runtime-shadow-count",
                    cpuImpact: MPOImpactLevel.Medium,
                    gpuImpact: severe ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                    memoryImpact: MPOImpactLevel.Low,
                    thermalImpact: severe ? MPOImpactLevel.High : MPOImpactLevel.Medium));
            }

            int localShadowLights = pointRuntimeShadowLights + spotRuntimeShadowLights;
            if (localShadowLights == 0)
            {
                result.AddPass();
            }
            else
            {
                int warningThreshold = context.Profile.DeviceTier == MPODeviceTier.LowEnd ? 2 : 4;
                bool warning = localShadowLights > warningThreshold;
                result.AddIssue(new MPOIssue(
                    Category,
                    warning ? MPOSeverity.Warning : MPOSeverity.Suggestion,
                    "Point/Spot lights cast runtime shadows",
                    $"{localShadowLights} enabled local lights cast runtime shadows ({pointRuntimeShadowLights} Point, {spotRuntimeShadowLights} Spot).",
                    "Local-light shadows can be expensive depending on visibility, range, overlap and shadow resolution. Validate them with Unity Profiler/GPU tools on representative target hardware.",
                    warning ? 4 : 1,
                    ruleId: "lighting.local-shadow-lights",
                    cpuImpact: warning ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                    gpuImpact: warning ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                    thermalImpact: warning ? MPOImpactLevel.High : MPOImpactLevel.Medium));
            }

            context.ReportProgress("Lighting scan complete", 1f);
        }

        private static string SafeObjectName(Object obj)
        {
            try { return obj != null && !string.IsNullOrEmpty(obj.name) ? obj.name : "Unknown"; }
            catch { return "Unknown"; }
        }
    }
}
