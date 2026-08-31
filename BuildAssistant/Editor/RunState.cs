using System;
using System.Linq;

namespace BuildAssistant.Editor
{
    internal sealed class RunState
    {
        internal RunState(bool completed, BuildAssistantHistoryEntry entry)
        {
            Completed = completed;
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }

        internal bool Completed { get; }
        internal BuildAssistantHistoryEntry Entry { get; }

        internal static RunState CreateRunning(BuildAssistantPlan plan, DateTime startedAtUtc)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            var recordedStart = startedAtUtc < plan.CreatedAtUtc ? plan.CreatedAtUtc : startedAtUtc;
            var previous = plan.PreviousComparableSuccess;
            var entry = new BuildAssistantHistoryEntry(plan.RunId, plan.CreatedAtUtc, recordedStart, recordedStart, BuildAssistantHistoryStatus.Interrupted, BuildAssistantError.BuildInvocationFailed, "ビルドの終了報告を記録できませんでした。", plan.OutputRoot, plan.RunDirectory, plan.ArtifactPath, plan.ProfileKind, plan.ProfileGuid, plan.ProfileName, plan.ProfilePath, plan.ProfileDependencyHash, plan.ProfileStableId, plan.Target, plan.TargetGroup, plan.NamedBuildTarget, plan.Subtarget, plan.ScriptingBackend, plan.Options, plan.EffectiveDefines, plan.Scenes, 0, 0, 0, 0, 0, Array.Empty<BuildAssistantAssetSize>(), Array.Empty<BuildAssistantTypeSize>(), previous?.RunId ?? string.Empty, 0, 0);
            return new RunState(false, entry);
        }

        internal RunState AsInterrupted(DateTime completedAtUtc)
        {
            var source = Entry;
            var terminalTime = completedAtUtc < source.StartedAtUtc ? source.StartedAtUtc : completedAtUtc;
            var entry = new BuildAssistantHistoryEntry(source.RunId, source.CreatedAtUtc, source.StartedAtUtc, terminalTime, BuildAssistantHistoryStatus.Interrupted, BuildAssistantError.BuildInvocationFailed, "前回のビルドは中断されました。自動では再実行していません。", source.OutputRoot, source.RunDirectory, source.ArtifactPath, source.ProfileKind, source.ProfileGuid, source.ProfileName, source.ProfilePath, source.ProfileDependencyHash, source.ProfileStableId, source.Target, source.TargetGroup, source.NamedBuildTarget, source.Subtarget, source.ScriptingBackend, source.Options, source.EffectiveDefines, source.Scenes, source.TotalErrors, source.TotalWarnings, source.TotalOutputBytes, source.PackedContentBytes, source.PackedOverheadBytes, source.Assets, source.Types, source.PreviousRunId, source.TotalOutputDeltaBytes, source.PackedContentDeltaBytes);
            return new RunState(true, entry);
        }
    }
}
