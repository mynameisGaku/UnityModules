using System;
using SceneWorkspace.Editor;
using UnityEngine;

namespace SceneWorkspace.Tests
{
    internal static class SceneWorkspaceTestData
    {
        internal static SceneWorkspaceSceneState Scene(string id, int index, bool loaded = true, bool active = false, bool dirty = false, bool exists = true, string path = null)
        {
            var scenePath = path ?? "Assets/Scenes/" + id + ".unity";
            return new SceneWorkspaceSceneState(index, exists ? "guid-" + id : string.Empty, scenePath, exists, loaded, active, dirty);
        }

        internal static SceneWorkspaceSnapshot Current(params SceneWorkspaceSceneState[] scenes)
        {
            return new SceneWorkspaceSnapshot(false, false, false, false, scenes);
        }

        internal static SceneWorkspaceProfileSnapshot Profile(params SceneWorkspaceSceneState[] scenes)
        {
            return new SceneWorkspaceProfileSnapshot(true, "profile-guid", "Assets/Profiles/Workspace.asset", "Workspace", scenes);
        }

        internal static SceneWorkspaceProfile CreateProfileAsset()
        {
            return ScriptableObject.CreateInstance<SceneWorkspaceProfile>();
        }

        internal static SceneWorkspaceSnapshot WithFlags(bool play = false, bool compiling = false, bool updating = false, bool prefab = false)
        {
            return new SceneWorkspaceSnapshot(play, compiling, updating, prefab, new[] { Scene("Main", 0, true, true) });
        }

        internal static SceneWorkspacePlan ClonePlan(SceneWorkspacePlan plan)
        {
            return new SceneWorkspacePlan(plan.Error, plan.Message, plan.Generation, plan.ProfileGuid, plan.ProfilePath, plan.ProfileName, plan.ProfileRevision, plan.CurrentFingerprint, plan.CurrentScenes, plan.TargetScenes, plan.Changes);
        }
    }
}
