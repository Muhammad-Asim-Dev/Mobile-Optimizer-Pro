using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public static class MPOImpactUtility
    {
        public static string Label(MPOImpactLevel level)
        {
            switch (level)
            {
                case MPOImpactLevel.High: return "High";
                case MPOImpactLevel.Medium: return "Medium";
                case MPOImpactLevel.Low: return "Low";
                default: return "—";
            }
        }

        public static int Score(MPOImpactLevel level)
        {
            return Mathf.Clamp((int)level, 0, 3);
        }

        public static MPOImpactLevel Max(params MPOImpactLevel[] levels)
        {
            MPOImpactLevel value = MPOImpactLevel.None;
            if (levels == null)
                return value;

            for (int i = 0; i < levels.Length; i++)
            {
                if ((int)levels[i] > (int)value)
                    value = levels[i];
            }

            return value;
        }
    }
}
