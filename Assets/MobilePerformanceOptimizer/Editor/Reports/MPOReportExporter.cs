using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public static class MPOReportExporter
    {
        public static bool ExportHtml(MPOScanSnapshotData snapshot, out string exportedPath)
        {
            return Export(snapshot, "html", BuildHtml, out exportedPath);
        }

        public static bool ExportJson(MPOScanSnapshotData snapshot, out string exportedPath)
        {
            return Export(snapshot, "json", data => JsonUtility.ToJson(data, true), out exportedPath);
        }

        public static bool ExportCsv(MPOScanSnapshotData snapshot, out string exportedPath)
        {
            return Export(snapshot, "csv", BuildCsv, out exportedPath);
        }

        private static bool Export(MPOScanSnapshotData snapshot, string extension, Func<MPOScanSnapshotData, string> contentBuilder, out string exportedPath)
        {
            exportedPath = string.Empty;
            if (snapshot == null || contentBuilder == null)
                return false;

            try
            {
                exportedPath = AskForPath(snapshot, extension);
                if (string.IsNullOrEmpty(exportedPath))
                    return false;

                string directory = Path.GetDirectoryName(exportedPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(exportedPath, contentBuilder(snapshot), new UTF8Encoding(false));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Report Export Failed",
                    "The report could not be written. Check the Console for details.\n\n" + exception.Message,
                    "OK");
                exportedPath = string.Empty;
                return false;
            }
        }

        private static string AskForPath(MPOScanSnapshotData snapshot, string extension)
        {
            if (snapshot == null)
                return string.Empty;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fileName = BuildFileName(snapshot) + "." + extension;
            return EditorUtility.SaveFilePanel("Export Mobile Performance Optimizer Report", projectRoot, fileName, extension);
        }

        private static string BuildFileName(MPOScanSnapshotData snapshot)
        {
            DateTime date;
            if (!DateTime.TryParse(snapshot.completedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date))
                date = DateTime.Now;
            string raw = "MPO_" + snapshot.projectName + "_" + snapshot.targetPlatformName + "_" + snapshot.deviceTierName + "_" + date.ToLocalTime().ToString("yyyyMMdd_HHmmss");
            foreach (char invalid in Path.GetInvalidFileNameChars())
                raw = raw.Replace(invalid, '_');
            return raw;
        }

        private static string BuildHtml(MPOScanSnapshotData snapshot)
        {
            var builder = new StringBuilder(32768);
            builder.AppendLine("<!doctype html>");
            builder.AppendLine("<html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            builder.AppendLine("<title>Mobile Performance Optimizer Report</title>");
            builder.AppendLine("<style>body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;margin:0;background:#f5f6f8;color:#20232a}.wrap{max-width:1180px;margin:0 auto;padding:32px}.card{background:white;border:1px solid #dfe3e8;border-radius:10px;padding:18px;margin:12px 0}.hero{display:flex;gap:28px;align-items:center}.score{font-size:48px;font-weight:700}.muted{color:#667085}.stats{display:flex;gap:12px;flex-wrap:wrap}.stat{min-width:130px;background:#f8fafc;border-radius:8px;padding:12px}.stat b{font-size:22px;display:block}.critical{border-left:5px solid #b42318}.warning{border-left:5px solid #b54708}.suggestion{border-left:5px solid #175cd3}.ignored{opacity:.62}.pill{display:inline-block;padding:3px 8px;border-radius:999px;background:#eef2f6;font-size:12px;margin-right:6px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:10px}.metric{font-size:13px;margin-top:6px}h1,h2,h3{margin-top:0}code{word-break:break-all}table{width:100%;border-collapse:collapse}td,th{text-align:left;padding:9px;border-bottom:1px solid #e5e7eb}</style></head><body><div class=\"wrap\">");
            builder.Append("<h1>Mobile Performance Optimizer</h1><p class=\"muted\">Static project analysis report — not an FPS prediction.</p>");

            builder.Append("<div class=\"card hero\"><div><div class=\"score\">").Append(snapshot.overallScore).Append(" / 100</div><b>").Append(Html(snapshot.rating)).Append("</b></div><div>");
            builder.Append("<div><b>").Append(Html(snapshot.projectName)).Append("</b></div>");
            builder.Append("<div class=\"muted\">Unity ").Append(Html(snapshot.unityVersion)).Append(" • ").Append(Html(snapshot.targetPlatformName)).Append(" • ").Append(Html(snapshot.deviceTierName)).Append(" • ").Append(Html(string.IsNullOrWhiteSpace(snapshot.scanScopeName) ? "Full Project" : snapshot.scanScopeName)).Append("</div>");
            builder.Append("<div class=\"muted\">Completed: ").Append(Html(FormatDate(snapshot.completedAtUtc))).Append("</div></div></div>");

            builder.Append("<div class=\"stats\">");
            AppendStat(builder, "Critical", snapshot.criticalCount);
            AppendStat(builder, "Warnings", snapshot.warningCount);
            AppendStat(builder, "Suggestions", snapshot.suggestionCount);
            AppendStat(builder, "Fixable", snapshot.fixableCount);
            AppendStat(builder, "Ignored", snapshot.ignoredCount);
            AppendStat(builder, "Checks", snapshot.totalChecks);
            AppendStat(builder, "Analyzers", snapshot.completedAnalyzerCount + "/" + snapshot.totalAnalyzerCount);
            AppendStat(builder, "Skipped Items", snapshot.recoverableErrorCount);
            builder.Append("</div>");

            var priorityGroups = snapshot.issues
                .Where(issue => !issue.ignored)
                .GroupBy(issue => issue.categoryName + "|" + issue.ruleId + "|" + issue.title)
                .Select(group => new
                {
                    Representative = group.First(),
                    Count = group.Count(),
                    Severity = group.Max(issue => issue.severity),
                    Impact = group.Max(issue => Math.Max(issue.cpuImpact, Math.Max(issue.gpuImpact, Math.Max(issue.memoryImpact, Math.Max(issue.buildSizeImpact, issue.thermalImpact))))),
                    Penalty = group.Sum(issue => issue.penalty)
                })
                .OrderByDescending(group => group.Severity)
                .ThenByDescending(group => group.Impact)
                .ThenByDescending(group => Math.Min(100, group.Penalty))
                .ThenByDescending(group => Math.Min(25, group.Count))
                .Take(5)
                .ToList();

            if (priorityGroups.Count > 0)
            {
                builder.Append("<h2>Top Problems to Fix First</h2>");
                int rank = 1;
                foreach (var group in priorityGroups)
                {
                    MPOReportIssueData issue = group.Representative;
                    builder.Append("<div class=\"card ").Append(SeverityCss(issue.severityName)).Append("\">");
                    builder.Append("<span class=\"pill\">#").Append(rank++).Append("</span>");
                    builder.Append("<span class=\"pill\">").Append(Html(issue.severityName)).Append("</span>");
                    builder.Append("<span class=\"pill\">").Append(Html(issue.categoryName)).Append("</span>");
                    builder.Append("<span class=\"pill\">").Append(group.Count).Append(" finding(s)</span>");
                    builder.Append("<h3>").Append(Html(issue.title)).Append("</h3>");
                    builder.Append("<p class=\"muted\"><b>Confidence:</b> ").Append(Html(issue.confidenceName))
                        .Append(" • <b>Highest estimated impact:</b> ").Append(ImpactName(group.Impact))
                        .Append(" • <b>Rule:</b> ").Append(Html(issue.ruleId)).Append("</p>");
                    if (!string.IsNullOrWhiteSpace(issue.recommendation))
                        builder.Append("<p><b>Recommended action:</b> ").Append(Html(issue.recommendation)).Append("</p>");
                    builder.Append("</div>");
                }
            }

            builder.Append("<h2>Category Health</h2><div class=\"grid\">");
            foreach (MPOReportCategoryData category in snapshot.categories)
            {
                builder.Append("<div class=\"card\"><h3>").Append(Html(category.name)).Append(" — ").Append(category.score).Append("/100</h3>");
                builder.Append("<div class=\"muted\">").Append(category.activeIssueCount).Append(" active issue(s) • ").Append(category.passedChecks).Append("/").Append(category.totalChecks).Append(" checks passed</div>");
                foreach (MPOReportMetricData metric in category.metrics)
                    builder.Append("<div class=\"metric\"><b>").Append(Html(metric.key)).Append(":</b> ").Append(Html(metric.value)).Append("</div>");
                builder.Append("</div>");
            }
            builder.Append("</div>");

            builder.Append("<h2>Findings</h2>");
            foreach (MPOReportIssueData issue in snapshot.issues)
            {
                string css = issue.ignored ? "card ignored" : "card " + SeverityCss(issue.severityName);
                builder.Append("<div class=\"").Append(css).Append("\">");
                builder.Append("<span class=\"pill\">").Append(Html(issue.severityName)).Append("</span><span class=\"pill\">").Append(Html(issue.categoryName)).Append("</span><span class=\"pill\">").Append(Html(issue.fixSafetyName)).Append("</span><span class=\"pill\">").Append(Html(issue.confidenceName)).Append("</span>");
                if (issue.ignored) builder.Append("<span class=\"pill\">Ignored</span>");
                builder.Append("<h3>").Append(Html(issue.title)).Append("</h3>");
                builder.Append("<p class=\"muted\"><b>Estimated impact:</b> CPU ").Append(Html(issue.cpuImpactName))
                    .Append(" • GPU ").Append(Html(issue.gpuImpactName))
                    .Append(" • Memory ").Append(Html(issue.memoryImpactName))
                    .Append(" • Build Size ").Append(Html(issue.buildSizeImpactName))
                    .Append(" • Thermal ").Append(Html(issue.thermalImpactName)).Append("</p>");
                builder.Append("<p><b>Why it matters:</b> ").Append(Html(issue.description)).Append("</p>");
                if (!string.IsNullOrWhiteSpace(issue.recommendation))
                    builder.Append("<p><b>Recommended action:</b> ").Append(Html(issue.recommendation)).Append("</p>");
                if (!string.IsNullOrWhiteSpace(issue.fixPreview))
                    builder.Append("<p><b>Fix preview:</b> ").Append(Html(issue.fixPreview)).Append("</p>");
                if (!string.IsNullOrWhiteSpace(issue.assetPath))
                    builder.Append("<p><b>Asset:</b> <code>").Append(Html(issue.assetPath)).Append("</code></p>");
                builder.Append("<div class=\"muted\">Rule: ").Append(Html(issue.ruleId)).Append(" • Penalty: ").Append(issue.penalty).Append("</div></div>");
            }

            if (snapshot.analyzerErrors.Count > 0)
            {
                builder.Append("<h2>Analyzer Notes</h2><div class=\"card warning\"><ul>");
                foreach (string error in snapshot.analyzerErrors)
                    builder.Append("<li>").Append(Html(error)).Append("</li>");
                builder.Append("</ul></div>");
            }

            if (snapshot.recoverableErrorCount > 0)
            {
                builder.Append("<h2>Recoverable Scan Warnings</h2><div class=\"card warning\">");
                builder.Append("<p>").Append(snapshot.recoverableErrorCount).Append(" item(s) were skipped because Unity could not read them safely. The scan continued.</p><ul>");
                foreach (string warning in snapshot.recoverableWarnings)
                    builder.Append("<li>").Append(Html(warning)).Append("</li>");
                builder.Append("</ul></div>");
            }

            builder.Append("<p class=\"muted\">Generated by Mobile Performance Optimizer ").Append(Html(MPOConstants.Version)).Append(".</p>");
            builder.AppendLine("</div></body></html>");
            return builder.ToString();
        }

        private static string BuildCsv(MPOScanSnapshotData snapshot)
        {
            var builder = new StringBuilder(16384);
            builder.AppendLine("Project,Unity Version,Platform,Device Tier,Scan Scope,Score,Completed UTC,Analyzers Completed,Analyzers Total,Analyzer Failures,Recoverable Skips");
            builder.Append(Csv(snapshot.projectName)).Append(',')
                .Append(Csv(snapshot.unityVersion)).Append(',')
                .Append(Csv(snapshot.targetPlatformName)).Append(',')
                .Append(Csv(snapshot.deviceTierName)).Append(',')
                .Append(Csv(string.IsNullOrWhiteSpace(snapshot.scanScopeName) ? "Full Project" : snapshot.scanScopeName)).Append(',')
                .Append(snapshot.overallScore).Append(',')
                .Append(Csv(snapshot.completedAtUtc)).Append(',')
                .Append(snapshot.completedAnalyzerCount).Append(',')
                .Append(snapshot.totalAnalyzerCount).Append(',')
                .Append(snapshot.failedAnalyzerCount).Append(',')
                .Append(snapshot.recoverableErrorCount).AppendLine();
            builder.AppendLine();
            builder.AppendLine("Status,Severity,Category,Title,Asset Path,Rule ID,Fix Safety,Confidence,Penalty,CPU Impact,GPU Impact,Memory Impact,Build Size Impact,Thermal Impact,Impact Score,Description,Recommendation,Fix Preview");

            foreach (MPOReportIssueData issue in snapshot.issues)
            {
                builder.Append(Csv(issue.ignored ? "Ignored" : "Active")).Append(',')
                    .Append(Csv(issue.severityName)).Append(',')
                    .Append(Csv(issue.categoryName)).Append(',')
                    .Append(Csv(issue.title)).Append(',')
                    .Append(Csv(issue.assetPath)).Append(',')
                    .Append(Csv(issue.ruleId)).Append(',')
                    .Append(Csv(issue.fixSafetyName)).Append(',')
                    .Append(Csv(issue.confidenceName)).Append(',')
                    .Append(issue.penalty).Append(',')
                    .Append(Csv(issue.cpuImpactName)).Append(',')
                    .Append(Csv(issue.gpuImpactName)).Append(',')
                    .Append(Csv(issue.memoryImpactName)).Append(',')
                    .Append(Csv(issue.buildSizeImpactName)).Append(',')
                    .Append(Csv(issue.thermalImpactName)).Append(',')
                    .Append(issue.impactScore).Append(',')
                    .Append(Csv(issue.description)).Append(',')
                    .Append(Csv(issue.recommendation)).Append(',')
                    .Append(Csv(issue.fixPreview)).AppendLine();
            }

            return builder.ToString();
        }

        private static void AppendStat(StringBuilder builder, string label, int value)
        {
            builder.Append("<div class=\"stat\"><span class=\"muted\">").Append(Html(label)).Append("</span><b>").Append(value).Append("</b></div>");
        }

        private static void AppendStat(StringBuilder builder, string label, string value)
        {
            builder.Append("<div class=\"stat\"><span class=\"muted\">").Append(Html(label)).Append("</span><b>").Append(Html(value)).Append("</b></div>");
        }

        private static string Html(string value)
        {
            string safe = value ?? string.Empty;
            return safe
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;")
                .Replace("\n", "<br>");
        }

        private static string Csv(string value)
        {
            string safe = (value ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
            return "\"" + safe.Replace("\"", "\"\"") + "\"";
        }

        private static string ImpactName(int impact)
        {
            if (impact >= 3) return "High";
            if (impact == 2) return "Medium";
            if (impact == 1) return "Low";
            return "—";
        }

        private static string SeverityCss(string severity)
        {
            if (string.Equals(severity, "Critical", StringComparison.OrdinalIgnoreCase)) return "critical";
            if (string.Equals(severity, "Warning", StringComparison.OrdinalIgnoreCase)) return "warning";
            return "suggestion";
        }

        private static string FormatDate(string utc)
        {
            if (DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
                return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            return utc ?? string.Empty;
        }
    }
}
