namespace SceneWorkspace.Editor
{
    /// <summary>Returns one pure validation outcome without exceptions or editor mutation.</summary>
    internal readonly struct SceneWorkspaceValidation
    {
        internal SceneWorkspaceValidation(SceneWorkspaceError error, string message)
        {
            Error = error;
            Message = message ?? string.Empty;
        }

        internal SceneWorkspaceError Error { get; }
        internal string Message { get; }
        internal bool Succeeded => Error == SceneWorkspaceError.None;

        internal static SceneWorkspaceValidation Success => new SceneWorkspaceValidation(SceneWorkspaceError.None, string.Empty);
    }
}
