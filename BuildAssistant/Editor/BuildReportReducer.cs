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
                return CreateFailure(plan, fallbackStartedAtUtc, fallbackCompletedAtUtc, BuildAssistantError.BuildReportUnavailable, "Unityからビルド報告が返されませんでした。");
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
            catch (Exception)
            {
                return CreateFailure(plan, fallbackStartedAtUtc, fallbackCompletedAtUtc, BuildAssistantError.ReportReadFailed, "ビルド報告の終了状態を読み取れませんでした。");
            }

            var succeeded = buildResult == BuildResult.Succeeded;
            var buildError = succeeded ? BuildAssistantError.None : BuildAssistantError.BuildInvocationFailed;
            var buildMessage = succeeded ? string.Empty : FormatBuildFailure(buildResult);

            var startedAtUtc = NormalizeUtc(fallbackStartedAtUtc);
            var completedAtUtc = NormalizeUtc(fallbackCompletedAtUtc);
            int totalErrors;
            int totalWarnings;
            ulong totalOutputBytes;
            try
            {
                totalErrors = report.TotalErrors;
                totalWarnings = report.TotalWarnings;
                totalOutputBytes = report.TotalSize;
            }
            catch (Exception)
            {
                var summaryError = succeeded ? BuildAssistantError.ReportReadFailed : buildError;
                var summaryMessage = succeeded ? "プレイヤービルドは成功しましたが、概要の集計情報を読み取れませんでした。" : buildMessage + " 概要の集計情報も読み取れませんでした。";
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
                        var assetKey = !string.IsNullOrEmpty(content.sourceAssetPath) ? content.sourceAssetPath : !IsEmptyGuid(guid) ? "guid:" + guid : $"生成物:{packedIndex:D4}:{contentIndex:D6}:{content.id}";
                        var typeName = content.type?.AssemblyQualifiedName ?? "[不明]";
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
            catch (Exception)
            {
                var analyticsError = succeeded ? BuildAssistantError.ReportReadFailed : buildError;
                var analyticsMessage = succeeded ? "プレイヤービルドは成功しましたが、格納内容の集計情報をまとめられませんでした。" : buildMessage + " 格納内容の集計情報もまとめられませんでした。";
                var emptyAggregation = SizeAggregator.Aggregate(Array.Empty<PackedAssetRow>(), Array.Empty<ulong>());
                var entry = CreateEntry(plan, startedAtUtc, completedAtUtc, succeeded ? BuildAssistantHistoryStatus.Succeeded : BuildAssistantHistoryStatus.Failed, analyticsError, analyticsMessage, totalErrors, totalWarnings, totalOutputBytes, emptyAggregation, plan.PreviousComparableSuccess?.RunId ?? string.Empty, 0, 0);
                return new BuildReportReduction(succeeded, analyticsError, analyticsMessage, entry);
            }
        }

        private static string FormatBuildFailure(BuildResult result)
        {
            switch (result)
            {
                case BuildResult.Cancelled:
                    return "Unityがプレイヤービルドの中断を報告しました。";
                case BuildResult.Failed:
                    return "Unityがプレイヤービルドの失敗を報告しました。";
                default:
                    return "Unityがプレイヤービルドを正常終了として報告しませんでした。";
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
            var createdTime = NormalizeUtc(plan.CreatedAtUtc);
            var startedTime = NormalizeUtc(startedAtUtc);
            if (startedTime < createdTime)
                startedTime = createdTime;
            var completedTime = NormalizeUtc(completedAtUtc);
            var terminalTime = completedTime < startedTime ? startedTime : completedTime;
            return new BuildAssistantHistoryEntry(plan.RunId, createdTime, startedTime, terminalTime, status, error, message, plan.OutputRoot, plan.RunDirectory, plan.ArtifactPath, plan.ProfileKind, plan.ProfileGuid, plan.ProfileName, plan.ProfilePath, plan.ProfileDependencyHash, plan.ProfileStableId, plan.Target, plan.TargetGroup, plan.NamedBuildTarget, plan.Subtarget, plan.ScriptingBackend, plan.Options, plan.EffectiveDefines, plan.Scenes, totalErrors, totalWarnings, totalOutputBytes, aggregation.PackedContentBytes, aggregation.PackedOverheadBytes, aggregation.Assets, aggregation.Types, previousRunId, outputDelta, packedDelta);
        }

        /// <summary>協定世界時として渡された内部時刻を、種類情報を保った値へ正規化します。</summary>
        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
                return value;
            if (value.Kind == DateTimeKind.Local)
                return value.ToUniversalTime();
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

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
