using System.Collections;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public sealed class AudioAnalyzer : IMPOIncrementalAnalyzer
    {
        public string Name => "Audio Analyzer";
        public MPOCategory Category => MPOCategory.Audio;

        public void Analyze(MPOScanContext context)
        {
            IEnumerator routine = AnalyzeIncremental(context);
            while (routine != null && routine.MoveNext()) { }
        }

        public IEnumerator AnalyzeIncremental(MPOScanContext context)
        {
            MPOCategoryResult result = context.Result.GetOrCreate(Category);
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", MPOAssetScope.GetFindRoots(context.Scope));
            yield return null;

            int scanned = 0;
            int decompressOnLoad = 0;
            int streaming = 0;
            int longDecompress = 0;
            int skipped = 0;
            double totalDuration = 0d;

            for (int i = 0; i < guids.Length; i++)
            {
                if (context.ReportProgress($"{i + 1}/{guids.Length}", guids.Length == 0 ? 1f : i / (float)guids.Length))
                    yield break;

                string path;
                if (!context.TryGet("Audio GUID " + guids[i], () => AssetDatabase.GUIDToAssetPath(guids[i]), out path) ||
                    !MPOAssetScope.ShouldScan(path, context.Scope))
                {
                    if ((i & 7) == 7)
                        yield return null;
                    continue;
                }

                bool ok = context.TryExecute("Audio asset: " + path, () =>
                {
                    AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (importer == null || clip == null)
                    {
                        context.RecordRecoverableWarning(path, "Audio importer or clip could not be loaded and was skipped.");
                        return;
                    }

                    scanned++;
                    totalDuration += clip.length;
                    AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                    if (settings.loadType == AudioClipLoadType.DecompressOnLoad) decompressOnLoad++;
                    if (settings.loadType == AudioClipLoadType.Streaming) streaming++;

                    bool longClip = clip.length >= context.Profile.LongAudioSeconds;
                    bool veryLongClip = clip.length >= context.Profile.VeryLongAudioSeconds;
                    bool longAndDecompressed = longClip && settings.loadType == AudioClipLoadType.DecompressOnLoad;
                    bool veryLongNotStreaming = veryLongClip && settings.loadType != AudioClipLoadType.Streaming;
                    bool pcmLong = longClip && settings.compressionFormat == AudioCompressionFormat.PCM;

                    if (!longAndDecompressed && !veryLongNotStreaming && !pcmLong)
                    {
                        result.AddPass();
                        return;
                    }

                    if (longAndDecompressed) longDecompress++;
                    double estimatedPcmMb = (double)clip.samples * Mathf.Max(1, clip.channels) * 2d / (1024d * 1024d);

                    var details = new StringBuilder();
                    details.AppendLine($"Length: {clip.length:0.0} seconds");
                    details.AppendLine("Load Type: " + settings.loadType);
                    details.AppendLine("Compression: " + settings.compressionFormat);
                    details.AppendLine("Preload Audio Data: " + MPOFormatUtility.Bool(settings.preloadAudioData));
                    details.AppendLine("Channels: " + clip.channels);
                    details.Append($"Approx. 16-bit PCM footprint: {MPOFormatUtility.Megabytes(estimatedPcmMb)}");

                    var recommendation = new StringBuilder();
                    MPOSeverity severity = MPOSeverity.Warning;
                    int penalty = 3;
                    if (veryLongClip && estimatedPcmMb >= 16d)
                    {
                        severity = MPOSeverity.Critical;
                        penalty = 5;
                    }

                    if (longAndDecompressed)
                        recommendation.AppendLine("• Long clips using Decompress On Load can consume significant memory. Consider Streaming for music/long ambience, or Compressed In Memory when streaming is not appropriate.");
                    if (veryLongNotStreaming)
                        recommendation.AppendLine("• Very long clip is not Streaming. Review runtime memory, latency and playback requirements before choosing a different load type.");
                    if (pcmLong)
                        recommendation.AppendLine("• PCM on a long clip is large. Use an appropriate compressed format unless PCM quality/latency is specifically required.");

                    bool canOfferStreamingFix = longAndDecompressed || veryLongNotStreaming;
                    result.AddIssue(new MPOIssue(
                        Category,
                        severity,
                        "Long audio clip needs memory review",
                        details.ToString(),
                        recommendation.ToString().TrimEnd(),
                        penalty,
                        null,
                        path,
                        "audio.long-clip-load-type",
                        canOfferStreamingFix ? MPOFixKind.StreamLongAudio : MPOFixKind.None,
                        canOfferStreamingFix ? MPOFixSafety.ReviewRequired : MPOFixSafety.Manual,
                        canOfferStreamingFix ? "Load Type: " + settings.loadType + " → Streaming\nPreload Audio Data: " + MPOFormatUtility.Bool(settings.preloadAudioData) + " → Off\n\nRecommended mainly for long music/ambience. Verify seek latency, looping, and platform playback after applying." : null,
                        cpuImpact: settings.loadType == AudioClipLoadType.DecompressOnLoad ? MPOImpactLevel.Low : MPOImpactLevel.Medium,
                        memoryImpact: estimatedPcmMb >= 32d ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                        buildSizeImpact: pcmLong ? MPOImpactLevel.High : MPOImpactLevel.Medium,
                        thermalImpact: MPOImpactLevel.Low));
                });

                if (!ok) skipped++;

                if ((i & 7) == 7)
                    yield return null;
            }

            result.SetMetric("Audio Clips", MPOFormatUtility.Number(scanned));
            result.SetMetric("Total Duration", FormatDuration(totalDuration));
            result.SetMetric("Decompress On Load", MPOFormatUtility.Number(decompressOnLoad));
            result.SetMetric("Streaming", MPOFormatUtility.Number(streaming));
            result.SetMetric("Long + Decompressed", MPOFormatUtility.Number(longDecompress));
            result.SetMetric("Skipped Audio", MPOFormatUtility.Number(skipped));
            context.ReportProgress("Audio scan complete", 1f);
        }

        private static string FormatDuration(double seconds)
        {
            int total = Mathf.RoundToInt((float)seconds);
            int minutes = total / 60;
            int remainder = total % 60;
            return minutes + "m " + remainder + "s";
        }
    }
}
