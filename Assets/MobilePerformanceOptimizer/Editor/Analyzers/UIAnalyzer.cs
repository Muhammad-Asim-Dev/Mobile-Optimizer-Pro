using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MobilePerformanceOptimizer
{
    public sealed class UIAnalyzer : IMPOIncrementalAnalyzer
    {
        public string Name => "UI / Canvas Analyzer";
        public MPOCategory Category => MPOCategory.UI;

        public void Analyze(MPOScanContext context)
        {
            IEnumerator routine = AnalyzeIncremental(context);
            while (routine != null && routine.MoveNext()) { }
        }

        public IEnumerator AnalyzeIncremental(MPOScanContext context)
        {
            MPOCategoryResult result = context.Result.GetOrCreate(Category);
            List<Canvas> canvases = MPOSceneUtility.GetComponentsInLoadedScenes<Canvas>(context);
            yield return null;
            List<Graphic> graphics = MPOSceneUtility.GetComponentsInLoadedScenes<Graphic>(context);
            if (context.ReportProgress("Collecting UI graphics", 0.08f)) yield break;
            yield return null;
            List<GraphicRaycaster> raycasters = MPOSceneUtility.GetComponentsInLoadedScenes<GraphicRaycaster>(context);
            yield return null;
            List<LayoutGroup> layoutGroups = MPOSceneUtility.GetComponentsInLoadedScenes<LayoutGroup>(context);
            yield return null;
            List<ContentSizeFitter> fitters = MPOSceneUtility.GetComponentsInLoadedScenes<ContentSizeFitter>(context);
            yield return null;
            List<Mask> masks = MPOSceneUtility.GetComponentsInLoadedScenes<Mask>(context);
            yield return null;
            List<RectMask2D> rectMasks = MPOSceneUtility.GetComponentsInLoadedScenes<RectMask2D>(context);
            yield return null;
            List<BaseMeshEffect> meshEffects = MPOSceneUtility.GetComponentsInLoadedScenes<BaseMeshEffect>(context);
            yield return null;

            int skippedUiItems = 0;
            int activeCanvases = CountActive(canvases, context, ref skippedUiItems);
            yield return null;
            int activeRaycasters = CountActive(raycasters, context, ref skippedUiItems);
            yield return null;
            int activeLayouts = CountActive(layoutGroups, context, ref skippedUiItems) + CountActive(fitters, context, ref skippedUiItems);
            yield return null;
            int activeMasks = CountActive(masks, context, ref skippedUiItems) + CountActive(rectMasks, context, ref skippedUiItems);
            yield return null;
            int activeMeshEffects = CountActive(meshEffects, context, ref skippedUiItems);
            yield return null;
            int activeGraphics = 0;
            int raycastTargets = 0;
            int likelyUnneededRaycastTargets = 0;
            int clearlyInteractiveRaycastTargets = 0;
            int nestedLayoutGroups = 0;
            int fitterInLayoutHierarchy = 0;

            int graphicIndex = 0;
            foreach (Graphic graphic in graphics)
            {
                graphicIndex++;
                bool ok = context.TryExecute("UI Graphic: " + SafeObjectName(graphic), () =>
                {
                    if (graphic == null || !graphic.enabled || !graphic.gameObject.activeInHierarchy)
                        return;

                    activeGraphics++;
                    if (!graphic.raycastTarget)
                        return;

                    raycastTargets++;
                    if (LooksInteractive(graphic.gameObject, context))
                        clearlyInteractiveRaycastTargets++;
                    else
                        likelyUnneededRaycastTargets++;
                });
                if (!ok) skippedUiItems++;
                if ((graphicIndex & 31) == 0)
                {
                    if (context.ReportProgress("Reading UI graphics", 0.15f + 0.45f * (graphicIndex / (float)Mathf.Max(1, graphics.Count)))) yield break;
                    yield return null;
                }
            }

            int layoutIndex = 0;
            foreach (LayoutGroup group in layoutGroups)
            {
                layoutIndex++;
                bool ok = context.TryExecute("LayoutGroup: " + SafeObjectName(group), () =>
                {
                    if (group == null || !group.enabled || !group.gameObject.activeInHierarchy)
                        return;

                    Transform parent = group.transform.parent;
                    if (parent != null && parent.GetComponentInParent<LayoutGroup>() != null)
                        nestedLayoutGroups++;
                });
                if (!ok) skippedUiItems++;
                if ((layoutIndex & 15) == 0) yield return null;
            }

            int fitterIndex = 0;
            foreach (ContentSizeFitter fitter in fitters)
            {
                fitterIndex++;
                bool ok = context.TryExecute("ContentSizeFitter: " + SafeObjectName(fitter), () =>
                {
                    if (fitter == null || !fitter.enabled || !fitter.gameObject.activeInHierarchy)
                        return;

                    if (fitter.GetComponent<LayoutGroup>() != null ||
                        (fitter.transform.parent != null && fitter.transform.parent.GetComponentInParent<LayoutGroup>() != null))
                        fitterInLayoutHierarchy++;
                });
                if (!ok) skippedUiItems++;
                if ((fitterIndex & 15) == 0) yield return null;
            }

            if (activeCanvases > context.Profile.MaxCanvases)
            {
                bool excessive = activeCanvases > Mathf.CeilToInt(context.Profile.MaxCanvases * 1.5f);
                result.AddIssue(new MPOIssue(
                    Category,
                    excessive ? MPOSeverity.Warning : MPOSeverity.Suggestion,
                    "Many active Canvases",
                    $"Loaded scenes contain {activeCanvases:N0} active Canvases. Selected guidance is {context.Profile.MaxCanvases:N0}.",
                    "Multiple canvases can intentionally isolate rebuilds, so do not merge them blindly. Review frequently changing canvases and profile Canvas.SendWillRenderCanvases/UI.Rendering before restructuring.",
                    excessive ? 3 : 1,
                    ruleId: "ui.canvas-count",
                    cpuImpact: excessive ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                    gpuImpact: MPOImpactLevel.Low,
                    thermalImpact: excessive ? MPOImpactLevel.Medium : MPOImpactLevel.Low));
            }
            else result.AddPass();

            float unneededRatio = raycastTargets > 0 ? likelyUnneededRaycastTargets / (float)raycastTargets : 0f;
            int likelyThreshold = Mathf.Max(25, context.Profile.MaxRaycastTargets / 3);
            bool raycastConcern = raycastTargets > context.Profile.MaxRaycastTargets &&
                                  likelyUnneededRaycastTargets > likelyThreshold &&
                                  unneededRatio >= 0.4f;
            bool severeRaycastConcern = likelyUnneededRaycastTargets > context.Profile.MaxRaycastTargets;

            if (raycastConcern || severeRaycastConcern)
            {
                var description = new StringBuilder();
                description.AppendLine($"Active UI Graphics: {activeGraphics:N0}");
                description.AppendLine($"Raycast Target enabled: {raycastTargets:N0}");
                description.AppendLine($"Clearly interactive targets: {clearlyInteractiveRaycastTargets:N0}");
                description.Append($"Potentially non-interactive targets: {likelyUnneededRaycastTargets:N0} ({unneededRatio:P0})");

                result.AddIssue(new MPOIssue(
                    Category,
                    severeRaycastConcern ? MPOSeverity.Warning : MPOSeverity.Suggestion,
                    "UI raycast targets need review",
                    description.ToString(),
                    "Disable Raycast Target only on graphics that never receive pointer, drag or scroll input. The detector intentionally requires both a high count and a high non-interactive ratio to reduce false positives; custom event routing can still make a target necessary.",
                    severeRaycastConcern ? 4 : 2,
                    ruleId: "ui.raycast-target-count",
                    cpuImpact: severeRaycastConcern ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                    thermalImpact: MPOImpactLevel.Low));
            }
            else result.AddPass();

            bool layoutCountConcern = activeLayouts > context.Profile.MaxLayoutComponents;
            bool hierarchyConcern = nestedLayoutGroups > Mathf.Max(8, context.Profile.MaxLayoutComponents / 6) ||
                                    fitterInLayoutHierarchy > Mathf.Max(8, context.Profile.MaxLayoutComponents / 6);
            if (layoutCountConcern && hierarchyConcern)
            {
                result.AddIssue(new MPOIssue(
                    Category,
                    MPOSeverity.Warning,
                    "Complex auto-layout hierarchy",
                    $"{activeLayouts:N0} active LayoutGroup/ContentSizeFitter components were found. Nested LayoutGroups: {nestedLayoutGroups:N0}. ContentSizeFitters inside layout hierarchies: {fitterInLayoutHierarchy:N0}.",
                    "Frequent or nested auto-layout rebuilds can be expensive on changing HUDs and scrolling content. Prefer stable layout after initialization where possible and profile UI.Layout markers before rewriting UI.",
                    4,
                    ruleId: "ui.layout-complexity",
                    cpuImpact: MPOImpactLevel.High,
                    memoryImpact: MPOImpactLevel.Low,
                    thermalImpact: MPOImpactLevel.Medium));
            }
            else if (layoutCountConcern)
            {
                result.AddIssue(new MPOIssue(
                    Category,
                    MPOSeverity.Suggestion,
                    "High auto-layout component count",
                    $"{activeLayouts:N0} active LayoutGroup/ContentSizeFitter components were found. The hierarchy does not show enough nesting to classify this as a stronger warning.",
                    "This may be perfectly valid for mostly static menus. Profile UI.Layout during real interactions and optimize only if rebuild cost is visible.",
                    1,
                    ruleId: "ui.layout-count",
                    cpuImpact: MPOImpactLevel.Low));
            }
            else result.AddPass();

            int meshEffectThreshold = context.Profile.DeviceTier == MPODeviceTier.LowEnd ? 18 : 30;
            if (activeMeshEffects > meshEffectThreshold)
            {
                result.AddIssue(new MPOIssue(
                    Category,
                    MPOSeverity.Suggestion,
                    "Many UI mesh effects",
                    $"{activeMeshEffects:N0} active UI mesh effects (such as Shadow/Outline) were found.",
                    "Review repeated Shadow/Outline effects on large or frequently changing UI. They can increase generated UI geometry and overdraw.",
                    2,
                    ruleId: "ui.mesh-effects",
                    cpuImpact: MPOImpactLevel.Low,
                    gpuImpact: MPOImpactLevel.Medium,
                    thermalImpact: MPOImpactLevel.Low));
            }
            else result.AddPass();

            int maskThreshold = context.Profile.DeviceTier == MPODeviceTier.LowEnd ? 24 : 48;
            if (activeMasks > maskThreshold)
            {
                result.AddIssue(new MPOIssue(
                    Category,
                    MPOSeverity.Suggestion,
                    "Many active UI masks",
                    $"{activeMasks:N0} active Mask/RectMask2D components were found.",
                    "Masks are often necessary, especially for ScrollViews. Review deep/nested masking and large masked regions if UI rendering is a measured bottleneck.",
                    1,
                    ruleId: "ui.mask-count",
                    cpuImpact: MPOImpactLevel.Low,
                    gpuImpact: MPOImpactLevel.Low));
            }
            else result.AddPass();

            result.SetMetric("Canvases", MPOFormatUtility.Number(activeCanvases));
            result.SetMetric("Graphics", MPOFormatUtility.Number(activeGraphics));
            result.SetMetric("Raycast Targets", MPOFormatUtility.Number(raycastTargets));
            result.SetMetric("Likely Unneeded Raycasts", MPOFormatUtility.Number(likelyUnneededRaycastTargets));
            result.SetMetric("Raycasters", MPOFormatUtility.Number(activeRaycasters));
            result.SetMetric("Layout Components", MPOFormatUtility.Number(activeLayouts));
            result.SetMetric("Nested Layout Groups", MPOFormatUtility.Number(nestedLayoutGroups));
            result.SetMetric("Fitters In Layout", MPOFormatUtility.Number(fitterInLayoutHierarchy));
            result.SetMetric("Masks", MPOFormatUtility.Number(activeMasks));
            result.SetMetric("Skipped UI Items", MPOFormatUtility.Number(skippedUiItems));
            context.ReportProgress("UI scan complete", 1f);
        }

        private static int CountActive<T>(List<T> components, MPOScanContext context, ref int skipped) where T : Behaviour
        {
            int count = 0;
            foreach (T component in components)
            {
                bool ok = context.TryExecute(typeof(T).Name + ": " + SafeObjectName(component), () =>
                {
                    if (component != null && component.enabled && component.gameObject.activeInHierarchy)
                        count++;
                });
                if (!ok) skipped++;
            }
            return count;
        }

        private static bool LooksInteractive(GameObject go, MPOScanContext context)
        {
            if (go == null)
                return false;

            try
            {
                if (go.GetComponent<Selectable>() != null)
                    return true;

                MonoBehaviour[] behaviours = go.GetComponents<MonoBehaviour>();
                if (behaviours == null)
                    return false;

                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour == null)
                        continue;

                    if (behaviour is IPointerClickHandler ||
                        behaviour is IPointerDownHandler ||
                        behaviour is IPointerUpHandler ||
                        behaviour is IBeginDragHandler ||
                        behaviour is IDragHandler ||
                        behaviour is IEndDragHandler ||
                        behaviour is IScrollHandler)
                        return true;
                }
            }
            catch (System.Exception exception)
            {
                context.RecordRecoverableError("Interactive UI check: " + SafeObjectName(go), exception);
            }

            return false;
        }

        private static string SafeObjectName(Object obj)
        {
            try { return obj != null && !string.IsNullOrEmpty(obj.name) ? obj.name : "Unknown"; }
            catch { return "Unknown"; }
        }
    }
}
