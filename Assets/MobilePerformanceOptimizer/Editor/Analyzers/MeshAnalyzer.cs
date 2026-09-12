using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public sealed class MeshAnalyzer : IMPOIncrementalAnalyzer
    {
        public string Name => "Mesh Analyzer";
        public MPOCategory Category => MPOCategory.Meshes;

        public void Analyze(MPOScanContext context)
        {
            IEnumerator routine = AnalyzeIncremental(context);
            while (routine != null && routine.MoveNext()) { }
        }

        public IEnumerator AnalyzeIncremental(MPOScanContext context)
        {
            MPOCategoryResult result = context.Result.GetOrCreate(Category);
            HashSet<string> paths = CollectMeshAssetPaths(context);
            yield return null;
            HashSet<Mesh> lodMeshesInLoadedScenes = CollectLodMeshesInLoadedScenes(context);
            yield return null;
            Dictionary<Mesh, int> loadedMeshBoneCounts = new Dictionary<Mesh, int>();
            HashSet<Mesh> loadedMeshes = CollectLoadedMeshes(context, loadedMeshBoneCounts);
            yield return null;

            int scanned = 0;
            int highPoly = 0;
            int readableMeshes = 0;
            int loadedHighPolyWithoutLod = 0;
            int highBoneMeshes = 0;
            int highBlendShapeMeshes = 0;
            int skippedPaths = 0;

            int pathIndex = 0;
            foreach (string path in paths)
            {
                pathIndex++;
                if (context.ReportProgress($"{pathIndex}/{paths.Count}", paths.Count == 0 ? 1f : pathIndex / (float)paths.Count))
                    yield break;

                if (!MPOAssetScope.ShouldScan(path, context.Scope))
                {
                    yield return null;
                    continue;
                }

                Object[] allAssets;
                bool pathLoaded = context.TryGet(
                    "Mesh asset path: " + path,
                    () => AssetDatabase.LoadAllAssetsAtPath(path),
                    out allAssets);

                if (!pathLoaded || allAssets == null || allAssets.Length == 0)
                {
                    if (pathLoaded)
                        context.RecordRecoverableWarning(path, "No readable mesh/model sub-assets were returned.");
                    skippedPaths++;
                    yield return null;
                    continue;
                }

                int meshSubAssetIndex = 0;
                foreach (Object asset in allAssets)
                {
                    if (!(asset is Mesh mesh))
                        continue;

                    meshSubAssetIndex++;
                    bool meshOk = context.TryExecute("Mesh sub-asset: " + path + " / " + SafeObjectName(mesh), () =>
                    {
                            scanned++;
                            long triangles = MPOSceneUtility.GetTriangleCount(mesh, context, path + " triangle data");
                            bool usedInLoadedScene = loadedMeshes.Contains(mesh);
                            bool isHighPoly = triangles > context.Profile.MaxMeshTriangles;
                            ModelImporter modelImporter = AssetImporter.GetAtPath(path) as ModelImporter;
                            bool isReadable = modelImporter != null ? modelImporter.isReadable : mesh.isReadable;
                            bool shouldHaveLod = triangles > context.Profile.HighPolyMeshLodThreshold;
                            bool usedByLoadedLod = lodMeshesInLoadedScenes.Contains(mesh);
                            bool lodConcern = usedInLoadedScene && shouldHaveLod && !usedByLoadedLod;
                            bool manySubmeshes = mesh.subMeshCount > context.Profile.MaxMaterialsPerRenderer;
                            int boneCount = loadedMeshBoneCounts.TryGetValue(mesh, out int loadedBones) ? loadedBones : 0;
                            bool highBones = boneCount > context.Profile.MaxSkinnedMeshBones;
                            bool highBlendShapes = mesh.blendShapeCount > context.Profile.MaxBlendShapes;

                            if (isHighPoly) highPoly++;
                            if (isReadable) readableMeshes++;
                            if (lodConcern) loadedHighPolyWithoutLod++;
                            if (highBones) highBoneMeshes++;
                            if (highBlendShapes) highBlendShapeMeshes++;

                            if (!isHighPoly && !isReadable && !lodConcern && !manySubmeshes && !highBones && !highBlendShapes)
                            {
                                result.AddPass();
                                return;
                            }

                            var details = new StringBuilder();
                            details.AppendLine($"Vertices: {mesh.vertexCount:N0}");
                            details.AppendLine($"Triangles: {triangles:N0}");
                            details.AppendLine($"Submeshes: {mesh.subMeshCount}");
                            details.AppendLine($"Blend Shapes: {mesh.blendShapeCount}");
                            if (boneCount > 0)
                                details.AppendLine($"Loaded skinned renderer bones: {boneCount:N0}");
                            details.AppendLine($"Read/Write: {MPOFormatUtility.Bool(isReadable)}");
                            details.AppendLine($"Used by active renderer in loaded scenes: {MPOFormatUtility.Bool(usedInLoadedScene)}");
                            details.Append($"Used by LODGroup in loaded scenes: {MPOFormatUtility.Bool(usedByLoadedLod)}");

                            var recommendation = new StringBuilder();
                            MPOSeverity severity = MPOSeverity.Suggestion;
                            int penalty = 1;

                            if (isHighPoly)
                            {
                                bool veryHigh = triangles > context.Profile.MaxMeshTriangles * 2L;
                                if (usedInLoadedScene)
                                    severity = veryHigh ? MPOSeverity.Critical : MPOSeverity.Warning;

                                penalty += usedInLoadedScene ? (veryHigh ? 6 : 4) : 1;
                                recommendation.AppendLine("• Review polygon density relative to real on-screen size and target device. High source density is most important when this mesh is actually rendered during gameplay.");
                            }

                            if (lodConcern)
                            {
                                if ((int)severity < (int)MPOSeverity.Warning) severity = MPOSeverity.Warning;
                                penalty += 2;
                                recommendation.AppendLine("• This high-detail mesh is actively used but is not referenced by a loaded-scene LODGroup. Consider LODs when its screen size changes significantly.");
                            }

                            if (isReadable)
                            {
                                if (usedInLoadedScene && (int)severity < (int)MPOSeverity.Warning) severity = MPOSeverity.Warning;
                                penalty += 1;
                                recommendation.AppendLine("• Disable model Read/Write if runtime code does not read or modify CPU-side mesh data.");
                            }

                            if (manySubmeshes)
                            {
                                if (usedInLoadedScene && (int)severity < (int)MPOSeverity.Warning) severity = MPOSeverity.Warning;
                                penalty += 1;
                                recommendation.AppendLine("• Review the submesh/material-slot count. Multiple submeshes can require additional draw work when rendered.");
                            }

                            if (highBones)
                            {
                                if ((int)severity < (int)MPOSeverity.Warning) severity = MPOSeverity.Warning;
                                penalty += 2;
                                recommendation.AppendLine($"• Loaded skinned usage exceeds the {context.Profile.MaxSkinnedMeshBones}-bone guidance for this device tier. Review rig complexity and active skinned characters.");
                            }

                            if (highBlendShapes)
                            {
                                if (usedInLoadedScene && (int)severity < (int)MPOSeverity.Warning) severity = MPOSeverity.Warning;
                                penalty += 1;
                                recommendation.AppendLine("• Many blend shapes can increase mesh data and deformation cost. Keep only shapes that are required at runtime.");
                            }

                            bool modelImporterCanFix = isReadable && AssetImporter.GetAtPath(path) is ModelImporter;
                            MPOFixKind fixKind = modelImporterCanFix ? MPOFixKind.DisableMeshReadWrite : MPOFixKind.None;
                            MPOFixSafety fixSafety = modelImporterCanFix ? MPOFixSafety.ReviewRequired : MPOFixSafety.Manual;
                            string fixPreview = modelImporterCanFix
                                ? "Model Read/Write: ON → OFF\n\nThis can reduce CPU-side mesh memory, but runtime mesh modification, some procedural systems, and scripts that read mesh data may require Read/Write."
                                : string.Empty;

                            MPOImpactLevel gpuImpact = usedInLoadedScene && (isHighPoly || highBones || highBlendShapes)
                                ? (triangles > context.Profile.MaxMeshTriangles * 2L || highBones ? MPOImpactLevel.High : MPOImpactLevel.Medium)
                                : isHighPoly ? MPOImpactLevel.Low : MPOImpactLevel.None;
                            MPOImpactLevel cpuImpact = highBones || (isReadable && usedInLoadedScene)
                                ? MPOImpactLevel.Medium
                                : manySubmeshes && usedInLoadedScene ? MPOImpactLevel.Low : MPOImpactLevel.None;
                            MPOImpactLevel memoryImpact = isReadable || highBlendShapes
                                ? (isReadable && usedInLoadedScene ? MPOImpactLevel.Medium : MPOImpactLevel.Low)
                                : MPOImpactLevel.None;

                            result.AddIssue(new MPOIssue(
                                Category,
                                severity,
                                isHighPoly ? "Mesh complexity needs mobile review" : "Mesh import/runtime settings need review",
                                details.ToString(),
                                recommendation.ToString().TrimEnd(),
                                Mathf.Clamp(penalty, 1, 9),
                                null,
                                path,
                                "meshes.mobile-review",
                                fixKind,
                                fixSafety,
                                fixPreview,
                                cpuImpact: cpuImpact,
                                gpuImpact: gpuImpact,
                                memoryImpact: memoryImpact,
                                buildSizeImpact: isHighPoly || highBlendShapes ? MPOImpactLevel.Medium : MPOImpactLevel.Low,
                                thermalImpact: (int)gpuImpact >= (int)MPOImpactLevel.Medium ? MPOImpactLevel.Medium : MPOImpactLevel.Low));
                    });

                    if (!meshOk)
                        skippedPaths++;

                    if ((meshSubAssetIndex & 7) == 0)
                        yield return null;
                }

                // Loading a model path can be expensive. Always return control to Unity before
                // scanning the next path so input/repaint events keep flowing.
                yield return null;
            }

            result.SetMetric("Meshes Scanned", MPOFormatUtility.Number(scanned));
            result.SetMetric("High Poly", MPOFormatUtility.Number(highPoly));
            result.SetMetric("Read/Write Enabled", MPOFormatUtility.Number(readableMeshes));
            result.SetMetric("Loaded High Poly / No LOD", MPOFormatUtility.Number(loadedHighPolyWithoutLod));
            result.SetMetric("High Bone Usage", MPOFormatUtility.Number(highBoneMeshes));
            result.SetMetric("High Blend Shapes", MPOFormatUtility.Number(highBlendShapeMeshes));
            result.SetMetric("Skipped Mesh Paths", MPOFormatUtility.Number(skippedPaths));
            context.ReportProgress("Mesh scan complete", 1f);
        }

        private static HashSet<string> CollectMeshAssetPaths(MPOScanContext context)
        {
            var paths = new HashSet<string>();
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", MPOAssetScope.GetFindRoots(context.Scope));
            string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", MPOAssetScope.GetFindRoots(context.Scope));

            foreach (string guid in modelGuids)
            {
                if (context.TryGet("Model GUID " + guid, () => AssetDatabase.GUIDToAssetPath(guid), out string path) && !string.IsNullOrEmpty(path))
                    paths.Add(path);
            }
            foreach (string guid in meshGuids)
            {
                if (context.TryGet("Mesh GUID " + guid, () => AssetDatabase.GUIDToAssetPath(guid), out string path) && !string.IsNullOrEmpty(path))
                    paths.Add(path);
            }

            return paths;
        }

        private static HashSet<Mesh> CollectLoadedMeshes(MPOScanContext context, Dictionary<Mesh, int> skinnedBoneCounts)
        {
            var meshes = new HashSet<Mesh>();
            List<Renderer> renderers = MPOSceneUtility.GetComponentsInLoadedScenes<Renderer>(context);
            foreach (Renderer renderer in renderers)
            {
                context.TryExecute("Loaded mesh usage: " + SafeObjectName(renderer), () =>
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        return;

                    if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                    {
                        meshes.Add(skinned.sharedMesh);
                        int bones = skinned.bones != null ? skinned.bones.Length : 0;
                        if (!skinnedBoneCounts.TryGetValue(skinned.sharedMesh, out int previous) || bones > previous)
                            skinnedBoneCounts[skinned.sharedMesh] = bones;
                        return;
                    }

                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null)
                        meshes.Add(filter.sharedMesh);
                });
            }

            return meshes;
        }

        private static HashSet<Mesh> CollectLodMeshesInLoadedScenes(MPOScanContext context)
        {
            var meshes = new HashSet<Mesh>();
            List<LODGroup> groups = MPOSceneUtility.GetComponentsInLoadedScenes<LODGroup>(context);
            foreach (LODGroup group in groups)
            {
                context.TryExecute("LODGroup: " + SafeObjectName(group), () =>
                {
                    if (group == null || !group.enabled || !group.gameObject.activeInHierarchy)
                        return;

                    LOD[] lods = group.GetLODs();
                    if (lods == null)
                        return;

                    foreach (LOD lod in lods)
                    {
                        if (lod.renderers == null)
                            continue;

                        foreach (Renderer renderer in lod.renderers)
                        {
                            if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                                meshes.Add(skinned.sharedMesh);

                            MeshFilter filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
                            if (filter != null && filter.sharedMesh != null)
                                meshes.Add(filter.sharedMesh);
                        }
                    }
                });
            }

            return meshes;
        }

        private static string SafeObjectName(Object obj)
        {
            try { return obj != null && !string.IsNullOrEmpty(obj.name) ? obj.name : "Unknown"; }
            catch { return "Unknown"; }
        }
    }
}
