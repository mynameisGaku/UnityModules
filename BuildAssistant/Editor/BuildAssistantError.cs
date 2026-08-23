namespace BuildAssistant.Editor
{
    /// <summary>Identifies a bounded failure reported by Build Assistant.</summary>
    public enum BuildAssistantError
    {
        /// <summary>No error occurred.</summary>
        None = 0,
        /// <summary>The output root was empty, relative, a file, or more than one missing directory deep.</summary>
        InvalidOutputRoot = 1,
        /// <summary>The output root escaped its boundary, used a reparse point, or overlapped a Unity-managed directory.</summary>
        UnsafeOutputPath = 2,
        /// <summary>The selected build target or build options are outside the supported desktop standalone contract.</summary>
        UnsupportedBuildTarget = 3,
        /// <summary>The editor is compiling, updating, entering play mode, or otherwise unable to start a build.</summary>
        EditorBusy = 4,
        /// <summary>The effective build profile contains no enabled scenes.</summary>
        NoEnabledScenes = 5,
        /// <summary>The build inputs changed after the plan was created.</summary>
        StalePlan = 6,
        /// <summary>Build Assistant or Unity is already building a player.</summary>
        BuildAlreadyRunning = 7,
        /// <summary>The planned run directory or reservation already exists.</summary>
        OutputAlreadyExists = 8,
        /// <summary>The output directory or durable run state could not be reserved.</summary>
        OutputReservationFailed = 9,
        /// <summary>Unity failed or refused to invoke the planned player build.</summary>
        BuildInvocationFailed = 10,
        /// <summary>Unity returned no BuildReport for the invoked build.</summary>
        BuildReportUnavailable = 11,
        /// <summary>The returned BuildReport could not be reduced to detached data.</summary>
        ReportReadFailed = 12,
        /// <summary>The build history or explicit JSON export could not be written durably.</summary>
        HistoryWriteFailed = 13
    }
}
