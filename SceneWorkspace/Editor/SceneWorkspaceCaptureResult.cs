using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneWorkspace.Editor
{
    /// <summary>Returns a detached current scene setup or its bounded capture failure.</summary>
    public sealed class SceneWorkspaceCaptureResult
    {
        internal SceneWorkspaceCaptureResult(SceneWorkspaceError error, string message, string fingerprint, IEnumerable<SceneWorkspaceSceneState> scenes)
        {
            Error = error;
            Message = message ?? string.Empty;
            Fingerprint = fingerprint ?? string.Empty;
            Scenes = Array.AsReadOnly((scenes ?? Enumerable.Empty<SceneWorkspaceSceneState>()).ToArray());
        }

        public SceneWorkspaceError Error { get; }
        public string Message { get; }
        public string Fingerprint { get; }
        public IReadOnlyList<SceneWorkspaceSceneState> Scenes { get; }
        public bool Succeeded => Error == SceneWorkspaceError.None;
    }
}
