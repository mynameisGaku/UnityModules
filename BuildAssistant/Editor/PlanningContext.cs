using System;

namespace BuildAssistant.Editor
{
    internal enum OutputRootMode
    {
        ExistingDirectory = 0,
        MissingChild = 1
    }

    internal sealed class PlanningContext
    {
        internal PlanningContext(EnvironmentSnapshot environment, string outputRoot, OutputRootMode outputRootMode, DateTime createdAtUtc, string entropy, bool runPathBusy, BuildAssistantHistoryEntry previousComparableSuccess)
        {
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            OutputRoot = outputRoot ?? string.Empty;
            OutputRootMode = outputRootMode;
            CreatedAtUtc = createdAtUtc.Kind == DateTimeKind.Utc ? createdAtUtc : createdAtUtc.ToUniversalTime();
            Entropy = entropy ?? string.Empty;
            RunPathBusy = runPathBusy;
            PreviousComparableSuccess = previousComparableSuccess;
        }

        internal EnvironmentSnapshot Environment { get; }
        internal string OutputRoot { get; }
        internal OutputRootMode OutputRootMode { get; }
        internal DateTime CreatedAtUtc { get; }
        internal string Entropy { get; }
        internal bool RunPathBusy { get; }
        internal BuildAssistantHistoryEntry PreviousComparableSuccess { get; }
    }
}
