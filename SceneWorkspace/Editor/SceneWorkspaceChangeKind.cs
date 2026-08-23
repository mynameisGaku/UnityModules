namespace SceneWorkspace.Editor
{
    /// <summary>Describes one deterministic difference between the current and target scene setup.</summary>
    public enum SceneWorkspaceChangeKind
    {
        Keep,
        Open,
        Close,
        Load,
        Unload,
        Reorder,
        SetActive
    }
}
