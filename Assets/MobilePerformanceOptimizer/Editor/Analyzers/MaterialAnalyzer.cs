using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MobilePerformanceOptimizer
{
    public sealed class MaterialAnalyzer : IMPOIncrementalAnalyzer
    {
        public string Name => "Material Analyzer";
        public MPOCategory Category => MPOCategory.Materials;

        public void Analyze(MPOScanContext context)
        {
            IEnumerator routine = AnalyzeIncremental(context);
            while (routine != null && routine.MoveNext()) { }
        }

        public IEnumerator AnalyzeIncremental(MPOScanContext context)
        {
            MPOCategoryResult result = context.Result.GetOrCreate(Category);
            string[] guids = AssetDatabase.FindAssets("t:Material", MPOAssetScope.GetFindRoots(context.Scope));
            yield return null;

            var duplicateGroups = new Dictionary<string, List<Material>>();
            Dictionary<Material, int> loadedUsage = CollectLoadedSceneMaterialUsage(context);
            yield return null;

            int scanned = 0;
            int transparentMaterials = 0;
            int shaderProblems = 0;
            int instancingCandidates = 0;
            int skippedMaterials = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                if (context.ReportProgress($"{i + 1}/{guids.Length}", guids.Length == 0 ? 1f : i / (float)guids.Length))
                    yield break;

                string path;
                if (!context.TryGet("Material GUID " + guids[i], () => AssetDatabase.GUIDToAssetPath(guids[i]), out path) ||
                    !MPOAssetScope.ShouldScan(path, context.Scope))
                {
                    if ((i & 7) == 7)
                        yield return null;
                    continue;
                }

                bool succeeded = context.TryExecute("Material asset: " + path, () =>
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        context.RecordRecoverableWarning(path, "Material could not be loaded and was skipped.");
                        return;
                    }

                    scanned++;
                    int usageCount = loadedUsage.TryGetValue(material, out int count) ? count : 0;
                    Shader shader = null;
                    bool shaderMissing = false;
                    bool shaderCompileError = false;
                    string shaderName = "Missing";
                    string shaderErrorSummary = string.Empty;

                    try
                    {
                        shader = material.shader;
                        shaderMissing = shader == null;
                        if (!shaderMissing)
                        {
                            shaderName = shader.name;

                            // Compile diagnostics are intentionally limited to materials currently used by loaded scenes.
                            // Other inexpensive material checks still run for the complete selected scan scope.
                            if (usageCount > 0 && !ShaderUtil.anythingCompiling)
                            {
                                shaderCompileError = ShaderUtil.ShaderHasError(shader);
                                if (shaderCompileError)
                                    shaderErrorSummary = GetFirstShaderError(shader);
                            }
                        }
                    }
                    catch (Exception shaderException)
                    {
                        context.RecordRecoverableError(path + " shader state", shaderException);
                        shaderCompileError = true;
                        shaderErrorSummary = "Shader diagnostics could not be read safely.";
                    }

                    int renderQueue = -1;
                    bool transparent = false;
                    if (!shaderMissing)
                    {
                        try
                        {
                            renderQueue = material.renderQueue;
                            transparent = renderQueue >= (int)RenderQueue.Transparent;
                        }
                        catch (Exception renderQueueException)
                        {
                            context.RecordRecoverableError(path + " render queue", renderQueueException);
                        }
                    }

                    if (transparent) transparentMaterials++;
                    if (shaderMissing || shaderCompileError) shaderProblems++;

                    int issuesAdded = 0;

                    if (shaderMissing || shaderCompileError)
                    {
                        string detail = $"Material: {SafeObjectName(material)}\nShader: {shaderName}";
                        if (!string.IsNullOrWhiteSpace(shaderErrorSummary))
                            detail += "\nCompiler: " + shaderErrorSummary;

                        result.AddIssue(new MPOIssue(
                            Category,
                            MPOSeverity.Critical,
                            shaderMissing ? "Material shader is missing" : "Material shader has compile errors",
                            detail,
                            shaderMissing
                                ? "Assign a valid URP-compatible shader and verify the material visually."
                                : "Fix the shader compiler error or replace the shader. The rest of the scan continues even when a shader is broken.",
                            8,
                            null,
                            path,
                            "materials.shader-support",
                            cpuImpact: MPOImpactLevel.Low,
                            gpuImpact: MPOImpactLevel.High,
                            memoryImpact: MPOImpactLevel.Low,
                            thermalImpact: MPOImpactLevel.Medium));
                        issuesAdded++;
                    }

                    if (transparent && !shaderMissing && !shaderCompileError)
                    {
                        bool repeated = usageCount >= (context.Profile.DeviceTier == MPODeviceTier.LowEnd ? 4 : 8);
                        result.AddIssue(new MPOIssue(
                            Category,
                            repeated ? MPOSeverity.Warning : MPOSeverity.Suggestion,
                            "Transparent material may increase overdraw",
                            $"Material: {SafeObjectName(material)}\nRender queue: {renderQueue}\nLoaded-scene references: {usageCount:N0}",
                            usageCount > 0
                                ? "Transparency can be expensive on mobile when layers overlap. Review screen coverage and overdraw for this material on target hardware."
                                : "This transparent material is in the selected project scope. Review it when it is used by gameplay content; transparency itself is not automatically a problem.",
                            repeated ? 3 : 1,
                            null,
                            path,
                            "materials.transparent",
                            gpuImpact: repeated ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                            thermalImpact: repeated ? MPOImpactLevel.Medium : MPOImpactLevel.Low));
                        issuesAdded++;
                    }

                    bool isVariant = false;
                    try { isVariant = material.isVariant; } catch { }

                    bool instancingCandidate = !shaderMissing && !shaderCompileError && !transparent && !isVariant &&
                                               !material.enableInstancing && MPOMaterialOptimizationUtility.SupportsGpuInstancing(material) &&
                                               IsLikelyInstancingCandidate(shader, usageCount);
                    if (instancingCandidate)
                    {
                        instancingCandidates++;
                        bool repeated = usageCount >= 2;
                        result.AddIssue(new MPOIssue(
                            Category,
                            repeated ? MPOSeverity.Warning : MPOSeverity.Suggestion,
                            "GPU Instancing is disabled",
                            $"Material: {SafeObjectName(material)}\nShader: {shaderName}\nGPU Instancing: Off\nLoaded-scene references: {usageCount:N0}",
                            repeated
                                ? "This material is reused by multiple loaded renderers. Enable GPU Instancing, then validate draw calls with the Unity Profiler/Frame Debugger."
                                : "This material uses a common instancing-capable shader. Enable GPU Instancing if the same mesh/material is rendered repeatedly; otherwise the setting may have no measurable benefit.",
                            repeated ? 3 : 1,
                            null,
                            path,
                            "materials.gpu-instancing",
                            MPOFixKind.EnableMaterialGpuInstancing,
                            MPOFixSafety.ReviewRequired,
                            "GPU Instancing: OFF → ON\n\nThis is most useful when repeated MeshRenderers share the same mesh and material. Validate batching/draw calls after applying.",
                            cpuImpact: repeated ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                            gpuImpact: MPOImpactLevel.Low,
                            thermalImpact: repeated ? MPOImpactLevel.Low : MPOImpactLevel.None));
                        issuesAdded++;
                    }

                    if (issuesAdded == 0)
                        result.AddPass();

                    // Duplicate signatures stay limited to materials that are actually referenced by loaded scenes.
                    // This keeps large Asset Store libraries responsive while still catching practical duplicates.
                    if (!shaderMissing && !shaderCompileError && usageCount > 0 && TryBuildSignature(material, out string signature) && !string.IsNullOrEmpty(signature))
                    {
                        if (!duplicateGroups.TryGetValue(signature, out List<Material> group))
                        {
                            group = new List<Material>();
                            duplicateGroups.Add(signature, group);
                        }
                        group.Add(material);
                    }
                });

                if (!succeeded)
                    skippedMaterials++;

                if ((i & 3) == 3)
                    yield return null;
            }

            int duplicateCandidates = 0;
            foreach (KeyValuePair<string, List<Material>> pair in duplicateGroups)
            {
                context.TryExecute("Duplicate material group", () =>
                {
                    if (pair.Value == null || pair.Value.Count < 2)
                        return;

                    List<Material> valid = pair.Value.Where(material => material != null).ToList();
                    if (valid.Count < 2)
                        return;

                    duplicateCandidates += valid.Count;
                    Material first = valid[0];
                    List<string> visibleNames = valid.Take(8).Select(SafeObjectName).ToList();
                    string names = string.Join(", ", visibleNames);
                    if (valid.Count > visibleNames.Count)
                        names += $", … +{valid.Count - visibleNames.Count} more";

                    result.AddIssue(new MPOIssue(
                        Category,
                        MPOSeverity.Suggestion,
                        "Potential duplicate materials found",
                        $"{valid.Count:N0} loaded-scene material assets serialize to the same material configuration:\n{names}",
                        "Verify that they are intentionally separate. If they are genuinely identical and interchangeable, sharing one material can simplify content and improve batching consistency. Do not merge automatically.",
                        valid.Count >= 8 ? 2 : 1,
                        first,
                        AssetDatabase.GetAssetPath(first),
                        "materials.duplicate-candidate",
                        cpuImpact: MPOImpactLevel.Low,
                        gpuImpact: MPOImpactLevel.Low,
                        memoryImpact: valid.Count >= 8 ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                        buildSizeImpact: valid.Count >= 8 ? MPOImpactLevel.Medium : MPOImpactLevel.Low));
                });

                yield return null;
            }

            List<Renderer> renderers = MPOSceneUtility.GetComponentsInLoadedScenes<Renderer>(context);
            int highSlotRenderers = 0;
            int rendererIndex = 0;
            foreach (Renderer renderer in renderers)
            {
                rendererIndex++;
                context.TryExecute("Renderer material slots: " + SafeObjectName(renderer), () =>
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        return;

                    Material[] sharedMaterials = renderer.sharedMaterials;
                    int slots = sharedMaterials != null ? sharedMaterials.Length : 0;
                    if (slots <= context.Profile.MaxMaterialsPerRenderer)
                        return;

                    highSlotRenderers++;
                    bool severe = slots > context.Profile.MaxMaterialsPerRenderer * 2;
                    result.AddIssue(new MPOIssue(
                        Category,
                        severe ? MPOSeverity.Warning : MPOSeverity.Suggestion,
                        "Active renderer has many material slots",
                        $"'{SafeObjectName(renderer)}' uses {slots} material slots. The selected profile guideline is {context.Profile.MaxMaterialsPerRenderer}.",
                        "Review whether submeshes/material slots can be reduced without changing the required visual result. Material slots can translate to additional rendering work when visible.",
                        severe ? 3 : 1,
                        renderer,
                        ruleId: "materials.renderer-slots",
                        cpuImpact: severe ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                        gpuImpact: severe ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                        thermalImpact: severe ? MPOImpactLevel.Medium : MPOImpactLevel.Low));
                });

                if ((rendererIndex & 15) == 0)
                    yield return null;
            }

            result.SetMetric("Materials Scanned", MPOFormatUtility.Number(scanned));
            result.SetMetric("Transparent Materials", MPOFormatUtility.Number(transparentMaterials));
            result.SetMetric("GPU Instancing Candidates", MPOFormatUtility.Number(instancingCandidates));
            result.SetMetric("Shader Problems", MPOFormatUtility.Number(shaderProblems));
            result.SetMetric("Skipped Materials", MPOFormatUtility.Number(skippedMaterials));
            result.SetMetric("Duplicate Candidates", MPOFormatUtility.Number(duplicateCandidates));
            result.SetMetric("High Slot Renderers", MPOFormatUtility.Number(highSlotRenderers));
            context.ReportProgress("Material scan complete", 1f);
        }

        private static bool IsLikelyInstancingCandidate(Shader shader, int loadedUsageCount)
        {
            if (shader == null)
                return false;

            string name = shader.name ?? string.Empty;
            string lower = name.ToLowerInvariant();

            if (lower.Contains("ui/") || lower.Contains("sprite") || lower.Contains("particle") ||
                lower.Contains("skybox") || lower.Contains("decal") || lower.Contains("unlit"))
                return false;

            // Common Unity/URP shaders are known practical candidates. For custom shaders we only surface
            // the recommendation when the material is actually repeated in loaded scenes.
            if (name == "Standard" || name == "Standard (Specular setup)")
                return true;
            if (name.StartsWith("Universal Render Pipeline/Lit", StringComparison.Ordinal) ||
                name.StartsWith("Universal Render Pipeline/Simple Lit", StringComparison.Ordinal) ||
                name.StartsWith("Universal Render Pipeline/Complex Lit", StringComparison.Ordinal) ||
                name.StartsWith("Universal Render Pipeline/Nature/", StringComparison.Ordinal))
                return true;

            return loadedUsageCount >= 2;
        }

        private static Dictionary<Material, int> CollectLoadedSceneMaterialUsage(MPOScanContext context)
        {
            var usage = new Dictionary<Material, int>();
            List<Renderer> renderers = MPOSceneUtility.GetComponentsInLoadedScenes<Renderer>(context);
            foreach (Renderer renderer in renderers)
            {
                context.TryExecute("Loaded material usage: " + SafeObjectName(renderer), () =>
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        return;

                    Material[] materials = renderer.sharedMaterials;
                    if (materials == null)
                        return;

                    foreach (Material material in materials)
                    {
                        if (material == null)
                            continue;

                        usage.TryGetValue(material, out int count);
                        usage[material] = count + 1;
                    }
                });
            }
            return usage;
        }

        private static bool TryBuildSignature(Material material, out string signature)
        {
            signature = string.Empty;
            if (material == null)
                return false;

            try
            {
                string json = EditorJsonUtility.ToJson(material, false);
                if (string.IsNullOrEmpty(json))
                    return false;

                json = Regex.Replace(json, "\\\"m_Name\\\":\\\".*?\\\",?", string.Empty);
                json = Regex.Replace(json, "\\\"m_ObjectHideFlags\\\":-?\\d+,?", string.Empty);
                signature = json;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetFirstShaderError(Shader shader)
        {
            if (shader == null)
                return string.Empty;

            try
            {
                var messages = ShaderUtil.GetShaderMessages(shader);
                if (messages == null || messages.Length == 0)
                    return "Shader compiler reported an error.";

                for (int i = 0; i < messages.Length; i++)
                {
                    string severity = messages[i].severity.ToString();
                    if (!string.Equals(severity, "Error", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string message = messages[i].message ?? "Shader compiler error";
                    return message.Length > 240 ? message.Substring(0, 240) + "…" : message;
                }

                string fallback = messages[0].message ?? "Shader compiler reported an error.";
                return fallback.Length > 240 ? fallback.Substring(0, 240) + "…" : fallback;
            }
            catch
            {
                return "Shader compiler reported an error.";
            }
        }

        private static string SafeObjectName(UnityEngine.Object obj)
        {
            try
            {
                return obj != null && !string.IsNullOrEmpty(obj.name) ? obj.name : "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
