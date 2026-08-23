namespace BuildAssistant.Editor
{
    /// <summary>Identifies the terminal state recorded for a Build Assistant run.</summary>
    public enum BuildAssistantHistoryStatus
    {
        /// <summary>The Unity build completed successfully.</summary>
        Succeeded = 0,
        /// <summary>The Unity build failed, was cancelled, or produced no readable report.</summary>
        Failed = 1,
        /// <summary>A durable running record remained after a reload or process interruption.</summary>
        Interrupted = 2
    }
}

