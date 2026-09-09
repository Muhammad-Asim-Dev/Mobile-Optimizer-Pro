using System;
using System.Collections.Generic;
using System.Linq;

namespace MobilePerformanceOptimizer
{
    public sealed class MPOScanResult
    {
        private const int MaxStoredRecoverableWarnings = 100;

        private readonly Dictionary<MPOCategory, MPOCategoryResult> _categories =
            new Dictionary<MPOCategory, MPOCategoryResult>();

        public DateTime CompletedAtUtc { get; internal set; }
        public bool WasCancelled { get; internal set; }
        public List<string> AnalyzerErrors { get; } = new List<string>();
        public List<string> RecoverableWarnings { get; } = new List<string>();
        public int RecoverableErrorCount { get; private set; }
        public int CompletedAnalyzerCount { get; internal set; }
        public int FailedAnalyzerCount { get; internal set; }
        public int TotalAnalyzerCount { get; internal set; }
        public IReadOnlyDictionary<MPOCategory, MPOCategoryResult> Categories => _categories;

        public int SuppressedRecoverableWarningCount =>
            Math.Max(0, RecoverableErrorCount - RecoverableWarnings.Count);

        public IEnumerable<MPOIssue> AllIssues => _categories.Values.SelectMany(x => x.Issues);
        public IEnumerable<MPOIssue> ActiveIssues => AllIssues.Where(x => !MPOIgnoreStore.IsIgnored(x));
        public int TotalChecks => _categories.Values.Sum(x => x.TotalChecks);
        public int PassedChecks => _categories.Values.Sum(x => x.PassedChecks);
        public int CriticalCount => ActiveIssues.Count(x => x.Severity == MPOSeverity.Critical);
        public int WarningCount => ActiveIssues.Count(x => x.Severity == MPOSeverity.Warning);
        public int SuggestionCount => ActiveIssues.Count(x => x.Severity == MPOSeverity.Suggestion);
        public int IgnoredCount => AllIssues.Count(MPOIgnoreStore.IsIgnored);
        public int FixableCount => ActiveIssues.Count(x => x.CanFix);
        public int SafeFixCount => ActiveIssues.Count(x => x.CanFix && x.FixSafety == MPOFixSafety.Safe);

        public MPOCategoryResult GetOrCreate(MPOCategory category)
        {
            if (_categories.TryGetValue(category, out var result))
                return result;

            result = new MPOCategoryResult(category);
            _categories.Add(category, result);
            return result;
        }

        internal void AddRecoverableWarning(string analyzerName, string itemLabel, Exception exception)
        {
            RecoverableErrorCount++;
            if (RecoverableWarnings.Count >= MaxStoredRecoverableWarnings)
                return;

            string analyzer = string.IsNullOrWhiteSpace(analyzerName) ? "Analyzer" : analyzerName;
            string item = string.IsNullOrWhiteSpace(itemLabel) ? "Unknown item" : itemLabel;
            string reason = exception == null
                ? "Unknown recoverable error"
                : exception.GetType().Name + ": " + exception.Message;

            RecoverableWarnings.Add(analyzer + " — " + item + " — " + reason);
        }

        internal void AddRecoverableWarning(string analyzerName, string itemLabel, string reason)
        {
            RecoverableErrorCount++;
            if (RecoverableWarnings.Count >= MaxStoredRecoverableWarnings)
                return;

            string analyzer = string.IsNullOrWhiteSpace(analyzerName) ? "Analyzer" : analyzerName;
            string item = string.IsNullOrWhiteSpace(itemLabel) ? "Unknown item" : itemLabel;
            string message = string.IsNullOrWhiteSpace(reason) ? "Skipped because the item could not be read safely." : reason;
            RecoverableWarnings.Add(analyzer + " — " + item + " — " + message);
        }
    }
}
