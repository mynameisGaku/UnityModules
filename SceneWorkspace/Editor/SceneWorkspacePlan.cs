using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneWorkspace.Editor
{
    /// <summary>Captures one immutable, single-use preview of a workspace switch.</summary>
    public sealed class SceneWorkspacePlan
    {
        internal SceneWorkspacePlan(SceneWorkspaceError error, string message, long generation, string profileGuid, string profilePath, string profileName, string profileRevision, string currentFingerprint, IEnumerable<SceneWorkspaceSceneState> currentScenes, IEnumerable<SceneWorkspaceSceneState> targetScenes, IEnumerable<SceneWorkspaceChange> changes)
        {
            Error = error;
            Message = message ?? string.Empty;
            Generation = generation;
            ProfileGuid = profileGuid ?? string.Empty;
            ProfilePath = profilePath ?? string.Empty;
            ProfileName = profileName ?? string.Empty;
            ProfileRevision = profileRevision ?? string.Empty;
            CurrentFingerprint = currentFingerprint ?? string.Empty;
            CurrentScenes = Array.AsReadOnly((currentScenes ?? Enumerable.Empty<SceneWorkspaceSceneState>()).ToArray());
            TargetScenes = Array.AsReadOnly((targetScenes ?? Enumerable.Empty<SceneWorkspaceSceneState>()).ToArray());
            Changes = Array.AsReadOnly((changes ?? Enumerable.Empty<SceneWorkspaceChange>()).ToArray());
        }

        public SceneWorkspaceError Error { get; }
        public string Message { get; }
        public long Generation { get; }
        public string ProfileGuid { get; }
        public string ProfilePath { get; }
        public string ProfileName { get; }
        public string ProfileRevision { get; }
        public string CurrentFingerprint { get; }
        public IReadOnlyList<SceneWorkspaceSceneState> CurrentScenes { get; }
        public IReadOnlyList<SceneWorkspaceSceneState> TargetScenes { get; }
        public IReadOnlyList<SceneWorkspaceChange> Changes { get; }
        public bool IsReady => Error == SceneWorkspaceError.None;
        public bool HasChanges => Changes.Any(change => change.Kind != SceneWorkspaceChangeKind.Keep);
    }
}
