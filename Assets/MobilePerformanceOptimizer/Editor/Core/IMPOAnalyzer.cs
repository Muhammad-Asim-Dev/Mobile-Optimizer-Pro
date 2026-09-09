using System.Collections;

namespace MobilePerformanceOptimizer
{
    public interface IMPOAnalyzer
    {
        string Name { get; }
        MPOCategory Category { get; }
        void Analyze(MPOScanContext context);
    }

    /// <summary>
    /// Optional cooperative analyzer contract. Long-running analyzers yield regularly so the
    /// Unity Editor can repaint, scroll, process input, and respond to Cancel between chunks.
    /// </summary>
    public interface IMPOIncrementalAnalyzer : IMPOAnalyzer
    {
        IEnumerator AnalyzeIncremental(MPOScanContext context);
    }
}
