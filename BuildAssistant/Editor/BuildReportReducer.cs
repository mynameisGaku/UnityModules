using System;
using System.Collections.Generic;
using UnityEditor.Build.Reporting;

namespace BuildAssistant.Editor
{
    internal sealed class BuildReportReduction
    {
        internal BuildReportReduction(bool buildSucceeded, BuildAssistantError error, string message, BuildAssistantHistoryEntry entry)
        {
            BuildSucceeded = buildSucceeded;
            Error = error;
            Message = message ?? string.Empty;
            Entry = entry;
        }

        internal bool BuildSucceeded { get; }
        internal BuildAssistantError Error { get; }
        internal string Message { get; }
        internal BuildAssistantHistoryEntry Entry { get; }
    }

    internal static class BuildReportReducer
    {
        internal static BuildReportReduction Reduce(BuildReport report, BuildAssistantPlan plan, DateTime fallbackStartedAtUtc, DateTime fallbackCompletedAtUtc)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (report == null)
                return CreateFailure(plan, fallbackStartedAtUtc, fallbackCompletedAtUtc, BuildAssistantError.BuildReportUnavailable, "Unity returned no BuildReport.");
            return Reduce(new UnityBuildReportView(report), plan, fallbackStartedAtUtc, fallbackCompletedAtUtc);
        }

        internal static BuildReportReduction Reduce(IBuildReportView report, BuildAssistantPlan plan, DateTime fallbackStartedAtUtc, DateTime fallbackCompletedAtUtc)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            BuildResult buildResult;
            try
            {
                buildResult = report.Result;
            }
            catch (Exception exception)
            {
                return CreateFailure(plan, fallbackStartedAtUtc, fallbackCompletedAtUtc, BuildAssistantError.ReportReadFailed, exception.Message);
            }

            var succeeded = buildResult == BuildResult.Succeeded;
            var buildError = succeeded ? BuildAssistantError.None : BuildAssistantError.BuildInvocationFailed;
            var buildMessage = succeeded ? string.Empty : "Unity reported build result: " + buildResult + ".";

            DateTime startedAtUtc;
            DateTime completedAtUtc;
            int totalErrors;
            int totalWarnings;
            ulong totalOutputBytes;
            try
            {
                startedAtUtc = ToUtcOrFallback(report.BuildStartedAt, fallbackStartedAtUtc);
                completedAtUtc = ToUtcOrFallback(report.BuildEndedAt, fallbackCompletedAtUtc);
                totalErrors = report.TotalErrors;
                totalWarnings = report.TotalWarnings;
                totalOutputBytes = report.TotalSize;
            }
            catch (Exception exception)
            {
                var summaryError = succeeded ? BuildAssistantError.ReportReadFailed : buildError;
                var summaryMessage = succeeded ? "The player build succeeded, but summary analytics could not be read: " + exception.Message : buildMessage + " Summary analytics also could not be read: " + exception.Message;
                var emptyAggregation = SizeAggregator.Aggregate(Array.Empty<PackedAssetRow>(), Array.Empty<ulong>());
                var entry = CreateEntry(plan, fallbackStartedAtUtc, fallbackCompletedAtUtc, succeeded ? BuildAssistantHistoryStatus.Succeeded : BuildAssistantHistoryStatus.Failed, summaryError, summaryMessage, 0, 0, 0, emptyAggregation, plan.PreviousComparableSuccess?.RunId ?? string.Empty, 0, 0);
                return new BuildReportReduction(succeeded, summaryError, summaryMessage, entry);
            }

            try
            {
                var rows = new List<PackedAssetRow>();
                var overheads = new List<ulong>();
                var packedAssets = report.PackedAssets ?? Array.Empty<PackedAssets>();
                for (var packedIndex = 0; packedIndex < packedAssets.Length; packedIndex++)
                {
                    var packed = packedAssets[packedIndex];
                    if (packed == null)
                        continue;
                    overheads.Add(packed.overhead);
                    var contents = packed.contents ?? Array.Empty<PackedAssetInfo>();
                    for (var contentIndex = 0; contentIndex < contents.Length; contentIndex++)
                    {
                        var content = contents[contentIndex];
                        var guid = content.sourceAssetGUID.ToString();
                        var assetKey = !string.IsNullOrEmpty(content.sourceAssetPath) ? content.sourceAssetPath : !IsEmptyGuid(guid) ? "guid:" + guid : $"generated:{packedIndex:D4}:{contentIndex:D6}:{content.id}";
                        var typeName = content.type?.AssemblyQualifiedName ?? "[unknown]";
                        rows.Add(new PackedAssetRow(assetKey, typeName, content.packedSize));
                    }
                }

                var aggregation = SizeAggregator.Aggregate(rows, overheads);
                var previous = plan.PreviousComparableSuccess;
                var outputDelta = previous == null ? 0 : HistoryComparer.Difference(totalOutputBytes, previous.TotalOutputBytes);
                var packedDelta = previous == null ? 0 : HistoryComparer.Difference(aggregation.PackedContentBytes, previous.PackedContentBytes);
                var entry = CreateEntry(plan, startedAtUtc, completedAtUtc, succeeded ? BuildAssistantHistoryStatus.Succeeded : BuildAssistantHistoryStatus.Failed, buildError, buildMessage, totalErrors, totalWarnings, totalOutputBytes, aggregation, previous?.RunId ?? string.Empty, outputDelta, packedDelta);
                return new BuildReportReduction(succeeded, buildError, buildMessage, entry);
            }
            catch (Exception exception)
            {
                var analyticsError = succeeded ? BuildAssistantError.ReportReadFailed : buildError;
                var analyticsMessage = succeeded ? "The player build succeeded, but packed analytics could not be reduced: " + exception.Message : buildMessage + " Packed analytics also could not be reduced: " + exception.Message;
                var emptyAggregation = SizeAggregator.Aggregate(Array.Empty<PackedAssetRow>(), Array.Empty<ulong>());
                var entry = CreateEntry(plan, startedAtUtc, completedAtUtc, succeeded ? BuildAssistantHistoryStatus.Succeeded : BuildAssistantHistoryStatus.Failed, analyticsError, analyticsMessage, totalErrors, totalWarnings, totalOutputBytes, emptyAggregation, plan.PreviousComparableSuccess?.RunId ?? string.Empty, 0, 0);
                return new BuildReportReduction(succeeded, analyticsError, analyticsMessage, entry);
            }
        }

        internal static BuildReportReduction CreateFailure(BuildAssistantPlan plan, DateTime startedAtUtc, DateTime completedAtUtc, BuildAssistantError error, string message)
        {
            var aggregation = SizeAggregator.Aggregate(Array.Empty<PackedAssetRow>(), Array.Empty<ulong>());
            var entry = CreateEntry(plan, startedAtUtc, completedAtUtc, BuildAssistantHistoryStatus.Failed, error, message, 0, 0, 0, aggregation, plan.PreviousComparableSuccess?.RunId ?? string.Empty, 0, 0);
            return new BuildReportReduction(false, error, message, entry);
        }

        private static BuildAssistantHistoryEntry CreateEntry(BuildAssistantPlan plan, DateTime startedAtUtc, DateTime completedAtUtc, BuildAssistantHistoryStatus status, BuildAssistantError error, string message, int totalErrors, int totalWarnings, ulong totalOutputBytes, SizeAggregation aggregation, string previousRunId, long outputDelta, long packedDelta)
        {
            var terminalTime = completedAtUtc < startedAtUtc ? startedAtUtc : completedAtUtc;
            return new BuildAssistantHistoryEntry(plan.RunId, plan.CreatedAtUtc, startedAtUtc, terminalTime, status, error, message, plan.OutputRoot, plan.RunDirectory, plan.ArtifactPath, plan.ProfileKind, plan.ProfileGuid, plan.ProfileName, plan.ProfilePath, plan.ProfileDependencyHash, plan.ProfileStableId, plan.Target, plan.TargetGroup, plan.NamedBuildTarget, plan.Subtarget, plan.ScriptingBackend, plan.Options, plan.EffectiveDefines, plan.Scenes, totalErrors, totalWarnings, totalOutputBytes, aggregation.PackedContentBytes, aggregation.PackedOverheadBytes, aggregation.Assets, aggregation.Types, previousRunId, outputDelta, packedDelta);
        }

        private static DateTime ToUtcOrFallback(DateTime value, DateTime fallback) => value == default ? fallback : value.ToUniversalTime();
        private static bool IsEmptyGuid(string value) => string.IsNullOrEmpty(value) || value == "00000000000000000000000000000000";

        private sealed class UnityBuildReportView : IBuildReportView
        {
            private readonly BuildReport report;

            internal UnityBuildReportView(BuildReport report)
            {
                this.report = report ?? throw new ArgumentNullException(nameof(report));
            }

            public BuildResult Result => report.summary.result;
            public DateTime BuildStartedAt => report.summary.buildStartedAt;
            public DateTime BuildEndedAt => report.summary.buildEndedAt;
            public int TotalErrors => report.summary.totalErrors;
            public int TotalWarnings => report.summary.totalWarnings;
            public ulong TotalSize => report.summary.totalSize;
            public PackedAssets[] PackedAssets => report.packedAssets;
        }
    }
}
