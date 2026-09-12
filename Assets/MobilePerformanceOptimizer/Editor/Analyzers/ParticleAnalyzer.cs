using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public sealed class ParticleAnalyzer : IMPOIncrementalAnalyzer
    {
        public string Name => "Particle Analyzer";
        public MPOCategory Category => MPOCategory.Particles;

        public void Analyze(MPOScanContext context)
        {
            IEnumerator routine = AnalyzeIncremental(context);
            while (routine != null && routine.MoveNext()) { }
        }

        public IEnumerator AnalyzeIncremental(MPOScanContext context)
        {
            MPOCategoryResult result = context.Result.GetOrCreate(Category);
            List<ParticleSystem> systems = MPOSceneUtility.GetComponentsInLoadedScenes<ParticleSystem>(context);

            int active = 0;
            int collisionEnabled = 0;
            int trailsEnabled = 0;
            int lightsEnabled = 0;
            int meshRenderers = 0;
            int subEmitterSystems = 0;
            int skippedSystems = 0;
            long maxParticleCapacity = 0;
            long estimatedSteadyParticles = 0;

            for (int i = 0; i < systems.Count; i++)
            {
                ParticleSystem ps = systems[i];
                bool ok = context.TryExecute("Particle system: " + SafeObjectName(ps), () =>
                {
                    if (ps == null || !ps.gameObject.activeInHierarchy)
                        return;

                    active++;
                    ParticleSystem.MainModule main = ps.main;
                    ParticleSystem.EmissionModule emission = ps.emission;
                    ParticleSystem.CollisionModule collision = ps.collision;
                    ParticleSystem.TrailModule trails = ps.trails;
                    ParticleSystem.LightsModule lights = ps.lights;
                    ParticleSystem.SubEmittersModule subEmitters = ps.subEmitters;
                    ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();

                    int maxParticles = main.maxParticles;
                    maxParticleCapacity += maxParticles;
                    float maxLifetime = GetMaxCurveValue(main.startLifetime);
                    float rateOverTime = emission.enabled ? GetMaxCurveValue(emission.rateOverTime) : 0f;
                    int roughSteadyCount = Mathf.Min(maxParticles, Mathf.CeilToInt(rateOverTime * maxLifetime));
                    estimatedSteadyParticles += roughSteadyCount;

                    bool hasCollision = collision.enabled;
                    bool hasTrails = trails.enabled;
                    bool hasLights = lights.enabled;
                    bool hasSubEmitters = subEmitters.enabled && subEmitters.subEmittersCount > 0;
                    bool usesMesh = renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh;

                    if (hasCollision) collisionEnabled++;
                    if (hasTrails) trailsEnabled++;
                    if (hasLights) lightsEnabled++;
                    if (hasSubEmitters) subEmitterSystems++;
                    if (usesMesh) meshRenderers++;

                    float capacityRatio = maxParticles / (float)Mathf.Max(1, context.Profile.MaxParticlesPerSystem);
                    bool heavyCapacity = capacityRatio > 1f;
                    bool highEstimatedRuntime = roughSteadyCount > context.Profile.MaxParticlesPerSystem / 2;

                    int costScore = 0;
                    if (capacityRatio > 2f) costScore += 4;
                    else if (heavyCapacity) costScore += 2;
                    if (highEstimatedRuntime) costScore += roughSteadyCount > context.Profile.MaxParticlesPerSystem ? 3 : 2;
                    if (hasCollision) costScore += 2;
                    if (hasLights) costScore += 3;
                    if (hasTrails) costScore += 1;
                    if (hasSubEmitters) costScore += 1;
                    if (usesMesh) costScore += 1;

                    if (costScore == 0)
                    {
                        result.AddPass();
                        return;
                    }

                    int criticalThreshold = context.Profile.DeviceTier == MPODeviceTier.HighEnd ? 9 : 7;
                    MPOSeverity severity = costScore >= criticalThreshold
                        ? MPOSeverity.Critical
                        : costScore >= 4 ? MPOSeverity.Warning : MPOSeverity.Suggestion;

                    var details = new StringBuilder();
                    details.AppendLine("Max particles: " + maxParticles.ToString("N0"));
                    details.AppendLine("Approx. rate × lifetime particles: " + roughSteadyCount.ToString("N0"));
                    details.AppendLine("Collision: " + MPOFormatUtility.Bool(hasCollision));
                    details.AppendLine("Trails: " + MPOFormatUtility.Bool(hasTrails));
                    details.AppendLine("Lights module: " + MPOFormatUtility.Bool(hasLights));
                    details.AppendLine("Sub emitters: " + MPOFormatUtility.Bool(hasSubEmitters));
                    details.Append("Mesh rendering: " + MPOFormatUtility.Bool(usesMesh));

                    var recommendation = new StringBuilder();
                    if (heavyCapacity)
                        recommendation.AppendLine("• Max Particles is high for the selected profile. Lower it only after confirming the visual peak does not need the capacity.");
                    if (highEstimatedRuntime)
                        recommendation.AppendLine("• Rate-over-time × max lifetime suggests a high steady particle population. Reduce emission/lifetime where the effect still reads well.");
                    if (hasCollision)
                        recommendation.AppendLine("• Particle collision can be CPU-heavy. Reserve it for effects where contact behavior matters.");
                    if (hasLights)
                        recommendation.AppendLine("• Particle lights can multiply lighting/shadow cost. Prefer emissive visuals or fewer spawned lights when possible.");
                    if (hasTrails)
                        recommendation.AppendLine("• Review trail lifetime and density, especially for effects that overlap heavily on screen.");
                    if (hasSubEmitters)
                        recommendation.AppendLine("• Sub emitters can multiply the real particle workload. Check the full effect chain in representative gameplay.");
                    if (usesMesh)
                        recommendation.AppendLine("• Mesh particles can be more expensive than billboard particles depending on mesh complexity and material overdraw.");

                    MPOImpactLevel gpuImpact = (hasLights || roughSteadyCount > context.Profile.MaxParticlesPerSystem)
                        ? MPOImpactLevel.High
                        : (hasTrails || usesMesh || heavyCapacity) ? MPOImpactLevel.Medium : MPOImpactLevel.Low;
                    MPOImpactLevel cpuImpact = hasCollision || hasSubEmitters
                        ? MPOImpactLevel.Medium
                        : MPOImpactLevel.Low;

                    var issue = new MPOIssue(
                        Category,
                        severity,
                        "Particle system needs mobile review",
                        details.ToString(),
                        recommendation.ToString().TrimEnd(),
                        Mathf.Clamp(1 + costScore, 1, 10),
                        ps,
                        null,
                        "particles.system-review",
                        fixKind: heavyCapacity || highEstimatedRuntime ? MPOFixKind.ReviewSettings : MPOFixKind.None,
                        fixSafety: MPOFixSafety.ReviewRequired,
                        cpuImpact: cpuImpact,
                        gpuImpact: gpuImpact,
                        memoryImpact: heavyCapacity ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                        thermalImpact: (int)gpuImpact >= (int)MPOImpactLevel.Medium ? MPOImpactLevel.High : MPOImpactLevel.Medium);
                    if (heavyCapacity || highEstimatedRuntime)
                        issue.SettingRecommendations["Max Particles"] = highEstimatedRuntime ? context.Profile.MaxParticlesPerSystem / 2 : context.Profile.MaxParticlesPerSystem;
                    result.AddIssue(issue);
                });

                if (!ok) skippedSystems++;

                if ((i & 15) == 15)
                {
                    if (context.ReportProgress("Reading particle systems", (i + 1f) / Mathf.Max(1, systems.Count))) yield break;
                    yield return null;
                }
            }

            if (active > context.Profile.MaxParticleSystems)
            {
                bool severe = active > context.Profile.MaxParticleSystems * 2;
                result.AddIssue(new MPOIssue(
                    Category,
                    severe ? MPOSeverity.Critical : MPOSeverity.Warning,
                    "High number of active particle systems",
                    $"Loaded scenes contain {active:N0} active particle systems. Selected {context.Profile.DisplayName} guidance is {context.Profile.MaxParticleSystems:N0}.",
                    "Pool and disable effects that do not need to stay active. Profile representative gameplay because active count alone does not describe the number of particles actually alive.",
                    severe ? 6 : 4,
                    ruleId: "particles.active-system-count",
                    cpuImpact: severe ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                    gpuImpact: severe ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                    memoryImpact: MPOImpactLevel.Low,
                    thermalImpact: severe ? MPOImpactLevel.High : MPOImpactLevel.Medium));
            }
            else
            {
                result.AddPass();
            }

            if (collisionEnabled > context.Profile.MaxCollisionParticleSystems)
            {
                result.AddIssue(new MPOIssue(
                    Category,
                    MPOSeverity.Warning,
                    "Many particle collision systems are active",
                    $"{collisionEnabled:N0} active particle systems use collision; selected guidance is {context.Profile.MaxCollisionParticleSystems:N0}.",
                    "Reserve collision for effects where it is visibly or mechanically necessary, and reduce collision quality/frequency where supported by the effect.",
                    4,
                    ruleId: "particles.collision-count",
                    cpuImpact: MPOImpactLevel.High,
                    gpuImpact: MPOImpactLevel.Low,
                    thermalImpact: MPOImpactLevel.Medium));
            }
            else
            {
                result.AddPass();
            }

            result.SetMetric("Active Systems", MPOFormatUtility.Number(active));
            result.SetMetric("Max Capacity Sum", MPOFormatUtility.Number(maxParticleCapacity));
            result.SetMetric("Approx. Rate × Lifetime", MPOFormatUtility.Number(estimatedSteadyParticles));
            result.SetMetric("Collision", MPOFormatUtility.Number(collisionEnabled));
            result.SetMetric("Trails", MPOFormatUtility.Number(trailsEnabled));
            result.SetMetric("Lights", MPOFormatUtility.Number(lightsEnabled));
            result.SetMetric("Sub Emitters", MPOFormatUtility.Number(subEmitterSystems));
            result.SetMetric("Mesh Renderers", MPOFormatUtility.Number(meshRenderers));
            result.SetMetric("Skipped Systems", MPOFormatUtility.Number(skippedSystems));
            context.ReportProgress("Particle scan complete", 1f);
        }

        private static float GetMaxCurveValue(ParticleSystem.MinMaxCurve curve)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return Mathf.Max(0f, curve.constant);
                case ParticleSystemCurveMode.TwoConstants:
                    return Mathf.Max(0f, curve.constantMax);
                case ParticleSystemCurveMode.Curve:
                    return Mathf.Max(0f, GetAnimationCurveMax(curve.curve) * curve.curveMultiplier);
                case ParticleSystemCurveMode.TwoCurves:
                    return Mathf.Max(0f, Mathf.Max(GetAnimationCurveMax(curve.curveMin), GetAnimationCurveMax(curve.curveMax)) * curve.curveMultiplier);
                default:
                    return Mathf.Max(0f, curve.constantMax);
            }
        }

        private static float GetAnimationCurveMax(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
                return 0f;

            float max = float.MinValue;
            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
                max = Mathf.Max(max, keys[i].value);
            return max == float.MinValue ? 0f : max;
        }

        private static string SafeObjectName(Object obj)
        {
            try { return obj != null && !string.IsNullOrEmpty(obj.name) ? obj.name : "Unknown"; }
            catch { return "Unknown"; }
        }
    }
}
