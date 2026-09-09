using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public static class MPOScoreCalculator
    {
        private static readonly Dictionary<MPOCategory, float> Weights = new Dictionary<MPOCategory, float>
        {
            { MPOCategory.Scene, 1.0f },
            { MPOCategory.Textures, 1.2f },
            { MPOCategory.Meshes, 1.0f },
            { MPOCategory.Materials, 0.8f },
            { MPOCategory.Lighting, 1.1f },
            { MPOCategory.URP, 1.2f },
            { MPOCategory.Particles, 1.0f },
            { MPOCategory.Physics, 1.0f },
            { MPOCategory.UI, 0.9f },
            { MPOCategory.Audio, 0.8f },
            { MPOCategory.Build, 0.8f },
            { MPOCategory.Quality, 1.0f }
        };

        public static int CalculateOverall(MPOScanResult result)
        {
            if (result == null || result.Categories.Count == 0)
                return 100;

            float weightedScore = 0f;
            float totalWeight = 0f;

            foreach (var pair in result.Categories)
            {
                float weight = Weights.TryGetValue(pair.Key, out float configured) ? configured : 1f;
                weightedScore += CalculateCategory(pair.Value) * weight;
                totalWeight += weight;
            }

            return totalWeight <= 0f ? 100 : Mathf.RoundToInt(weightedScore / totalWeight);
        }

        public static int CalculateCategory(MPOCategoryResult result)
        {
            if (result == null)
                return 100;

            IEnumerable<MPOIssue> active = result.Issues.Where(x => !MPOIgnoreStore.IsIgnored(x));
            int criticalPenalty = Mathf.Min(60, active.Where(x => x.Severity == MPOSeverity.Critical).Sum(x => x.Penalty));
            int warningPenalty = Mathf.Min(35, active.Where(x => x.Severity == MPOSeverity.Warning).Sum(x => x.Penalty));
            int suggestionPenalty = Mathf.Min(10, active.Where(x => x.Severity == MPOSeverity.Suggestion).Sum(x => x.Penalty));
            return Mathf.Clamp(100 - criticalPenalty - warningPenalty - suggestionPenalty, 0, 100);
        }

        public static string GetRating(int score)
        {
            if (score >= 90) return "Excellent";
            if (score >= 80) return "Good";
            if (score >= 70) return "Acceptable";
            if (score >= 50) return "Needs Optimization";
            return "Poor";
        }
    }
}
