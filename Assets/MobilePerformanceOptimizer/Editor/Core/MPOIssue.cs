using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public sealed class MPOIssue
    {
        public MPOCategory Category { get; }
        public MPOSeverity Severity { get; }
        public string Title { get; }
        public string Description { get; }
        public string Recommendation { get; }
        public string AssetPath { get; }
        public UnityEngine.Object ContextObject { get; }
        public int Penalty { get; }
        public string RuleId { get; }
        public MPOFixKind FixKind { get; }
        public MPOFixSafety FixSafety { get; }
        public string FixPreview { get; }
        public int FixIntValue { get; }
        public string FixStringValue { get; }

        public MPOImpactLevel CpuImpact { get; }
        public MPOImpactLevel GpuImpact { get; }
        public MPOImpactLevel MemoryImpact { get; }
        public MPOImpactLevel BuildSizeImpact { get; }
        public MPOImpactLevel ThermalImpact { get; }

        public System.Collections.Generic.Dictionary<string, object> SettingRecommendations { get; } = new System.Collections.Generic.Dictionary<string, object>();

        public bool CanFix => FixKind != MPOFixKind.None;

        public MPOImpactLevel HighestImpact => MPOImpactUtility.Max(
            CpuImpact,
            GpuImpact,
            MemoryImpact,
            BuildSizeImpact,
            ThermalImpact);

        public int ImpactScore =>
            MPOImpactUtility.Score(CpuImpact) +
            MPOImpactUtility.Score(GpuImpact) +
            MPOImpactUtility.Score(MemoryImpact) +
            MPOImpactUtility.Score(BuildSizeImpact) +
            MPOImpactUtility.Score(ThermalImpact);

        public string IdentityKey
        {
            get
            {
                string objectId = ContextObject != null ? ContextObject.GetInstanceID().ToString() : string.Empty;
                return RuleId + "|" + AssetPath + "|" + objectId + "|" + Title;
            }
        }

        public MPOIssue(
            MPOCategory category,
            MPOSeverity severity,
            string title,
            string description,
            string recommendation,
            int penalty,
            UnityEngine.Object contextObject = null,
            string assetPath = null,
            string ruleId = null,
            MPOFixKind fixKind = MPOFixKind.None,
            MPOFixSafety fixSafety = MPOFixSafety.Manual,
            string fixPreview = null,
            int fixIntValue = 0,
            string fixStringValue = null,
            MPOImpactLevel cpuImpact = MPOImpactLevel.None,
            MPOImpactLevel gpuImpact = MPOImpactLevel.None,
            MPOImpactLevel memoryImpact = MPOImpactLevel.None,
            MPOImpactLevel buildSizeImpact = MPOImpactLevel.None,
            MPOImpactLevel thermalImpact = MPOImpactLevel.None)
        {
            Category = category;
            Severity = severity;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Recommendation = recommendation ?? string.Empty;
            Penalty = Mathf.Max(0, penalty);
            ContextObject = contextObject;
            AssetPath = assetPath ?? string.Empty;
            RuleId = string.IsNullOrWhiteSpace(ruleId) ? BuildRuleId(category, title) : ruleId;
            FixKind = fixKind;
            FixSafety = fixKind == MPOFixKind.None ? MPOFixSafety.Manual : fixSafety;
            FixPreview = fixPreview ?? string.Empty;
            FixIntValue = fixIntValue;
            FixStringValue = fixStringValue ?? string.Empty;
            CpuImpact = cpuImpact;
            GpuImpact = gpuImpact;
            MemoryImpact = memoryImpact;
            BuildSizeImpact = buildSizeImpact;
            ThermalImpact = thermalImpact;
        }

        private static string BuildRuleId(MPOCategory category, string title)
        {
            string source = (title ?? "Issue").Trim().ToLowerInvariant();
            char[] chars = source.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]))
                    chars[i] = '-';
            }
            return category.ToString().ToLowerInvariant() + "." + new string(chars).Trim('-');
        }
    }
}
