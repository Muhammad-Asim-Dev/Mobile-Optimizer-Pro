using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    [Serializable]
    internal sealed class MPOIgnoreData
    {
        public List<string> ruleIds = new List<string>();
        public List<string> assetPaths = new List<string>();
        public List<string> folderPaths = new List<string>();
    }

    public static class MPOIgnoreStore
    {
        private const string Key = "MPO.IgnoreData.v1";
        private static readonly HashSet<string> IgnoreOnceKeys = new HashSet<string>(StringComparer.Ordinal);
        private static MPOIgnoreData _data;
        private static HashSet<string> _ruleLookup;
        private static HashSet<string> _assetLookup;

        public static int RuleCount => Data.ruleIds.Count;
        public static int AssetCount => Data.assetPaths.Count;
        public static int FolderCount => Data.folderPaths.Count;
        public static int OnceCount => IgnoreOnceKeys.Count;
        public static int PersistentCount => RuleCount + AssetCount + FolderCount;
        public static int TotalCount => PersistentCount + OnceCount;

        private static MPOIgnoreData Data
        {
            get
            {
                if (_data != null)
                    return _data;

                string json = EditorPrefs.GetString(Key, string.Empty);
                _data = string.IsNullOrWhiteSpace(json) ? new MPOIgnoreData() : JsonUtility.FromJson<MPOIgnoreData>(json);
                if (_data == null)
                    _data = new MPOIgnoreData();
                RebuildLookups();
                return _data;
            }
        }

        public static bool IsIgnored(MPOIssue issue)
        {
            if (issue == null)
                return false;

            EnsureLookups();

            if (IgnoreOnceKeys.Contains(issue.IdentityKey))
                return true;

            if (!string.IsNullOrEmpty(issue.RuleId) && _ruleLookup.Contains(issue.RuleId))
                return true;

            string path = Normalize(issue.AssetPath);
            if (!string.IsNullOrEmpty(path) && _assetLookup.Contains(path))
                return true;

            if (!string.IsNullOrEmpty(path))
            {
                List<string> folders = Data.folderPaths;
                for (int i = 0; i < folders.Count; i++)
                {
                    string folder = folders[i];
                    if (path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        public static void IgnoreOnce(MPOIssue issue)
        {
            if (issue == null)
                return;

            string identity = issue.IdentityKey;
            if (!string.IsNullOrWhiteSpace(identity))
                IgnoreOnceKeys.Add(identity);
        }

        public static void IgnoreRule(string ruleId)
        {
            EnsureLookups();
            if (string.IsNullOrWhiteSpace(ruleId) || !_ruleLookup.Add(ruleId))
                return;
            Data.ruleIds.Add(ruleId);
            Save();
        }

        public static void IgnoreAsset(string assetPath)
        {
            EnsureLookups();
            string path = Normalize(assetPath);
            if (string.IsNullOrWhiteSpace(path) || !_assetLookup.Add(path))
                return;
            Data.assetPaths.Add(path);
            Save();
        }

        public static void IgnoreFolder(string assetPath)
        {
            string path = Normalize(assetPath);
            if (string.IsNullOrWhiteSpace(path))
                return;

            string folder = Normalize(Path.GetDirectoryName(path));
            if (string.IsNullOrWhiteSpace(folder))
                return;
            if (!folder.EndsWith("/", StringComparison.Ordinal))
                folder += "/";

            if (!Data.folderPaths.Exists(x => string.Equals(x, folder, StringComparison.OrdinalIgnoreCase)))
            {
                Data.folderPaths.Add(folder);
                Save();
            }
        }

        public static bool RemoveRule(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
                return false;
            bool removed = Data.ruleIds.RemoveAll(x => string.Equals(x, ruleId, StringComparison.Ordinal)) > 0;
            if (removed)
            {
                RebuildLookups();
                Save();
            }
            return removed;
        }

        public static bool RemoveAsset(string assetPath)
        {
            string path = Normalize(assetPath);
            if (string.IsNullOrWhiteSpace(path))
                return false;
            bool removed = Data.assetPaths.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
            {
                RebuildLookups();
                Save();
            }
            return removed;
        }

        public static bool RemoveFolder(string folderPath)
        {
            string path = Normalize(folderPath);
            if (string.IsNullOrWhiteSpace(path))
                return false;
            if (!path.EndsWith("/", StringComparison.Ordinal))
                path += "/";
            bool removed = Data.folderPaths.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
                Save();
            return removed;
        }

        public static void ClearOnce()
        {
            IgnoreOnceKeys.Clear();
        }

        public static void ClearAll()
        {
            _data = new MPOIgnoreData();
            IgnoreOnceKeys.Clear();
            RebuildLookups();
            Save();
        }

        public static string[] GetRules() => Data.ruleIds.ToArray();
        public static string[] GetAssets() => Data.assetPaths.ToArray();
        public static string[] GetFolders() => Data.folderPaths.ToArray();

        private static void EnsureLookups()
        {
            // Access Data first so deserialization/rebuild happens on first use.
            _ = Data;
            if (_ruleLookup == null || _assetLookup == null)
                RebuildLookups();
        }

        private static void RebuildLookups()
        {
            _ruleLookup = new HashSet<string>(_data != null ? _data.ruleIds : new List<string>(), StringComparer.Ordinal);
            _assetLookup = new HashSet<string>(_data != null ? _data.assetPaths : new List<string>(), StringComparer.OrdinalIgnoreCase);
        }

        private static void Save()
        {
            EditorPrefs.SetString(Key, JsonUtility.ToJson(Data));
        }

        private static string Normalize(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
