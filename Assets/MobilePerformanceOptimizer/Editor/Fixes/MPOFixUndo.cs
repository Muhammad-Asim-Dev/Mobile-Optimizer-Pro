using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    // Native Undo restores importer objects; persist their restored state before scans read it.
    [InitializeOnLoad]
    internal static class MPOFixUndo
    {
        public static event Action Changed;
        static MPOFixUndo() { Undo.undoRedoPerformed += OnUndo; }
        private static void OnUndo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var importer in Resources.FindObjectsOfTypeAll<AssetImporter>().Where(EditorUtility.IsDirty).ToArray())
            {
                // Only importers touched by this tool, including across a domain reload.
                if (!SessionState.GetBool("MPO.Undo." + importer.assetPath, false)) continue;
                try { importer.SaveAndReimport(); }
                catch (Exception e) { Debug.LogWarning("[MPO] Undo reimport failed for " + importer.assetPath + ": " + e.Message); }
            }
            EditorApplication.delayCall += () => Changed?.Invoke();
        }
        internal static void Track(UnityEngine.Object target)
        {
            if (target is AssetImporter importer) SessionState.SetBool("MPO.Undo." + importer.assetPath, true);
        }
    }
}
