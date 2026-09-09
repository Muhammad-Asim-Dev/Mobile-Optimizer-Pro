using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    [Serializable]
    public sealed class MPOReportMetricData
    {
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class MPOReportCategoryData
    {
        public int category;
        public string name;
        public int score;
        public int totalChecks;
        public int passedChecks;
        public int activeIssueCount;
        public List<MPOReportMetricData> metrics = new List<MPOReportMetricData>();
    }

    [Serializable]
    public sealed class MPOReportIssueData
    {
        public string identity;
        public int category;
        public string categoryName;
        public int severity;
        public string severityName;
        public string title;
        public string description;
        public string recommendation;
        public string assetPath;
        public int penalty;
        public string ruleId;
        public int fixKind;
        public string fixKindName;
        public int fixSafety;
        public string fixSafetyName;
        public string fixPreview;
        public int cpuImpact;
        public string cpuImpactName;
        public int gpuImpact;
        public string gpuImpactName;
        public int memoryImpact;
        public string memoryImpactName;
        public int buildSizeImpact;
        public string buildSizeImpactName;
        public int thermalImpact;
        public string thermalImpactName;
        public int impactScore;
        public int confidence;
        public string confidenceName;
        public bool ignored;
    }

    [Serializable]
    public sealed class MPOScanSnapshotData
    {
        public string id;
        public string projectName;
        public string unityVersion;
        public string completedAtUtc;
        public int targetPlatform;
        public string targetPlatformName;
        public int deviceTier;
        public string deviceTierName;
        public int scanScopeMode;
        public string scanScopeName;
        public string scanScopeKey;
        public int overallScore;
        public string rating;
        public int totalChecks;
        public int passedChecks;
        public int criticalCount;
        public int warningCount;
        public int suggestionCount;
        public int fixableCount;
        public int safeFixCount;
        public int ignoredCount;
        public bool wasCancelled;
        public int totalAnalyzerCount;
        public int completedAnalyzerCount;
        public int failedAnalyzerCount;
        public int recoverableErrorCount;
        public List<string> analyzerErrors = new List<string>();
        public List<string> recoverableWarnings = new List<string>();
        public List<MPOReportCategoryData> categories = new List<MPOReportCategoryData>();
        public List<MPOReportIssueData> issues = new List<MPOReportIssueData>();
    }

    public static class MPOReportSnapshotBuilder
    {
        public static MPOScanSnapshotData Build(
            MPOScanResult result,
            MPOTargetPlatform targetPlatform,
            MPODeviceTier deviceTier,
            string existingId = null,
            MPOScanScope scope = null)
        {
            if (result == null)
                return null;

            scope = scope ?? MPOScanScope.FullProject();

            var snapshot = new MPOScanSnapshotData
            {
                id = string.IsNullOrWhiteSpace(existingId) ? Guid.NewGuid().ToString("N") : existingId,
                projectName = string.IsNullOrWhiteSpace(Application.productName) ? "Unity Project" : Application.productName,
                unityVersion = Application.unityVersion,
                completedAtUtc = result.CompletedAtUtc == default(DateTime)
                    ? DateTime.UtcNow.ToString("O")
                    : result.CompletedAtUtc.ToString("O"),
                targetPlatform = (int)targetPlatform,
                targetPlatformName = targetPlatform.ToString(),
                deviceTier = (int)deviceTier,
                deviceTierName = deviceTier.ToString(),
                scanScopeMode = (int)scope.Mode,
                scanScopeName = scope.DisplayName,
                scanScopeKey = scope.ScopeKey,
                overallScore = MPOScoreCalculator.CalculateOverall(result),
                totalChecks = result.TotalChecks,
                passedChecks = result.PassedChecks,
                criticalCount = result.CriticalCount,
                warningCount = result.WarningCount,
                suggestionCount = result.SuggestionCount,
                fixableCount = result.FixableCount,
                safeFixCount = result.SafeFixCount,
                ignoredCount = result.IgnoredCount,
                wasCancelled = result.WasCancelled,
                totalAnalyzerCount = result.TotalAnalyzerCount,
                completedAnalyzerCount = result.CompletedAnalyzerCount,
                failedAnalyzerCount = result.FailedAnalyzerCount,
                recoverableErrorCount = result.RecoverableErrorCount
            };

            snapshot.rating = MPOScoreCalculator.GetRating(snapshot.overallScore);
            snapshot.analyzerErrors.AddRange(result.AnalyzerErrors);
            snapshot.recoverableWarnings.AddRange(result.RecoverableWarnings);

            foreach (MPOCategory category in Enum.GetValues(typeof(MPOCategory)))
            {
                if (!result.Categories.TryGetValue(category, out MPOCategoryResult categoryResult))
                    continue;

                var categoryData = new MPOReportCategoryData
                {
                    category = (int)category,
                    name = category.ToString(),
                    score = MPOScoreCalculator.CalculateCategory(categoryResult),
                    totalChecks = categoryResult.TotalChecks,
                    passedChecks = categoryResult.PassedChecks,
                    activeIssueCount = categoryResult.Issues.Count(issue => !MPOIgnoreStore.IsIgnored(issue))
                };

                foreach (KeyValuePair<string, string> metric in categoryResult.Metrics.OrderBy(pair => pair.Key))
                {
                    categoryData.metrics.Add(new MPOReportMetricData
                    {
                        key = metric.Key,
                        value = metric.Value
                    });
                }

                snapshot.categories.Add(categoryData);
            }

            foreach (MPOIssue issue in result.AllIssues
                         .OrderByDescending(item => item.Severity)
                         .ThenByDescending(item => item.ImpactScore)
                         .ThenByDescending(item => item.Penalty)
                         .ThenBy(item => item.Category)
                         .ThenBy(item => item.Title))
            {
                snapshot.issues.Add(new MPOReportIssueData
                {
                    identity = BuildStableIssueIdentity(issue),
                    category = (int)issue.Category,
                    categoryName = issue.Category.ToString(),
                    severity = (int)issue.Severity,
                    severityName = issue.Severity.ToString(),
                    title = issue.Title,
                    description = issue.Description,
                    recommendation = issue.Recommendation,
                    assetPath = issue.AssetPath,
                    penalty = issue.Penalty,
                    ruleId = issue.RuleId,
                    fixKind = (int)issue.FixKind,
                    fixKindName = issue.FixKind.ToString(),
                    fixSafety = (int)issue.FixSafety,
                    fixSafetyName = issue.FixSafety.ToString(),
                    fixPreview = issue.FixPreview,
                    cpuImpact = (int)issue.CpuImpact,
                    cpuImpactName = MPOImpactUtility.Label(issue.CpuImpact),
                    gpuImpact = (int)issue.GpuImpact,
                    gpuImpactName = MPOImpactUtility.Label(issue.GpuImpact),
                    memoryImpact = (int)issue.MemoryImpact,
                    memoryImpactName = MPOImpactUtility.Label(issue.MemoryImpact),
                    buildSizeImpact = (int)issue.BuildSizeImpact,
                    buildSizeImpactName = MPOImpactUtility.Label(issue.BuildSizeImpact),
                    thermalImpact = (int)issue.ThermalImpact,
                    thermalImpactName = MPOImpactUtility.Label(issue.ThermalImpact),
                    impactScore = issue.ImpactScore,
                    confidence = (int)MPOConfidenceUtility.Get(issue),
                    confidenceName = MPOConfidenceUtility.Label(MPOConfidenceUtility.Get(issue)),
                    ignored = MPOIgnoreStore.IsIgnored(issue)
                });
            }

            return snapshot;
        }

        public static string BuildStableIssueIdentity(MPOIssue issue)
        {
            if (issue == null)
                return string.Empty;

            return (issue.RuleId ?? string.Empty) + "|" +
                   (issue.AssetPath ?? string.Empty) + "|" +
                   (issue.Title ?? string.Empty);
        }
    }
}
