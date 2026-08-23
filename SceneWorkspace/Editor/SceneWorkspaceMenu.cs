using UnityEditor;

namespace SceneWorkspace.Editor
{
    /// <summary>Adds one intuitive Tools menu entry for the Scene Workspace window.</summary>
    internal static class SceneWorkspaceMenu
    {
        [MenuItem("Tools/Scene Workspace/Open")]
        private static void Open()
        {
            SceneWorkspaceWindow.Open();
        }
    }
}
