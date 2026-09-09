using System;

namespace MobilePerformanceOptimizer
{
    public static class MPOFormatUtility
    {
        public static string Number(long value)
        {
            return value.ToString("N0");
        }

        public static string Megabytes(double value)
        {
            return value < 0.1 ? "< 0.1 MB" : value.ToString("N1") + " MB";
        }

        public static string Bool(bool value)
        {
            return value ? "On" : "Off";
        }
    }
}
