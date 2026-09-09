using System.Collections.Generic;

namespace MobilePerformanceOptimizer
{
    public sealed class MPOCategoryResult
    {
        public MPOCategory Category { get; }
        public int TotalChecks { get; private set; }
        public int PassedChecks { get; private set; }
        public List<MPOIssue> Issues { get; } = new List<MPOIssue>();
        public Dictionary<string, string> Metrics { get; } = new Dictionary<string, string>();

        public MPOCategoryResult(MPOCategory category)
        {
            Category = category;
        }

        public void AddPass()
        {
            TotalChecks++;
            PassedChecks++;
        }

        public void AddIssue(MPOIssue issue)
        {
            TotalChecks++;
            if (issue != null)
                Issues.Add(issue);
        }

        public void SetMetric(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            Metrics[key] = value ?? string.Empty;
        }
    }
}
