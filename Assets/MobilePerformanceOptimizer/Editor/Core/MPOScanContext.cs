using System;
using UnityEditor;

namespace MobilePerformanceOptimizer
{
    public sealed class MPOScanContext
    {
        private readonly Func<string, float, bool> _progressCallback;

        public MPOProfile Profile { get; }
        public MPOScanResult Result { get; }
        public MPOScanScope Scope { get; }
        public string AnalyzerName { get; }
        public bool IsCancelled { get; private set; }

        public MPOScanContext(
            MPOProfile profile,
            MPOScanResult result,
            MPOScanScope scope,
            string analyzerName,
            Func<string, float, bool> progressCallback)
        {
            Profile = profile;
            Result = result;
            Scope = scope ?? MPOScanScope.FullProject();
            AnalyzerName = analyzerName ?? string.Empty;
            _progressCallback = progressCallback;
        }

        public bool ReportProgress(string message, float normalizedProgress)
        {
            if (IsCancelled)
                return true;

            normalizedProgress = UnityEngine.Mathf.Clamp01(normalizedProgress);
            if (_progressCallback == null)
                return false;

            try
            {
                IsCancelled = _progressCallback(message ?? string.Empty, normalizedProgress);
            }
            catch (Exception exception)
            {
                // A progress-bar/UI failure must never stop project analysis.
                RecordRecoverableError("Progress UI", exception);
                IsCancelled = false;
            }

            return IsCancelled;
        }

        public bool TryExecute(string itemLabel, Action action)
        {
            if (action == null)
                return true;

            try
            {
                action();
                return true;
            }
            catch (OutOfMemoryException)
            {
                // Continuing after OOM can destabilize the Editor; let the runner handle it as fatal.
                throw;
            }
            catch (Exception exception)
            {
                RecordRecoverableError(itemLabel, exception);
                return false;
            }
        }

        public bool TryGet<T>(string itemLabel, Func<T> getter, out T value)
        {
            try
            {
                value = getter != null ? getter() : default(T);
                return true;
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch (Exception exception)
            {
                value = default(T);
                RecordRecoverableError(itemLabel, exception);
                return false;
            }
        }

        public void RecordRecoverableError(string itemLabel, Exception exception)
        {
            Result?.AddRecoverableWarning(AnalyzerName, itemLabel, exception);
        }

        public void RecordRecoverableWarning(string itemLabel, string reason)
        {
            Result?.AddRecoverableWarning(AnalyzerName, itemLabel, reason);
        }

        public void Ping(UnityEngine.Object obj, string assetPath)
        {
            try
            {
                if (obj == null && !string.IsNullOrEmpty(assetPath))
                    obj = AssetDatabase.LoadMainAssetAtPath(assetPath);

                if (obj == null)
                    return;

                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
            catch (Exception exception)
            {
                RecordRecoverableError("Ping asset: " + (assetPath ?? "Unknown"), exception);
            }
        }
    }
}
