namespace MobilePerformanceOptimizer
{
    public enum MPOConfidenceLevel
    {
        Informational = 0,
        ReviewRecommended = 1,
        High = 2
    }

    /// <summary>
    /// Communicates how deterministic a static recommendation is. This is intentionally separate
    /// from severity: a severe issue can still require visual/gameplay review before changing it.
    /// </summary>
    public static class MPOConfidenceUtility
    {
        public static MPOConfidenceLevel Get(MPOIssue issue)
        {
            if (issue == null)
                return MPOConfidenceLevel.Informational;

            if (issue.CanFix && issue.FixSafety == MPOFixSafety.Safe)
                return MPOConfidenceLevel.High;

            if (issue.Severity == MPOSeverity.Suggestion)
                return MPOConfidenceLevel.Informational;

            switch (issue.Category)
            {
                case MPOCategory.Build:
                case MPOCategory.Quality:
                case MPOCategory.URP:
                    return MPOConfidenceLevel.High;

                case MPOCategory.Textures:
                case MPOCategory.Meshes:
                case MPOCategory.Materials:
                case MPOCategory.Lighting:
                case MPOCategory.Particles:
                case MPOCategory.Physics:
                case MPOCategory.UI:
                case MPOCategory.Audio:
                    return MPOConfidenceLevel.ReviewRecommended;

                default:
                    return issue.Severity == MPOSeverity.Critical
                        ? MPOConfidenceLevel.High
                        : MPOConfidenceLevel.ReviewRecommended;
            }
        }

        public static string Label(MPOConfidenceLevel confidence)
        {
            switch (confidence)
            {
                case MPOConfidenceLevel.High: return "HIGH CONFIDENCE";
                case MPOConfidenceLevel.ReviewRecommended: return "REVIEW RECOMMENDED";
                default: return "INFORMATIONAL";
            }
        }

        public static string UiClass(MPOConfidenceLevel confidence)
        {
            switch (confidence)
            {
                case MPOConfidenceLevel.High: return "mpo-impact-low";
                case MPOConfidenceLevel.ReviewRecommended: return "mpo-impact-medium";
                default: return "mpo-impact-none";
            }
        }
    }
}
