using System;
using UnityEditor;

namespace SceneWorkspace.Editor
{
    /// <summary>Performs only the explicit profile capture requested from the editor window.</summary>
    internal static class SceneWorkspaceProfileWriter
    {
        internal static SceneWorkspaceValidation ReplaceFromCapture(SceneWorkspaceProfile profile, SceneWorkspaceCaptureResult capture)
        {
            if (profile == null)
                return new SceneWorkspaceValidation(SceneWorkspaceError.InvalidProfile, "Select a workspace profile before capturing.");
            var profilePath = AssetDatabase.GetAssetPath(profile) ?? string.Empty;
            if (string.IsNullOrEmpty(profilePath) || !profilePath.StartsWith("Assets/", StringComparison.Ordinal))
                return new SceneWorkspaceValidation(SceneWorkspaceError.ProfileNotSaved, "Save the workspace profile under Assets before capturing.");
            if (capture == null || !capture.Succeeded)
                return new SceneWorkspaceValidation(capture?.Error ?? SceneWorkspaceError.CaptureFailed, capture?.Message ?? "The current setup could not be captured.");

            var entries = new SceneWorkspaceProfileEntry[capture.Scenes.Count];
            for (var index = 0; index < capture.Scenes.Count; index++)
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(capture.Scenes[index].Path);
                if (scene == null)
                    return new SceneWorkspaceValidation(SceneWorkspaceError.MissingScene, "A captured scene asset is missing.");
                entries[index] = new SceneWorkspaceProfileEntry(scene, capture.Scenes[index].Loaded, capture.Scenes[index].Active);
            }

            profile.ReplaceEntries(entries);
            EditorUtility.SetDirty(profile);
            return SceneWorkspaceValidation.Success;
        }
    }
}
