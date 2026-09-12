using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    [Serializable]
    internal sealed class MPOSettingSnapshot
    {
        public string key;
        public string before;
        public string after;
        public string type;
    }

    [Serializable]
    internal sealed class MPOFixSnapshotData
    {
        public string key;
        public int kind;
        public string assetPath;
        public bool bool0;
        public bool bool1;
        public bool bool2;
        public bool bool3;
        public int int0;
        public int int1;
        public int int2;
        public int int3;
        public float float0;
        public bool bool4;
        public string string0;
        public string string1;
        public List<MPOSettingSnapshot> settings;
    }

    [Serializable]
    internal sealed class MPOFixSessionData
    {
        public string startedAtUtc;
        public List<MPOFixSnapshotData> snapshots = new List<MPOFixSnapshotData>();
    }

    public static class MPOFixSession
    {
        private static MPOFixSessionData _session;

        public static bool HasRevertableSession => Session.snapshots.Count > 0;
        public static int ChangeCount => Session.snapshots.Count;
        public static string StartedAtUtc => Session.startedAtUtc ?? string.Empty;

        public static void Clear()
        {
            _session = new MPOFixSessionData { startedAtUtc = DateTime.UtcNow.ToString("O") };
            Persist();
        }

        internal static bool HasKey(string key)
        {
            for (int i = 0; i < Session.snapshots.Count; i++)
            {
                if (Session.snapshots[i].key == key)
                    return true;
            }
            return false;
        }

        internal static void Add(MPOFixSnapshotData snapshot)
        {
            if (snapshot == null || string.IsNullOrEmpty(snapshot.key) || HasKey(snapshot.key))
                return;

            if (Session.snapshots.Count == 0)
                Session.startedAtUtc = DateTime.UtcNow.ToString("O");

            Session.snapshots.Add(snapshot);
            Persist();
        }

        public static string[] GetChangeDescriptions()
        {
            var descriptions = new List<string>();
            foreach (MPOFixSnapshotData snapshot in Session.snapshots)
            {
                string target = string.IsNullOrWhiteSpace(snapshot.assetPath) ? "Project Build Settings" : snapshot.assetPath;
                descriptions.Add(FriendlyFixName((MPOFixKind)snapshot.kind) + " — " + target);
            }
            return descriptions.ToArray();
        }

        public static bool RevertLastSession(out string message)
        {
            if (!HasRevertableSession)
            {
                message = "There is no fix session to revert.";
                return false;
            }

            int restored = 0;
            var failed = new List<MPOFixSnapshotData>();
            try
            {
                for (int i = Session.snapshots.Count - 1; i >= 0; i--)
                {
                    MPOFixSnapshotData snapshot = Session.snapshots[i];
                    try
                    {
                        if (Restore(snapshot))
                            restored++;
                        else
                            failed.Add(snapshot);
                    }
                    catch (Exception restoreException)
                    {
                        Debug.LogException(restoreException);
                        failed.Add(snapshot);
                    }
                }

                AssetDatabase.SaveAssets();

                if (failed.Count == 0)
                {
                    message = "Restored " + restored + " change(s). Re-scan the project to refresh results.";
                    Clear();
                    return true;
                }

                failed.Reverse();
                Session.snapshots = failed;
                Persist();
                message = "Restored " + restored + " change(s), but " + failed.Count + " change(s) could not be restored. The failed snapshots were kept so you can retry after resolving missing/locked assets. Check the Console for any exceptions.";
                return restored > 0;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                message = "Revert stopped because an unexpected error occurred. The session was kept. Check the Console.\n\n" + exception.Message;
                Persist();
                return false;
            }
        }

        private static bool Restore(MPOFixSnapshotData snapshot)
        {
            switch ((MPOFixKind)snapshot.kind)
            {
                case MPOFixKind.ReviewSettings:
                    return MPOFixPlans.RestoreSettings(snapshot);
                case MPOFixKind.DisableDevelopmentBuildFlags:
                    EditorUserBuildSettings.development = snapshot.bool0;
                    EditorUserBuildSettings.allowDebugging = snapshot.bool1;
                    EditorUserBuildSettings.connectProfiler = snapshot.bool2;
                    EditorUserBuildSettings.buildWithDeepProfilingSupport = snapshot.bool3;
                    return true;

                case MPOFixKind.DisableTextureReadWrite:
                {
                    TextureImporter importer = AssetImporter.GetAtPath(snapshot.assetPath) as TextureImporter;
                    if (importer == null) return false;
                    importer.isReadable = snapshot.bool0;
                    importer.SaveAndReimport();
                    return true;
                }

                case MPOFixKind.DisableMeshReadWrite:
                {
                    ModelImporter importer = AssetImporter.GetAtPath(snapshot.assetPath) as ModelImporter;
                    if (importer == null) return false;
                    importer.isReadable = snapshot.bool0;
                    importer.SaveAndReimport();
                    return true;
                }

                case MPOFixKind.StreamLongAudio:
                {
                    AudioImporter importer = AssetImporter.GetAtPath(snapshot.assetPath) as AudioImporter;
                    if (importer == null) return false;
                    AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                    settings.loadType = (AudioClipLoadType)snapshot.int0;
                    settings.compressionFormat = (AudioCompressionFormat)snapshot.int1;
                    settings.sampleRateSetting = (AudioSampleRateSetting)snapshot.int2;
                    settings.sampleRateOverride = (uint)snapshot.int3;
                    settings.quality = snapshot.float0;
                    settings.preloadAudioData = snapshot.bool4;
                    importer.defaultSampleSettings = settings;
                    importer.forceToMono = snapshot.bool0;
                    importer.loadInBackground = snapshot.bool1;
                    importer.SaveAndReimport();
                    return true;
                }

                case MPOFixKind.SetTexturePlatformMaxSize:
                {
                    TextureImporter importer = AssetImporter.GetAtPath(snapshot.assetPath) as TextureImporter;
                    if (importer == null || string.IsNullOrWhiteSpace(snapshot.string0)) return false;
                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(snapshot.string0);
                    settings.name = snapshot.string0;
                    settings.overridden = snapshot.bool0;
                    settings.maxTextureSize = snapshot.int0;
                    importer.SetPlatformTextureSettings(settings);
                    importer.SaveAndReimport();
                    return true;
                }

                case MPOFixKind.EnableMaterialGpuInstancing:
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(snapshot.assetPath);
                    if (material == null) return false;
                    material.enableInstancing = snapshot.bool0;
                    EditorUtility.SetDirty(material);
                    return true;
                }
            }

            return false;
        }

        private static string FriendlyFixName(MPOFixKind kind)
        {
            switch (kind)
            {
                case MPOFixKind.DisableDevelopmentBuildFlags: return "Disable development/debug build flags";
                case MPOFixKind.DisableTextureReadWrite: return "Disable texture Read/Write";
                case MPOFixKind.DisableMeshReadWrite: return "Disable mesh Read/Write";
                case MPOFixKind.StreamLongAudio: return "Stream long audio";
                case MPOFixKind.SetTexturePlatformMaxSize: return "Set mobile texture max size";
                case MPOFixKind.EnableMaterialGpuInstancing: return "Enable material GPU Instancing";
                default: return "Optimization change";
            }
        }

        private static MPOFixSessionData Session
        {
            get
            {
                if (_session != null)
                    return _session;

                try
                {
                    if (File.Exists(MPOConstants.SessionFile))
                    {
                        string json = File.ReadAllText(MPOConstants.SessionFile);
                        _session = JsonUtility.FromJson<MPOFixSessionData>(json);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[MPO] Could not load the previous fix session: " + exception.Message);
                }

                if (_session == null)
                    _session = new MPOFixSessionData { startedAtUtc = DateTime.UtcNow.ToString("O") };
                if (_session.snapshots == null)
                    _session.snapshots = new List<MPOFixSnapshotData>();
                if (string.IsNullOrWhiteSpace(_session.startedAtUtc))
                    _session.startedAtUtc = DateTime.UtcNow.ToString("O");
                return _session;
            }
        }

        private static void Persist()
        {
            try
            {
                string directory = Path.GetDirectoryName(MPOConstants.SessionFile);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(MPOConstants.SessionFile, JsonUtility.ToJson(Session, true));
            }
            catch (Exception exception)
            {
                throw new IOException("Could not persist the fix session; no new changes should be applied.", exception);
            }
        }
    }
}
