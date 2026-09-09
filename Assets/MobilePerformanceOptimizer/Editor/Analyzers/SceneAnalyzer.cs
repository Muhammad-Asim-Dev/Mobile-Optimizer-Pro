using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public sealed class SceneAnalyzer : IMPOIncrementalAnalyzer
    {
        public string Name => "Scene Analyzer";
        public MPOCategory Category => MPOCategory.Scene;

        public void Analyze(MPOScanContext context)
        {
            IEnumerator routine = AnalyzeIncremental(context);
            while (routine != null && routine.MoveNext()) { }
        }

        public IEnumerator AnalyzeIncremental(MPOScanContext context)
        {
            MPOCategoryResult result = context.Result.GetOrCreate(Category);
            context.ReportProgress("Reading loaded scenes", 0.01f);

            List<Renderer> renderers = MPOSceneUtility.GetComponentsInLoadedScenes<Renderer>(context);
            if (context.ReportProgress("Collecting renderers", 0.05f)) yield break;
            yield return null;
            List<Light> lights = MPOSceneUtility.GetComponentsInLoadedScenes<Light>(context);
            if (context.ReportProgress("Collecting lights", 0.08f)) yield break;
            yield return null;
            List<Camera> cameras = MPOSceneUtility.GetComponentsInLoadedScenes<Camera>(context);
            if (context.ReportProgress("Collecting cameras", 0.11f)) yield break;
            yield return null;
            List<ParticleSystem> particles = MPOSceneUtility.GetComponentsInLoadedScenes<ParticleSystem>(context);
            if (context.ReportProgress("Collecting particles", 0.14f)) yield break;
            yield return null;
            List<Rigidbody> rigidbodies = MPOSceneUtility.GetComponentsInLoadedScenes<Rigidbody>(context);
            yield return null;
            List<Collider> colliders = MPOSceneUtility.GetComponentsInLoadedScenes<Collider>(context);
            yield return null;
            List<Animator> animators = MPOSceneUtility.GetComponentsInLoadedScenes<Animator>(context);
            yield return null;
            List<Canvas> canvases = MPOSceneUtility.GetComponentsInLoadedScenes<Canvas>(context);
            yield return null;

            long activeTriangleCount = 0;
            int skinnedCount = 0;
            int activeRendererCount = 0;
            int activeCameraCount = 0;
            int fullScreenCameraCount = 0;
            int skippedSceneItems = 0;
            var uniqueActiveMaterials = new HashSet<Material>();

            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                bool ok = context.TryExecute("Scene renderer: " + SafeObjectName(renderer), () =>
                {
                    if (renderer == null)
                        return;

                    if (renderer is SkinnedMeshRenderer)
                        skinnedCount++;

                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        return;

                    activeRendererCount++;
                    Material[] materials = renderer.sharedMaterials;
                    if (materials != null)
                    {
                        foreach (Material material in materials)
                        {
                            if (material != null)
                                uniqueActiveMaterials.Add(material);
                        }
                    }

                    if (renderer is SkinnedMeshRenderer skinned)
                    {
                        activeTriangleCount += MPOSceneUtility.GetTriangleCount(skinned.sharedMesh, context, SafeObjectName(renderer) + " mesh");
                    }
                    else
                    {
                        MeshFilter filter = renderer.GetComponent<MeshFilter>();
                        if (filter != null)
                            activeTriangleCount += MPOSceneUtility.GetTriangleCount(filter.sharedMesh, context, SafeObjectName(renderer) + " mesh");
                    }
                });

                if (!ok) skippedSceneItems++;
                if ((i & 31) == 31)
                {
                    if (context.ReportProgress("Reading renderers", 0.15f + 0.35f * ((i + 1f) / Mathf.Max(1, renderers.Count)))) yield break;
                    yield return null;
                }
            }

            int cameraIndex = 0;
            foreach (Camera camera in cameras)
            {
                cameraIndex++;
                bool ok = context.TryExecute("Scene camera: " + SafeObjectName(camera), () =>
                {
                    if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy)
                        return;

                    activeCameraCount++;
                    if (camera.targetTexture == null && camera.rect.width >= 0.95f && camera.rect.height >= 0.95f)
                        fullScreenCameraCount++;
                });
                if (!ok) skippedSceneItems++;
                if ((cameraIndex & 15) == 0)
                {
                    if (context.ReportProgress("Reading cameras", 0.55f)) yield break;
                    yield return null;
                }
            }

            int totalGameObjects = 0;
            foreach (GameObject root in MPOSceneUtility.EnumerateLoadedSceneRoots(context))
            {
                bool ok = context.TryExecute("Scene hierarchy count: " + SafeObjectName(root), () =>
                {
                    Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                    totalGameObjects += transforms != null ? transforms.Length : 0;
                });
                if (!ok) skippedSceneItems++;
                if (context.ReportProgress("Reading scene hierarchy", 0.65f)) yield break;
                yield return null;
            }

            result.SetMetric("GameObjects", MPOFormatUtility.Number(totalGameObjects));
            result.SetMetric("Renderers Loaded", MPOFormatUtility.Number(renderers.Count));
            result.SetMetric("Active Renderers", MPOFormatUtility.Number(activeRendererCount));
            result.SetMetric("Skinned Renderers", MPOFormatUtility.Number(skinnedCount));
            result.SetMetric("Active Triangles", MPOFormatUtility.Number(activeTriangleCount));
            result.SetMetric("Active Materials", MPOFormatUtility.Number(uniqueActiveMaterials.Count));
            result.SetMetric("Lights Loaded", MPOFormatUtility.Number(lights.Count));
            result.SetMetric("Active Cameras", MPOFormatUtility.Number(activeCameraCount));
            result.SetMetric("Full-Screen Cameras", MPOFormatUtility.Number(fullScreenCameraCount));
            result.SetMetric("Particle Systems", MPOFormatUtility.Number(particles.Count));
            result.SetMetric("Rigidbodies", MPOFormatUtility.Number(rigidbodies.Count));
            result.SetMetric("Colliders", MPOFormatUtility.Number(colliders.Count));
            result.SetMetric("Animators", MPOFormatUtility.Number(animators.Count));
            result.SetMetric("Canvases", MPOFormatUtility.Number(canvases.Count));
            result.SetMetric("Skipped Scene Items", MPOFormatUtility.Number(skippedSceneItems));

            bool triangleSevere = activeTriangleCount > context.Profile.MaxLoadedSceneTriangles * 1.5f;
            AddThresholdCheck(
                result,
                activeTriangleCount <= context.Profile.MaxLoadedSceneTriangles,
                new MPOIssue(
                    Category,
                    triangleSevere ? MPOSeverity.Critical : MPOSeverity.Warning,
                    "Active loaded-scene triangle count is high",
                    $"Enabled renderers in loaded scenes reference approximately {activeTriangleCount:N0} triangles. The selected {context.Profile.DisplayName} profile guideline is {context.Profile.MaxLoadedSceneTriangles:N0}.",
                    "Review high-poly meshes, add LODs where appropriate, reduce unseen geometry, improve culling/streaming, and validate the actual bottleneck on target hardware.",
                    triangleSevere ? 10 : 6,
                    ruleId: "scene.active-triangles",
                    cpuImpact: triangleSevere ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                    gpuImpact: triangleSevere ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                    memoryImpact: MPOImpactLevel.Medium,
                    thermalImpact: triangleSevere ? MPOImpactLevel.High : MPOImpactLevel.Medium));

            bool rendererSevere = activeRendererCount > context.Profile.MaxLoadedSceneRenderers * 1.5f;
            AddThresholdCheck(
                result,
                activeRendererCount <= context.Profile.MaxLoadedSceneRenderers,
                new MPOIssue(
                    Category,
                    rendererSevere ? MPOSeverity.Critical : MPOSeverity.Warning,
                    "Active renderer count is high",
                    $"Loaded scenes contain {activeRendererCount:N0} enabled Renderers. The selected profile guideline is {context.Profile.MaxLoadedSceneRenderers:N0}.",
                    "Check whether distant content can be streamed, pooled, disabled, combined where appropriate, or culled more aggressively. Renderer count is a workload hint, not a draw-call count.",
                    rendererSevere ? 7 : 5,
                    ruleId: "scene.active-renderers",
                    cpuImpact: rendererSevere ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                    gpuImpact: MPOImpactLevel.Medium,
                    memoryImpact: MPOImpactLevel.Low,
                    thermalImpact: rendererSevere ? MPOImpactLevel.High : MPOImpactLevel.Medium));

            int cameraGuideline = context.Profile.DeviceTier == MPODeviceTier.LowEnd ? 1 : 2;
            bool cameraConcern = fullScreenCameraCount > cameraGuideline;
            AddThresholdCheck(
                result,
                !cameraConcern,
                new MPOIssue(
                    Category,
                    fullScreenCameraCount > cameraGuideline + 2 ? MPOSeverity.Critical : MPOSeverity.Warning,
                    "Multiple full-screen cameras are active",
                    $"{activeCameraCount} Camera component(s) are active; {fullScreenCameraCount} appear to render to the screen with near-full viewport rects. The selected tier guideline is {cameraGuideline} full-screen camera(s).",
                    "Confirm every active full-screen camera is necessary. Additional cameras can repeat culling and rendering work. Cameras that render to textures or partial viewports are counted separately in the metric but do not trigger this rule by themselves.",
                    fullScreenCameraCount > cameraGuideline + 2 ? 7 : 3,
                    ruleId: "scene.full-screen-cameras",
                    cpuImpact: MPOImpactLevel.Medium,
                    gpuImpact: fullScreenCameraCount > cameraGuideline + 2 ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                    thermalImpact: MPOImpactLevel.Medium));

            context.ReportProgress("Scene scan complete", 1f);
        }

        private static void AddThresholdCheck(MPOCategoryResult result, bool passed, MPOIssue issue)
        {
            if (passed) result.AddPass();
            else result.AddIssue(issue);
        }

        private static string SafeObjectName(Object obj)
        {
            try { return obj != null && !string.IsNullOrEmpty(obj.name) ? obj.name : "Unknown"; }
            catch { return "Unknown"; }
        }
    }
}
