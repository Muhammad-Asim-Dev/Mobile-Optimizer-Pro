using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    [Serializable]
    internal sealed class MPOScanHistoryData
    {
        public List<MPOScanSnapshotData> scans = new List<MPOScanSnapshotData>();
    }

    public static class MPOScanHistoryStore
    {
        private const int MaxHistoryEntries = 25;
        private static MPOScanHistoryData _data;

        public static int Count => Data.scans.Count;

        public static void Add(MPOScanSnapshotData snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.id))
                return;

            for (int i = Data.scans.Count - 1; i >= 0; i--)
            {
                if (Data.scans[i] != null && Data.scans[i].id == snapshot.id)
                    Data.scans.RemoveAt(i);
            }

            Data.scans.Add(snapshot);
            while (Data.scans.Count > MaxHistoryEntries)
                Data.scans.RemoveAt(0);

            Persist();
        }

        public static List<MPOScanSnapshotData> GetRecent(int count = 10)
        {
            int take = Mathf.Clamp(count, 1, MaxHistoryEntries);
            return Data.scans
                .Where(scan => scan != null)
                .AsEnumerable()
                .Reverse()
                .Take(take)
                .ToList();
        }

        public static MPOScanSnapshotData GetPreviousComparable(MPOScanSnapshotData current)
        {
            if (current == null)
                return null;

            for (int i = Data.scans.Count - 1; i >= 0; i--)
            {
                MPOScanSnapshotData candidate = Data.scans[i];
                if (candidate == null || candidate.id == current.id)
                    continue;

                bool sameTarget = candidate.targetPlatform == current.targetPlatform && candidate.deviceTier == current.deviceTier;
                bool sameScope = string.Equals(candidate.scanScopeKey ?? string.Empty, current.scanScopeKey ?? string.Empty, StringComparison.Ordinal);
                if (sameTarget && sameScope)
                    return candidate;
            }

            return null;
        }

        public static void Clear()
        {
            _data = new MPOScanHistoryData();
            Persist();
        }

        private static MPOScanHistoryData Data
        {
            get
            {
                if (_data != null)
                    return _data;

                try
                {
                    if (File.Exists(MPOConstants.HistoryFile))
                    {
                        string json = File.ReadAllText(MPOConstants.HistoryFile);
                        _data = JsonUtility.FromJson<MPOScanHistoryData>(json);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[MPO] Could not load scan history: " + exception.Message);
                }

                if (_data == null)
                    _data = new MPOScanHistoryData();
                if (_data.scans == null)
                    _data.scans = new List<MPOScanSnapshotData>();

                return _data;
            }
        }

        private static void Persist()
        {
            try
            {
                string directory = Path.GetDirectoryName(MPOConstants.HistoryFile);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(MPOConstants.HistoryFile, JsonUtility.ToJson(Data, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[MPO] Could not persist scan history: " + exception.Message);
            }
        }
    }
}
