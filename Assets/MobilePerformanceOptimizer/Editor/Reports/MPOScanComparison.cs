using System.Collections.Generic;
using System.Linq;

namespace MobilePerformanceOptimizer
{
    public sealed class MPOScanComparison
    {
        public MPOScanSnapshotData Before { get; private set; }
        public MPOScanSnapshotData After { get; private set; }
        public int ScoreDelta => After.overallScore - Before.overallScore;
        public int CriticalDelta => After.criticalCount - Before.criticalCount;
        public int WarningDelta => After.warningCount - Before.warningCount;
        public int SuggestionDelta => After.suggestionCount - Before.suggestionCount;
        public int NewIssueCount { get; private set; }
        public int ResolvedIssueCount { get; private set; }

        public static MPOScanComparison Create(MPOScanSnapshotData before, MPOScanSnapshotData after)
        {
            if (before == null || after == null)
                return null;

            IEnumerable<MPOReportIssueData> beforeSource = before.issues ?? new List<MPOReportIssueData>();
            IEnumerable<MPOReportIssueData> afterSource = after.issues ?? new List<MPOReportIssueData>();
            var beforeIssues = new HashSet<string>(beforeSource.Where(issue => issue != null && !issue.ignored).Select(issue => issue.identity ?? string.Empty));
            var afterIssues = new HashSet<string>(afterSource.Where(issue => issue != null && !issue.ignored).Select(issue => issue.identity ?? string.Empty));

            return new MPOScanComparison
            {
                Before = before,
                After = after,
                NewIssueCount = afterIssues.Count(identity => !beforeIssues.Contains(identity)),
                ResolvedIssueCount = beforeIssues.Count(identity => !afterIssues.Contains(identity))
            };
        }
    }
}
