using System;

namespace SceneWorkspace.Editor
{
    /// <summary>Provides one detached scene row captured from an editor setup or profile.</summary>
    public sealed class SceneWorkspaceSceneState
    {
        internal SceneWorkspaceSceneState(int index, string guid, string path, bool exists, bool loaded, bool active, bool dirty)
        {
            Index = index;
            Guid = guid ?? string.Empty;
            Path = path ?? string.Empty;
            Exists = exists;
            Loaded = loaded;
            Active = active;
            Dirty = dirty;
        }

        public int Index { get; }
        public string Guid { get; }
        public string Path { get; }
        public bool Exists { get; }
        public bool Loaded { get; }
        public bool Active { get; }
        public bool Dirty { get; }

        internal SceneWorkspaceSceneState WithIndex(int index)
        {
            return new SceneWorkspaceSceneState(index, Guid, Path, Exists, Loaded, Active, Dirty);
        }

        internal bool HasSameSetup(SceneWorkspaceSceneState other)
        {
            return other != null
                && StringComparer.Ordinal.Equals(Guid, other.Guid)
                && StringComparer.Ordinal.Equals(Path, other.Path)
                && Loaded == other.Loaded
                && Active == other.Active;
        }
    }
}
