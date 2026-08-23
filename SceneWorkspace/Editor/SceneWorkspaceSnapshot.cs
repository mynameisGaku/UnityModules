using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneWorkspace.Editor
{
    /// <summary>Captures the editor guards and ordered scene setup needed by pure validation.</summary>
    internal sealed class SceneWorkspaceSnapshot
    {
        internal SceneWorkspaceSnapshot(bool playModeActive, bool compiling, bool updating, bool prefabStageOpen, IEnumerable<SceneWorkspaceSceneState> scenes)
        {
            PlayModeActive = playModeActive;
            Compiling = compiling;
            Updating = updating;
            PrefabStageOpen = prefabStageOpen;
            Scenes = Array.AsReadOnly((scenes ?? Enumerable.Empty<SceneWorkspaceSceneState>()).ToArray());
        }

        internal bool PlayModeActive { get; }
        internal bool Compiling { get; }
        internal bool Updating { get; }
        internal bool PrefabStageOpen { get; }
        internal IReadOnlyList<SceneWorkspaceSceneState> Scenes { get; }
    }
}
