using UnityEditor;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public static class MPOFixEngine
    {
        public static string GetActionKey(MPOIssue issue)
        {
            if (issue == null)
                return string.Empty;

            return issue.FixKind + "|" +
                   (issue.AssetPath ?? string.Empty) + "|" +
                   issue.FixIntValue + "|" +
                   (issue.FixStringValue ?? string.Empty);
        }

        public static bool Apply(MPOIssue issue, out string message)
        {
            if (issue == null || !issue.CanFix)
            {
                message = "This issue does not have an automatic fix.";
                return false;
            }

            switch (issue.FixKind)
            {
                case MPOFixKind.DisableDevelopmentBuildFlags:
                    return DisableDevelopmentBuildFlags(out message);
                case MPOFixKind.DisableTextureReadWrite:
                    return DisableTextureReadWrite(issue.AssetPath, out message);
                case MPOFixKind.DisableMeshReadWrite:
                    return DisableMeshReadWrite(issue.AssetPath, out message);
                case MPOFixKind.StreamLongAudio:
                    return StreamLongAudio(issue.AssetPath, out message);
                case MPOFixKind.SetTexturePlatformMaxSize:
                    return SetTexturePlatformMaxSize(issue.AssetPath, issue.FixStringValue, issue.FixIntValue, out message);
                case MPOFixKind.EnableMaterialGpuInstancing:
                    return EnableMaterialGpuInstancing(issue.AssetPath, out message);
                default:
                    message = "Unsupported fix type.";
                    return false;
            }
        }

        private static bool DisableDevelopmentBuildFlags(out string message)
        {
            string key = "build.flags";
            if (!MPOFixSession.HasKey(key))
            {
                MPOFixSession.Add(new MPOFixSnapshotData
                {
                    key = key,
                    kind = (int)MPOFixKind.DisableDevelopmentBuildFlags,
                    bool0 = EditorUserBuildSettings.development,
                    bool1 = EditorUserBuildSettings.allowDebugging,
                    bool2 = EditorUserBuildSettings.connectProfiler,
                    bool3 = EditorUserBuildSettings.buildWithDeepProfilingSupport
                });
            }

            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
            message = "Disabled Development Build, Script Debugging, Autoconnect Profiler, and Deep Profiling build flags.";
            return true;
        }

        private static bool DisableTextureReadWrite(string assetPath, out string message)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                message = "Texture importer was not found.";
                return false;
            }

            string key = "texture.readable|" + assetPath;
            if (!MPOFixSession.HasKey(key))
            {
                MPOFixSession.Add(new MPOFixSnapshotData
                {
                    key = key,
                    kind = (int)MPOFixKind.DisableTextureReadWrite,
                    assetPath = assetPath,
                    bool0 = importer.isReadable
                });
            }

            importer.isReadable = false;
            importer.SaveAndReimport();
            message = "Disabled Texture Read/Write for " + assetPath + ".";
            return true;
        }

        private static bool DisableMeshReadWrite(string assetPath, out string message)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                message = "Model importer was not found. Embedded/native mesh assets are recommendation-only.";
                return false;
            }

            string key = "mesh.readable|" + assetPath;
            if (!MPOFixSession.HasKey(key))
            {
                MPOFixSession.Add(new MPOFixSnapshotData
                {
                    key = key,
                    kind = (int)MPOFixKind.DisableMeshReadWrite,
                    assetPath = assetPath,
                    bool0 = importer.isReadable
                });
            }

            importer.isReadable = false;
            importer.SaveAndReimport();
            message = "Disabled Model Read/Write for " + assetPath + ".";
            return true;
        }

        private static bool StreamLongAudio(string assetPath, out string message)
        {
            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
            {
                message = "Audio importer was not found.";
                return false;
            }

            AudioImporterSampleSettings current = importer.defaultSampleSettings;
            string key = "audio.streaming|" + assetPath;
            if (!MPOFixSession.HasKey(key))
            {
                MPOFixSession.Add(new MPOFixSnapshotData
                {
                    key = key,
                    kind = (int)MPOFixKind.StreamLongAudio,
                    assetPath = assetPath,
                    int0 = (int)current.loadType,
                    int1 = (int)current.compressionFormat,
                    int2 = (int)current.sampleRateSetting,
                    int3 = (int)current.sampleRateOverride,
                    float0 = current.quality,
                    bool0 = importer.forceToMono,
                    bool1 = importer.loadInBackground,
                    bool4 = current.preloadAudioData
                });
            }

            current.loadType = AudioClipLoadType.Streaming;
            current.preloadAudioData = false;
            importer.defaultSampleSettings = current;
            importer.SaveAndReimport();
            message = "Changed long audio to Streaming and disabled preload. Verify playback/latency for this clip.";
            return true;
        }

        private static bool SetTexturePlatformMaxSize(string assetPath, string platformName, int targetMaxSize, out string message)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                message = "Texture importer was not found.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(platformName))
            {
                message = "Target platform was not provided for this texture fix.";
                return false;
            }

            targetMaxSize = Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.Max(32, targetMaxSize)), 32, 8192);
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);

            string key = "texture.platform-max|" + platformName + "|" + targetMaxSize + "|" + assetPath;
            if (!MPOFixSession.HasKey(key))
            {
                MPOFixSession.Add(new MPOFixSnapshotData
                {
                    key = key,
                    kind = (int)MPOFixKind.SetTexturePlatformMaxSize,
                    assetPath = assetPath,
                    string0 = platformName,
                    bool0 = settings.overridden,
                    int0 = settings.maxTextureSize
                });
            }

            settings.name = platformName;
            settings.overridden = true;
            settings.maxTextureSize = targetMaxSize;
            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();

            message = "Set " + platformName + " texture max size to " + targetMaxSize + " px for " + assetPath + ".";
            return true;
        }

        private static bool EnableMaterialGpuInstancing(string assetPath, out string message)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                message = "Material could not be loaded.";
                return false;
            }

            if (material.shader == null)
            {
                message = "Material has no valid shader, so GPU Instancing was not changed.";
                return false;
            }

            string key = "material.instancing|" + assetPath;
            if (!MPOFixSession.HasKey(key))
            {
                MPOFixSession.Add(new MPOFixSnapshotData
                {
                    key = key,
                    kind = (int)MPOFixKind.EnableMaterialGpuInstancing,
                    assetPath = assetPath,
                    bool0 = material.enableInstancing
                });
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            message = "Enabled GPU Instancing on " + assetPath + ". Verify on target hardware that the shader and repeated renderer usage benefit from instancing.";
            return true;
        }
    }
}
