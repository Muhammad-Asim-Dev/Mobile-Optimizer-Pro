using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public sealed class PhysicsAnalyzer : IMPOIncrementalAnalyzer
    {
        public string Name => "Physics Analyzer";
        public MPOCategory Category => MPOCategory.Physics;

        public void Analyze(MPOScanContext context)
        {
            IEnumerator routine = AnalyzeIncremental(context);
            while (routine != null && routine.MoveNext()) { }
        }

        public IEnumerator AnalyzeIncremental(MPOScanContext context)
        {
            MPOCategoryResult result = context.Result.GetOrCreate(Category);
            List<Rigidbody> rigidbodies = MPOSceneUtility.GetComponentsInLoadedScenes<Rigidbody>(context);
            yield return null;
            List<Rigidbody2D> rigidbodies2D = MPOSceneUtility.GetComponentsInLoadedScenes<Rigidbody2D>(context);
            yield return null;
            List<MeshCollider> meshColliders = MPOSceneUtility.GetComponentsInLoadedScenes<MeshCollider>(context);
            yield return null;
            List<Collider> colliders = MPOSceneUtility.GetComponentsInLoadedScenes<Collider>(context);
            yield return null;
            List<Collider2D> colliders2D = MPOSceneUtility.GetComponentsInLoadedScenes<Collider2D>(context);
            yield return null;

            int dynamic3D = 0;
            int dynamic2D = 0;
            int activeMeshColliders = 0;
            int nonConvexMeshColliders = 0;
            int meshCollidersOnDynamicBodies = 0;
            int skippedPhysicsItems = 0;

            int rbIndex = 0;
            foreach (Rigidbody rb in rigidbodies)
            {
                rbIndex++;
                bool ok = context.TryExecute("Rigidbody: " + SafeObjectName(rb), () =>
                {
                    if (rb != null && rb.gameObject.activeInHierarchy && !rb.isKinematic)
                        dynamic3D++;
                });
                if (!ok) skippedPhysicsItems++;
                if ((rbIndex & 31) == 0) yield return null;
            }

            int rb2DIndex = 0;
            foreach (Rigidbody2D rb in rigidbodies2D)
            {
                rb2DIndex++;
                bool ok = context.TryExecute("Rigidbody2D: " + SafeObjectName(rb), () =>
                {
                    if (rb != null && rb.gameObject.activeInHierarchy && rb.bodyType == RigidbodyType2D.Dynamic)
                        dynamic2D++;
                });
                if (!ok) skippedPhysicsItems++;
                if ((rb2DIndex & 31) == 0) yield return null;
            }

            int meshColliderIndex = 0;
            foreach (MeshCollider collider in meshColliders)
            {
                meshColliderIndex++;
                bool ok = context.TryExecute("MeshCollider: " + SafeObjectName(collider), () =>
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                        return;

                    activeMeshColliders++;
                    if (!collider.convex)
                        nonConvexMeshColliders++;
                    if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic)
                        meshCollidersOnDynamicBodies++;
                });
                if (!ok) skippedPhysicsItems++;
                if ((meshColliderIndex & 31) == 0) yield return null;
            }

            int dynamicTotal = dynamic3D + dynamic2D;
            if (dynamicTotal > context.Profile.MaxDynamicRigidbodies)
            {
                bool severe = dynamicTotal > context.Profile.MaxDynamicRigidbodies * 2;
                result.AddIssue(new MPOIssue(
                    Category,
                    severe ? MPOSeverity.Critical : MPOSeverity.Warning,
                    "High number of dynamic physics bodies",
                    $"Loaded scenes contain {dynamicTotal:N0} active dynamic bodies ({dynamic3D:N0} 3D, {dynamic2D:N0} 2D). Selected guidance is {context.Profile.MaxDynamicRigidbodies:N0}.",
                    "Pool/sleep bodies where possible, prefer simpler movement for decorative objects, and profile Physics.Simulate/Physics2D.Simulate on target devices.",
                    severe ? 6 : 4,
                    ruleId: "physics.dynamic-body-count",
                    cpuImpact: severe ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                    memoryImpact: MPOImpactLevel.Low,
                    thermalImpact: severe ? MPOImpactLevel.High : MPOImpactLevel.Medium));
            }
            else result.AddPass();

            if (activeMeshColliders > context.Profile.MaxMeshColliders)
            {
                bool severe = activeMeshColliders > context.Profile.MaxMeshColliders * 2;
                result.AddIssue(new MPOIssue(
                    Category,
                    severe ? MPOSeverity.Critical : MPOSeverity.Warning,
                    "Many MeshColliders are active",
                    $"{activeMeshColliders:N0} active MeshColliders were found; {nonConvexMeshColliders:N0} are non-convex and {meshCollidersOnDynamicBodies:N0} are attached to non-kinematic rigidbodies. Selected guidance is {context.Profile.MaxMeshColliders:N0}.",
                    "Use primitive/compound colliders for moving or frequently queried objects where acceptable. Detailed MeshColliders are generally better reserved for static geometry that genuinely needs them.",
                    severe ? 6 : 4,
                    ruleId: "physics.mesh-collider-count",
                    cpuImpact: severe ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                    memoryImpact: MPOImpactLevel.Medium,
                    thermalImpact: severe ? MPOImpactLevel.High : MPOImpactLevel.Medium));
            }
            else result.AddPass();

            if (meshCollidersOnDynamicBodies > 0)
            {
                result.AddIssue(new MPOIssue(
                    Category,
                    meshCollidersOnDynamicBodies > 6 ? MPOSeverity.Warning : MPOSeverity.Suggestion,
                    "MeshColliders are used on dynamic rigidbodies",
                    $"{meshCollidersOnDynamicBodies:N0} active MeshCollider(s) are attached to non-kinematic Rigidbody objects.",
                    "Dynamic mesh collision can be substantially more expensive than primitive/compound collision. Keep it only where the collision shape is necessary and profile the gameplay case.",
                    meshCollidersOnDynamicBodies > 6 ? 4 : 1,
                    ruleId: "physics.dynamic-mesh-collider",
                    cpuImpact: meshCollidersOnDynamicBodies > 6 ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                    thermalImpact: MPOImpactLevel.Medium));
            }
            else result.AddPass();

            float fixedDelta = Time.fixedDeltaTime;
            if (fixedDelta + 0.00001f < context.Profile.MinRecommendedFixedDeltaTime)
            {
                float frequency = fixedDelta > 0f ? 1f / fixedDelta : 0f;
                var issue = new MPOIssue(
                    Category,
                    MPOSeverity.Warning,
                    "Very frequent physics timestep",
                    $"Fixed Timestep is {fixedDelta:0.####} s (~{frequency:0.#} Hz). Selected mobile guidance is not faster than ~{1f / context.Profile.MinRecommendedFixedDeltaTime:0.#} Hz unless the game needs it.",
                    "A smaller timestep increases physics simulations per second. Validate gameplay requirements before increasing Fixed Timestep.",
                    4,
                    assetPath: "ProjectSettings/TimeManager.asset",
                    ruleId: "physics.fixed-timestep",
                    fixKind: MPOFixKind.ReviewSettings, fixSafety: MPOFixSafety.ReviewRequired,
                    cpuImpact: MPOImpactLevel.High,
                    thermalImpact: MPOImpactLevel.High);
                issue.SettingRecommendations["Fixed Timestep"] = context.Profile.MinRecommendedFixedDeltaTime;
                result.AddIssue(issue);
            }
            else result.AddPass();

            int active3DColliders = 0;
            int collider3DIndex = 0;
            foreach (Collider collider in colliders)
            {
                collider3DIndex++;
                bool ok = context.TryExecute("Collider: " + SafeObjectName(collider), () =>
                {
                    if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                        active3DColliders++;
                });
                if (!ok) skippedPhysicsItems++;
                if ((collider3DIndex & 63) == 0) yield return null;
            }

            int active2DColliders = 0;
            int collider2DIndex = 0;
            foreach (Collider2D collider in colliders2D)
            {
                collider2DIndex++;
                bool ok = context.TryExecute("Collider2D: " + SafeObjectName(collider), () =>
                {
                    if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                        active2DColliders++;
                });
                if (!ok) skippedPhysicsItems++;
                if ((collider2DIndex & 63) == 0) yield return null;
            }

            result.SetMetric("Dynamic Bodies", MPOFormatUtility.Number(dynamicTotal));
            result.SetMetric("3D Colliders", MPOFormatUtility.Number(active3DColliders));
            result.SetMetric("2D Colliders", MPOFormatUtility.Number(active2DColliders));
            result.SetMetric("MeshColliders", MPOFormatUtility.Number(activeMeshColliders));
            result.SetMetric("Dynamic MeshColliders", MPOFormatUtility.Number(meshCollidersOnDynamicBodies));
            result.SetMetric("Fixed Timestep", fixedDelta.ToString("0.####") + " s");
            result.SetMetric("Skipped Physics Items", MPOFormatUtility.Number(skippedPhysicsItems));
            context.ReportProgress("Physics scan complete", 1f);
        }

        private static string SafeObjectName(Object obj)
        {
            try { return obj != null && !string.IsNullOrEmpty(obj.name) ? obj.name : "Unknown"; }
            catch { return "Unknown"; }
        }
    }
}
