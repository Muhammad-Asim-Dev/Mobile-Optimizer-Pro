using System;
using System.Collections.Generic;
using System.Linq;

namespace MobilePerformanceOptimizer
{
    /// <summary>
    /// Presentation-only grouping for noisy projects. It does not change analyzer output or scoring;
    /// it simply groups findings that originate from the same optimization rule.
    /// </summary>
    public sealed class MPOIssueGroup
    {
        private readonly List<MPOIssue> _issues;

        public string Key { get; }
        public string RuleId { get; }
        public MPOCategory Category { get; }
        public string Title { get; }
        public IReadOnlyList<MPOIssue> Issues => _issues;
        public int Count => _issues.Count;
        public int DistinctAssetCount { get; }
        public int FixableCount { get; }
        public int SafeFixCount { get; }
        public int TotalPenalty { get; }
        public MPOSeverity Severity { get; }
        public MPOImpactLevel CpuImpact { get; }
        public MPOImpactLevel GpuImpact { get; }
        public MPOImpactLevel MemoryImpact { get; }
        public MPOImpactLevel BuildSizeImpact { get; }
        public MPOImpactLevel ThermalImpact { get; }
        public MPOImpactLevel HighestImpact { get; }
        public int PriorityScore { get; }
        public MPOIssue Representative { get; }

        private MPOIssueGroup(IGrouping<string, MPOIssue> grouping)
        {
            _issues = grouping
                .Where(x => x != null)
                .OrderByDescending(x => x.Severity)
                .ThenByDescending(x => x.ImpactScore)
                .ThenByDescending(x => x.Penalty)
                .ThenBy(x => x.AssetPath)
                .ToList();

            Representative = _issues.FirstOrDefault();
            if (Representative == null)
                return;

            RuleId = Representative.RuleId;
            Category = Representative.Category;
            Title = Representative.Title;
            Key = GetKey(Representative);
            DistinctAssetCount = _issues
                .Select(x => x.AssetPath)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            FixableCount = _issues.Count(x => x.CanFix);
            SafeFixCount = _issues.Count(x => x.CanFix && x.FixSafety == MPOFixSafety.Safe);
            TotalPenalty = _issues.Sum(x => x.Penalty);
            Severity = _issues.OrderByDescending(x => (int)x.Severity).First().Severity;
            CpuImpact = _issues.OrderByDescending(x => (int)x.CpuImpact).First().CpuImpact;
            GpuImpact = _issues.OrderByDescending(x => (int)x.GpuImpact).First().GpuImpact;
            MemoryImpact = _issues.OrderByDescending(x => (int)x.MemoryImpact).First().MemoryImpact;
            BuildSizeImpact = _issues.OrderByDescending(x => (int)x.BuildSizeImpact).First().BuildSizeImpact;
            ThermalImpact = _issues.OrderByDescending(x => (int)x.ThermalImpact).First().ThermalImpact;
            HighestImpact = MPOImpactUtility.Max(CpuImpact, GpuImpact, MemoryImpact, BuildSizeImpact, ThermalImpact);

            // Ranking is intentionally bounded so one very noisy rule does not dominate forever.
            int severityWeight = Severity == MPOSeverity.Critical ? 300 : Severity == MPOSeverity.Warning ? 170 : 70;
            int impactWeight = MPOImpactUtility.Score(HighestImpact) * 35;
            int breadthWeight = Math.Min(120, Count * 4);
            int penaltyWeight = Math.Min(100, TotalPenalty);
            int fixWeight = SafeFixCount > 0 ? 25 : FixableCount > 0 ? 12 : 0;
            PriorityScore = severityWeight + impactWeight + breadthWeight + penaltyWeight + fixWeight;
        }

        public static string GetKey(MPOIssue issue)
        {
            if (issue == null)
                return string.Empty;

            return issue.Category + "|" + (issue.RuleId ?? string.Empty) + "|" + (issue.Title ?? string.Empty);
        }

        public static List<MPOIssueGroup> Build(IEnumerable<MPOIssue> issues)
        {
            if (issues == null)
                return new List<MPOIssueGroup>();

            return issues
                .Where(x => x != null)
                .GroupBy(GetKey, StringComparer.Ordinal)
                .Select(x => new MPOIssueGroup(x))
                .Where(x => x.Representative != null)
                .OrderByDescending(x => x.PriorityScore)
                .ThenByDescending(x => x.Severity)
                .ThenByDescending(x => x.HighestImpact)
                .ThenByDescending(x => x.Count)
                .ThenBy(x => x.Title)
                .ToList();
        }
    }
}
