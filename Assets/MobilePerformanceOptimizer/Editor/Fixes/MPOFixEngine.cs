using UnityEditor;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    public static class MPOFixEngine
    {
        public static string GetActionKey(MPOIssue issue)
        {
            if (issue == null)
                return string.Empty;

            return issue.FixKind + "|" +
                   (issue.AssetPath ?? string.Empty) + "|" +
                   issue.FixIntValue + "|" +
                   (issue.FixStringValue ?? string.Empty) + "|" +
                   (issue.ContextObject != null ? GlobalObjectId.GetGlobalObjectIdSlow(issue.ContextObject) + ":" + issue.ContextObject.GetInstanceID() : string.Empty) + "|" + issue.RuleId;
        }

        public static bool Apply(MPOIssue issue, out string message)
        {
            if (issue == null || !issue.CanFix)
            {
                message = "This issue does not have an automatic fix.";
                return false;
            }

            try
            {
                if (issue.FixKind == MPOFixKind.DisableDevelopmentBuildFlags)
                    return DisableDevelopmentBuildFlags(out message);
                return MPOFixPlans.Apply(MPOFixPlans.Create(issue), out message) == MPOApplyStatus.Applied;
            }
            catch (System.Exception e) { message = e.Message; return false; }
        }

        private static bool DisableDevelopmentBuildFlags(out string message)
        {
            string key = "build.flags";
            if (!MPOFixSession.HasKey(key))
            {
                MPOFixSession.Add(new MPOFixSnapshotData
                {
                    key = key,
                    kind = (int)MPOFixKind.DisableDevelopmentBuildFlags,
                    bool0 = EditorUserBuildSettings.development,
                    bool1 = EditorUserBuildSettings.allowDebugging,
                    bool2 = EditorUserBuildSettings.connectProfiler,
                    bool3 = EditorUserBuildSettings.buildWithDeepProfilingSupport
                });
            }

            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
            message = "Disabled Development Build, Script Debugging, Autoconnect Profiler, and Deep Profiling build flags.";
            return !EditorUserBuildSettings.development && !EditorUserBuildSettings.allowDebugging &&
                !EditorUserBuildSettings.connectProfiler && !EditorUserBuildSettings.buildWithDeepProfilingSupport;
        }

    }
}
