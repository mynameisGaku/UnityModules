using System.Collections.Generic;

namespace SceneWorkspace.Editor
{
    /// <summary>Separates Unity scene mutation from deterministic planning and recovery tests.</summary>
    internal interface ISceneWorkspaceGateway
    {
        SceneWorkspaceSnapshot CaptureCurrentSetup();
        SceneWorkspaceProfileSnapshot CaptureProfile(SceneWorkspaceProfile profile);
        void RestoreSetup(IReadOnlyList<SceneWorkspaceSceneState> scenes);
    }
}
