using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MobilePerformanceOptimizer
{
    internal static class MPOSceneUtility
    {
        public static List<GameObject> EnumerateLoadedSceneRoots(MPOScanContext context = null)
        {
            var rootsResult = new List<GameObject>();
            int sceneCount;
            try
            {
                sceneCount = SceneManager.sceneCount;
            }
            catch (Exception exception)
            {
                context?.RecordRecoverableError("Loaded scene list", exception);
                return rootsResult;
            }

            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                Scene scene;
                try
                {
                    scene = SceneManager.GetSceneAt(sceneIndex);
                    if (!scene.IsValid() || !scene.isLoaded)
                        continue;
                    if (context != null && context.Scope != null && !context.Scope.ShouldScanLoadedScene(scene))
                        continue;
                }
                catch (Exception exception)
                {
                    context?.RecordRecoverableError("Scene index " + sceneIndex, exception);
                    continue;
                }

                GameObject[] roots;
                try
                {
                    roots = scene.GetRootGameObjects();
                }
                catch (Exception exception)
                {
                    context?.RecordRecoverableError("Scene roots: " + SafeSceneName(scene), exception);
                    continue;
                }

                if (roots == null)
                    continue;

                foreach (GameObject root in roots)
                {
                    if (root != null)
                        rootsResult.Add(root);
                }
            }

            return rootsResult;
        }

        public static List<T> GetComponentsInLoadedScenes<T>(MPOScanContext context = null) where T : Component
        {
            var results = new List<T>();
            foreach (GameObject root in EnumerateLoadedSceneRoots(context))
            {
                try
                {
                    T[] components = root.GetComponentsInChildren<T>(true);
                    if (components != null && components.Length > 0)
                        results.AddRange(components);
                }
                catch (Exception exception)
                {
                    string rootName;
                    try { rootName = root != null ? root.name : "Unknown root"; }
                    catch { rootName = "Unknown root"; }
                    context?.RecordRecoverableError("Scene hierarchy: " + rootName + " / " + typeof(T).Name, exception);
                }
            }
            return results;
        }

        public static long GetTriangleCount(Mesh mesh, MPOScanContext context = null, string itemLabel = null)
        {
            if (mesh == null)
                return 0;

            try
            {
                long triangles = 0;
                int subMeshCount = mesh.subMeshCount;
                for (int i = 0; i < subMeshCount; i++)
                {
                    if (mesh.GetTopology(i) == MeshTopology.Triangles)
                        triangles += (long)mesh.GetIndexCount(i) / 3L;
                }

                return triangles;
            }
            catch (Exception exception)
            {
                context?.RecordRecoverableError(itemLabel ?? "Mesh triangle data", exception);
                return 0;
            }
        }

        private static string SafeSceneName(Scene scene)
        {
            try
            {
                return string.IsNullOrEmpty(scene.name) ? "Unnamed scene" : scene.name;
            }
            catch
            {
                return "Unknown scene";
            }
        }
    }
}
