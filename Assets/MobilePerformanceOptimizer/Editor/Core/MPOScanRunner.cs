using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;

namespace MobilePerformanceOptimizer
{
    /// <summary>
    /// Cooperative main-thread scan scheduler. Unity AssetDatabase/Object APIs remain on the
    /// main thread, while incremental analyzers yield between chunks so the Editor stays responsive.
    /// </summary>
    public static class MPOScanRunner
    {
        private const double EditorFrameBudgetMilliseconds = 4.0;

        private static readonly List<IMPOAnalyzer> AllAnalyzers = new List<IMPOAnalyzer>
        {
            new SceneAnalyzer(),
            new TextureAnalyzer(),
            new MeshAnalyzer(),
            new MaterialAnalyzer(),
            new LightingAnalyzer(),
            new URPAnalyzer(),
            new ParticleAnalyzer(),
            new PhysicsAnalyzer(),
            new UIAnalyzer(),
            new AudioAnalyzer(),
            new BuildSettingsAnalyzer(),
            new QualitySettingsAnalyzer()
        };

        private static readonly List<IMPOAnalyzer> ActiveAnalyzers = new List<IMPOAnalyzer>();
        private static MPOScanResult _result;
        private static MPOProfile _profile;
        private static MPOScanScope _scope;
        private static int _analyzerIndex;
        private static IMPOAnalyzer _currentAnalyzer;
        private static MPOScanContext _currentContext;
        private static IEnumerator _currentRoutine;
        private static bool _cancelRequested;
        private static bool _insideTick;
        private static float _localProgress;
        private static string _statusMessage = string.Empty;
        private static Action<MPOScanResult> _completionCallback;
        private static Action _progressCallback;
        private static double _lastProgressNotifyTime;

        public static bool IsRunning { get; private set; }
        public static float Progress { get; private set; }
        public static string StatusMessage => _statusMessage;
        public static string CurrentAnalyzerName => _currentAnalyzer != null ? _currentAnalyzer.Name : string.Empty;
        public static int CurrentAnalyzerNumber => IsRunning ? Math.Min(_analyzerIndex + 1, ActiveAnalyzers.Count) : 0;
        public static int AnalyzerCount => ActiveAnalyzers.Count;
        public static MPOScanScope CurrentScope => _scope;

        public static bool Start(MPOProfile profile, Action<MPOScanResult> onCompleted, Action onProgress = null)
        {
            return Start(profile, MPOScanScope.FullProject(), onCompleted, onProgress);
        }

        public static bool Start(MPOProfile profile, MPOScanScope scope, Action<MPOScanResult> onCompleted, Action onProgress = null)
        {
            if (profile == null || IsRunning)
                return false;

            _profile = profile;
            _scope = scope ?? MPOScanScope.FullProject();
            ActiveAnalyzers.Clear();
            ActiveAnalyzers.AddRange(AllAnalyzers.Where(analyzer => analyzer != null && _scope.ShouldRunCategory(analyzer.Category) && MPOConstants.IsCoreReleaseCategory(analyzer.Category)));

            _result = new MPOScanResult { TotalAnalyzerCount = ActiveAnalyzers.Count };
            _analyzerIndex = 0;
            _currentAnalyzer = null;
            _currentContext = null;
            _currentRoutine = null;
            _cancelRequested = false;
            _localProgress = 0f;
            Progress = 0f;
            _statusMessage = "Preparing " + _scope.DisplayName + " scan…";
            _completionCallback = onCompleted;
            _progressCallback = onProgress;
            _lastProgressNotifyTime = 0d;
            IsRunning = true;

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            NotifyProgress();
            return true;
        }

        public static void Cancel()
        {
            if (!IsRunning)
                return;

            _cancelRequested = true;
            _statusMessage = "Cancelling after the current safe scan chunk…";
            NotifyProgress();
        }

        public static void CancelAndDetachCallbacks()
        {
            if (!IsRunning)
                return;

            _completionCallback = null;
            _progressCallback = null;
            _cancelRequested = true;
        }

        public static MPOScanResult Run(MPOProfile profile)
        {
            return Run(profile, MPOScanScope.FullProject());
        }

        public static MPOScanResult Run(MPOProfile profile, MPOScanScope scope)
        {
            if (profile == null)
                return new MPOScanResult { CompletedAtUtc = DateTime.UtcNow };

            scope = scope ?? MPOScanScope.FullProject();
            List<IMPOAnalyzer> analyzers = AllAnalyzers.Where(analyzer => scope.ShouldRunCategory(analyzer.Category) && MPOConstants.IsCoreReleaseCategory(analyzer.Category)).ToList();
            var result = new MPOScanResult { TotalAnalyzerCount = analyzers.Count };
            for (int i = 0; i < analyzers.Count; i++)
            {
                IMPOAnalyzer analyzer = analyzers[i];
                var context = new MPOScanContext(profile, result, scope, analyzer.Name, (message, progress) => false);
                try
                {
                    if (analyzer is IMPOIncrementalAnalyzer incremental)
                    {
                        IEnumerator routine = incremental.AnalyzeIncremental(context);
                        while (routine != null && routine.MoveNext()) { }
                    }
                    else
                    {
                        analyzer.Analyze(context);
                    }
                    result.CompletedAnalyzerCount++;
                }
                catch (OutOfMemoryException)
                {
                    result.AnalyzerErrors.Add("Fatal: Out of memory while scanning.");
                    break;
                }
                catch (Exception exception)
                {
                    result.FailedAnalyzerCount++;
                    result.AnalyzerErrors.Add(analyzer.Name + ": " + exception.GetType().Name + ": " + exception.Message);
                }
            }

            result.CompletedAtUtc = DateTime.UtcNow;
            return result;
        }

        private static void Tick()
        {
            if (!IsRunning || _insideTick)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _statusMessage = EditorApplication.isCompiling
                    ? "Unity is compiling scripts — scan paused safely…"
                    : "Unity is importing/updating assets — scan paused safely…";
                NotifyProgress();
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                _cancelRequested = true;

            _insideTick = true;
            try
            {
                Stopwatch budget = Stopwatch.StartNew();
                while (IsRunning && budget.Elapsed.TotalMilliseconds < EditorFrameBudgetMilliseconds)
                {
                    if (_cancelRequested && _currentRoutine == null)
                    {
                        FinishScan(true);
                        break;
                    }

                    if (_currentRoutine == null)
                    {
                        if (_analyzerIndex >= ActiveAnalyzers.Count)
                        {
                            FinishScan(false);
                            break;
                        }

                        BeginAnalyzer();
                    }

                    bool hasMore;
                    try
                    {
                        hasMore = _currentRoutine != null && _currentRoutine.MoveNext();
                    }
                    catch (OutOfMemoryException exception)
                    {
                        _result.AnalyzerErrors.Add("Fatal: Out of memory while scanning. Close memory-heavy tools/scenes and try again. " + exception.Message);
                        FinishScan(false);
                        break;
                    }
                    catch (Exception exception)
                    {
                        FailCurrentAnalyzer(exception);
                        continue;
                    }

                    if (_cancelRequested || (_currentContext != null && _currentContext.IsCancelled))
                    {
                        FinishScan(true);
                        break;
                    }

                    if (!hasMore)
                        CompleteCurrentAnalyzer();
                }
            }
            finally
            {
                _insideTick = false;
                if (IsRunning)
                    NotifyProgress();
            }
        }

        private static void BeginAnalyzer()
        {
            _currentAnalyzer = ActiveAnalyzers[_analyzerIndex];
            _localProgress = 0f;
            UpdateGlobalProgress();
            _statusMessage = "Starting " + _currentAnalyzer.Name + "…";

            _currentContext = new MPOScanContext(
                _profile,
                _result,
                _scope,
                _currentAnalyzer.Name,
                (message, localProgress) =>
                {
                    _localProgress = Math.Max(0f, Math.Min(1f, localProgress));
                    _statusMessage = string.IsNullOrWhiteSpace(message)
                        ? _currentAnalyzer.Name
                        : _currentAnalyzer.Name + " — " + message;
                    UpdateGlobalProgress();
                    return _cancelRequested;
                });

            if (_currentAnalyzer is IMPOIncrementalAnalyzer incremental)
                _currentRoutine = incremental.AnalyzeIncremental(_currentContext);
            else
                _currentRoutine = RunSingleStep(_currentAnalyzer, _currentContext);
        }

        private static IEnumerator RunSingleStep(IMPOAnalyzer analyzer, MPOScanContext context)
        {
            analyzer.Analyze(context);
            yield break;
        }

        private static void CompleteCurrentAnalyzer()
        {
            DisposeCurrentRoutine();
            if (_currentContext == null || !_currentContext.IsCancelled)
                _result.CompletedAnalyzerCount++;

            _analyzerIndex++;
            _localProgress = 0f;
            _currentAnalyzer = null;
            _currentContext = null;
            UpdateGlobalProgress();
        }

        private static void FailCurrentAnalyzer(Exception exception)
        {
            string analyzerName = _currentAnalyzer != null ? _currentAnalyzer.Name : "Analyzer";
            _result.FailedAnalyzerCount++;
            _result.AnalyzerErrors.Add(analyzerName + ": " + exception.GetType().Name + ": " + exception.Message);
            UnityEngine.Debug.LogWarning("[Mobile Performance Optimizer] " + analyzerName + " stopped early, but the scan will continue with the next analyzer.\n" + exception);

            DisposeCurrentRoutine();
            _analyzerIndex++;
            _localProgress = 0f;
            _currentAnalyzer = null;
            _currentContext = null;
            UpdateGlobalProgress();
        }

        private static void FinishScan(bool cancelled)
        {
            if (!IsRunning)
                return;

            DisposeCurrentRoutine();
            _result.WasCancelled = cancelled;
            _result.CompletedAtUtc = DateTime.UtcNow;
            Progress = cancelled ? Progress : 1f;
            _statusMessage = cancelled ? "Scan cancelled. Partial results preserved." : "Scan complete.";

            EditorApplication.update -= Tick;
            IsRunning = false;

            MPOScanResult completedResult = _result;
            Action<MPOScanResult> callback = _completionCallback;
            Action progress = _progressCallback;

            _profile = null;
            _result = null;
            _currentAnalyzer = null;
            _currentContext = null;
            _completionCallback = null;
            _progressCallback = null;
            _cancelRequested = false;
            ActiveAnalyzers.Clear();

            try { progress?.Invoke(); } catch { }
            try { callback?.Invoke(completedResult); }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }

        private static void DisposeCurrentRoutine()
        {
            try
            {
                if (_currentRoutine is IDisposable disposable)
                    disposable.Dispose();
            }
            catch { }
            _currentRoutine = null;
        }

        private static void UpdateGlobalProgress()
        {
            int total = Math.Max(1, ActiveAnalyzers.Count);
            Progress = Math.Max(0f, Math.Min(1f, (_analyzerIndex + _localProgress) / total));
        }

        private static void NotifyProgress()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_lastProgressNotifyTime > 0d && now - _lastProgressNotifyTime < 0.10d)
                return;

            _lastProgressNotifyTime = now;
            try { _progressCallback?.Invoke(); }
            catch { }
        }
    }
}
