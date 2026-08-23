namespace BuildAssistant.Editor
{
    /// <summary>Reports build success independently from durable history persistence.</summary>
    public sealed class BuildAssistantBuildResult
    {
        internal BuildAssistantBuildResult(bool buildSucceeded, bool historyPersisted, BuildAssistantError error, string message, BuildAssistantHistoryEntry entry)
        {
            BuildSucceeded = buildSucceeded;
            HistoryPersisted = historyPersisted;
            Error = error;
            Message = message ?? string.Empty;
            Entry = entry;
        }

        /// <summary>Gets whether Unity reported a successful player build.</summary>
        public bool BuildSucceeded { get; }

        /// <summary>Gets whether the terminal entry was written to bounded history.</summary>
        public bool HistoryPersisted { get; }

        /// <summary>Gets the primary bounded error. A successful player build can report analytics or history persistence errors.</summary>
        public BuildAssistantError Error { get; }

        /// <summary>Gets a detached diagnostic suitable for an editor UI.</summary>
        public string Message { get; }

        /// <summary>Gets the detached terminal entry when build invocation began.</summary>
        public BuildAssistantHistoryEntry Entry { get; }
    }
}
