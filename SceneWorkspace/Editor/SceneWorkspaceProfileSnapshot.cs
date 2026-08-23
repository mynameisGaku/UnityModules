using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneWorkspace.Editor
{
    /// <summary>Captures a profile identity and ordered target setup without retaining serialized entry objects.</summary>
    internal sealed class SceneWorkspaceProfileSnapshot
    {
        internal SceneWorkspaceProfileSnapshot(bool exists, string guid, string path, string name, IEnumerable<SceneWorkspaceSceneState> scenes)
        {
            Exists = exists;
            Guid = guid ?? string.Empty;
            Path = path ?? string.Empty;
            Name = name ?? string.Empty;
            Scenes = Array.AsReadOnly((scenes ?? Enumerable.Empty<SceneWorkspaceSceneState>()).ToArray());
        }

        internal bool Exists { get; }
        internal string Guid { get; }
        internal string Path { get; }
        internal string Name { get; }
        internal IReadOnlyList<SceneWorkspaceSceneState> Scenes { get; }
    }
}
