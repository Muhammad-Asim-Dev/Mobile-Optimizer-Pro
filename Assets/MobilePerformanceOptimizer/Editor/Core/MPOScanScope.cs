using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MobilePerformanceOptimizer
{
    public enum MPOScanScopeMode
    {
        FullProject = 0,
        BuildScenes = 1,
        CurrentScene = 2,
        SelectedFolder = 3,
        SelectedAssets = 4
    }

    /// <summary>
    /// Immutable description of what a scan is allowed to inspect. Asset analyzers use this
    /// scope to filter project paths, while scene analyzers use it to filter currently loaded scenes.
    /// The scope never opens/closes scenes or mutates the user's project state.
    /// </summary>
    public sealed class MPOScanScope
    {
        private readonly HashSet<string> _includedAssetPaths;
        private readonly HashSet<string> _scenePaths;
        private readonly string[] _searchFolders;
        private readonly int _activeSceneHandle;
        private readonly bool _hasLoadedScopedScene;

        public MPOScanScopeMode Mode { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string ScopeKey { get; }
        public string SelectedFolderPath { get; }
        public int SelectedAssetCount { get; }

        public string[] SearchFolders => _searchFolders;

        private MPOScanScope(
            MPOScanScopeMode mode,
            string displayName,
            string description,
            string scopeKey,
            IEnumerable<string> includedAssetPaths,
            IEnumerable<string> scenePaths,
            IEnumerable<string> searchFolders,
            int activeSceneHandle,
            bool hasLoadedScopedScene,
            string selectedFolderPath,
            int selectedAssetCount)
        {
            Mode = mode;
            DisplayName = displayName ?? mode.ToString();
            Description = description ?? string.Empty;
            ScopeKey = string.IsNullOrWhiteSpace(scopeKey) ? mode.ToString() : scopeKey;
            SelectedFolderPath = selectedFolderPath ?? string.Empty;
            SelectedAssetCount = Mathf.Max(0, selectedAssetCount);
            _activeSceneHandle = activeSceneHandle;
            _hasLoadedScopedScene = hasLoadedScopedScene;
            _includedAssetPaths = new HashSet<string>(NormalizeMany(includedAssetPaths), StringComparer.OrdinalIgnoreCase);
            _scenePaths = new HashSet<string>(NormalizeMany(scenePaths), StringComparer.OrdinalIgnoreCase);
            _searchFolders = NormalizeMany(searchFolders).Where(AssetDatabase.IsValidFolder).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static MPOScanScope FullProject()
        {
            return new MPOScanScope(
                MPOScanScopeMode.FullProject,
                "Full Project",
                "Scans project assets plus currently loaded scene content.",
                "full-project",
                null,
                null,
                new[] { "Assets" },
                0,
                true,
                string.Empty,
                0);
        }

        public static bool TryCreate(MPOScanScopeMode mode, string selectedFolderPath, out MPOScanScope scope, out string error)
        {
            scope = null;
            error = string.Empty;

            try
            {
                switch (mode)
                {
                    case MPOScanScopeMode.FullProject:
                        scope = FullProject();
                        return true;

                    case MPOScanScopeMode.BuildScenes:
                        return TryCreateBuildScenes(out scope, out error);

                    case MPOScanScopeMode.CurrentScene:
                        return TryCreateCurrentScene(out scope, out error);

                    case MPOScanScopeMode.SelectedFolder:
                        return TryCreateSelectedFolder(selectedFolderPath, out scope, out error);

                    case MPOScanScopeMode.SelectedAssets:
                        return TryCreateSelectedAssets(out scope, out error);

                    default:
                        error = "Unknown scan scope.";
                        return false;
                }
            }
            catch (Exception exception)
            {
                error = "Could not prepare the selected scan scope: " + exception.Message;
                return false;
            }
        }

        public bool ShouldScanAsset(string assetPath)
        {
            string normalized = Normalize(assetPath);
            if (string.IsNullOrEmpty(normalized))
                return false;

            switch (Mode)
            {
                case MPOScanScopeMode.FullProject:
                    return true;

                case MPOScanScopeMode.SelectedFolder:
                    return _searchFolders.Any(folder =>
                        normalized.Equals(folder, StringComparison.OrdinalIgnoreCase) ||
                        normalized.StartsWith(folder.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));

                case MPOScanScopeMode.BuildScenes:
                case MPOScanScopeMode.CurrentScene:
                case MPOScanScopeMode.SelectedAssets:
                    return _includedAssetPaths.Contains(normalized);

                default:
                    return true;
            }
        }

        public bool ShouldScanLoadedScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            switch (Mode)
            {
                case MPOScanScopeMode.FullProject:
                    return true;

                case MPOScanScopeMode.CurrentScene:
                    return _activeSceneHandle != 0 && scene.handle == _activeSceneHandle;

                case MPOScanScopeMode.BuildScenes:
                    return !string.IsNullOrEmpty(scene.path) && _scenePaths.Contains(Normalize(scene.path));

                // Folder/asset scans are intentionally asset-only. They do not inspect unrelated
                // loaded scene objects just because those scenes happen to be open in the Editor.
                case MPOScanScopeMode.SelectedFolder:
                case MPOScanScopeMode.SelectedAssets:
                    return false;

                default:
                    return true;
            }
        }

        public bool ShouldRunCategory(MPOCategory category)
        {
            bool assetCategory = category == MPOCategory.Textures ||
                                 category == MPOCategory.Meshes ||
                                 category == MPOCategory.Materials ||
                                 category == MPOCategory.Audio;

            if (Mode == MPOScanScopeMode.SelectedFolder || Mode == MPOScanScopeMode.SelectedAssets)
                return assetCategory;

            if (Mode == MPOScanScopeMode.CurrentScene && _includedAssetPaths.Count == 0 && assetCategory)
                return false;

            if (Mode == MPOScanScopeMode.BuildScenes && !_hasLoadedScopedScene)
            {
                bool sceneObjectCategory = category == MPOCategory.Scene ||
                                           category == MPOCategory.Lighting ||
                                           category == MPOCategory.Particles ||
                                           category == MPOCategory.Physics ||
                                           category == MPOCategory.UI;
                return !sceneObjectCategory;
            }

            return true;
        }

        private static bool TryCreateBuildScenes(out MPOScanScope scope, out string error)
        {
            scope = null;
            error = string.Empty;

            string[] scenePaths = (EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>())
                .Where(scene => scene != null && scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => Normalize(scene.path))
                .Where(path => path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (scenePaths.Length == 0)
            {
                error = "No enabled scenes were found in Build Settings.";
                return false;
            }

            string[] dependencies = AssetDatabase.GetDependencies(scenePaths, true) ?? Array.Empty<string>();
            var included = new HashSet<string>(dependencies.Select(Normalize), StringComparer.OrdinalIgnoreCase);
            foreach (string scenePath in scenePaths)
                included.Add(scenePath);

            scope = new MPOScanScope(
                MPOScanScopeMode.BuildScenes,
                "Build Scenes",
                "Scans assets referenced by enabled Build Settings scenes. Scene-component checks use build scenes that are currently loaded, without changing your open-scene setup.",
                "build-scenes|" + StableListKey(scenePaths),
                included,
                scenePaths,
                new[] { "Assets" },
                0,
                HasAnyLoadedScene(scenePaths),
                string.Empty,
                scenePaths.Length);
            return true;
        }

        private static bool TryCreateCurrentScene(out MPOScanScope scope, out string error)
        {
            scope = null;
            error = string.Empty;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = "There is no valid active scene to scan.";
                return false;
            }

            var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string path = Normalize(scene.path);
            if (!string.IsNullOrWhiteSpace(path))
            {
                foreach (string dependency in AssetDatabase.GetDependencies(path, true) ?? Array.Empty<string>())
                    included.Add(Normalize(dependency));
                included.Add(path);
            }

            string sceneName = string.IsNullOrWhiteSpace(scene.name) ? "Current Scene" : scene.name;
            string keySource = !string.IsNullOrWhiteSpace(path) ? path : "unsaved-handle-" + scene.handle;
            scope = new MPOScanScope(
                MPOScanScopeMode.CurrentScene,
                "Current Scene — " + sceneName,
                string.IsNullOrWhiteSpace(path)
                    ? "Scans the active unsaved scene's loaded components. Save the scene to include referenced project assets."
                    : "Scans the active scene, its loaded components, and assets referenced by that scene.",
                "current-scene|" + keySource,
                included,
                string.IsNullOrWhiteSpace(path) ? null : new[] { path },
                new[] { "Assets" },
                scene.handle,
                true,
                string.Empty,
                1);
            return true;
        }

        private static bool TryCreateSelectedFolder(string selectedFolderPath, out MPOScanScope scope, out string error)
        {
            scope = null;
            error = string.Empty;
            string path = Normalize(selectedFolderPath);
            if (string.IsNullOrWhiteSpace(path) || !AssetDatabase.IsValidFolder(path))
            {
                error = "Choose a valid folder inside Assets before starting a Selected Folder scan.";
                return false;
            }

            if (!path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                error = "Selected Folder scans currently support folders inside Assets only.";
                return false;
            }

            scope = new MPOScanScope(
                MPOScanScopeMode.SelectedFolder,
                "Folder — " + path,
                "Scans Texture, Mesh, Material and Audio assets inside the selected folder and its subfolders.",
                "folder|" + path.ToLowerInvariant(),
                null,
                null,
                new[] { path },
                0,
                false,
                path,
                1);
            return true;
        }

        private static bool TryCreateSelectedAssets(out MPOScanScope scope, out string error)
        {
            scope = null;
            error = string.Empty;

            var selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (UnityEngine.Object selected in Selection.objects ?? Array.Empty<UnityEngine.Object>())
            {
                if (selected == null)
                    continue;
                string path = Normalize(AssetDatabase.GetAssetPath(selected));
                if (!string.IsNullOrWhiteSpace(path) && path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                    selectedPaths.Add(path);
            }

            if (selectedPaths.Count == 0)
            {
                error = "Select one or more project assets in the Project window before starting a Selected Assets scan.";
                return false;
            }

            var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceFiles = new List<string>();
            foreach (string path in selectedPaths)
            {
                if (AssetDatabase.IsValidFolder(path))
                {
                    string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { path });
                    foreach (string guid in guids)
                    {
                        string child = Normalize(AssetDatabase.GUIDToAssetPath(guid));
                        if (!string.IsNullOrWhiteSpace(child))
                            sourceFiles.Add(child);
                    }
                }
                else
                {
                    sourceFiles.Add(path);
                }
            }

            string[] uniqueSources = sourceFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (uniqueSources.Length > 0)
            {
                foreach (string dependency in AssetDatabase.GetDependencies(uniqueSources, true) ?? Array.Empty<string>())
                    included.Add(Normalize(dependency));
                foreach (string source in uniqueSources)
                    included.Add(Normalize(source));
            }

            scope = new MPOScanScope(
                MPOScanScopeMode.SelectedAssets,
                "Selected Assets — " + selectedPaths.Count,
                "Scans selected project assets and their dependencies for Texture, Mesh, Material and Audio findings.",
                "selection|" + StableListKey(selectedPaths),
                included,
                null,
                new[] { "Assets" },
                0,
                false,
                string.Empty,
                selectedPaths.Count);
            return true;
        }


        private static bool HasAnyLoadedScene(IEnumerable<string> scenePaths)
        {
            var wanted = new HashSet<string>(NormalizeMany(scenePaths), StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded && !string.IsNullOrWhiteSpace(scene.path) && wanted.Contains(Normalize(scene.path)))
                    return true;
            }
            return false;
        }

        private static IEnumerable<string> NormalizeMany(IEnumerable<string> values)
        {
            if (values == null)
                yield break;
            foreach (string value in values)
            {
                string normalized = Normalize(value);
                if (!string.IsNullOrWhiteSpace(normalized))
                    yield return normalized;
            }
        }

        private static string Normalize(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
        }

        private static string StableListKey(IEnumerable<string> values)
        {
            string joined = string.Join("|", NormalizeMany(values).OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                for (int i = 0; i < joined.Length; i++)
                {
                    hash ^= joined[i];
                    hash *= 1099511628211UL;
                }
                return hash.ToString("X16");
            }
        }
    }
}
