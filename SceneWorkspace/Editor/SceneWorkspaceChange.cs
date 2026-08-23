namespace SceneWorkspace.Editor
{
    /// <summary>Provides one immutable preview row and its before and after positions or states.</summary>
    public sealed class SceneWorkspaceChange
    {
        internal SceneWorkspaceChange(SceneWorkspaceChangeKind kind, string path, int beforeIndex, int afterIndex, bool beforeLoaded, bool afterLoaded, bool beforeActive, bool afterActive)
        {
            Kind = kind;
            Path = path ?? string.Empty;
            BeforeIndex = beforeIndex;
            AfterIndex = afterIndex;
            BeforeLoaded = beforeLoaded;
            AfterLoaded = afterLoaded;
            BeforeActive = beforeActive;
            AfterActive = afterActive;
        }

        public SceneWorkspaceChangeKind Kind { get; }
        public string Path { get; }
        public int BeforeIndex { get; }
        public int AfterIndex { get; }
        public bool BeforeLoaded { get; }
        public bool AfterLoaded { get; }
        public bool BeforeActive { get; }
        public bool AfterActive { get; }
    }
}
