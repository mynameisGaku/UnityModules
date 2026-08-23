using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneWorkspace.Editor
{
    /// <summary>Builds one deterministic scene-switch plan from validated immutable snapshots.</summary>
    internal static class SceneWorkspacePlanner
    {
        internal static SceneWorkspacePlan Create(SceneWorkspaceSnapshot current, SceneWorkspaceProfileSnapshot profile, long generation)
        {
            var currentValidation = SceneWorkspaceValidator.ValidateCurrent(current);
            if (!currentValidation.Succeeded)
                return Failure(currentValidation, current, profile);

            var profileValidation = SceneWorkspaceValidator.ValidateProfile(profile);
            if (!profileValidation.Succeeded)
                return Failure(profileValidation, current, profile);

            var currentScenes = current.Scenes.Select((scene, index) => scene.WithIndex(index)).ToArray();
            var targetScenes = profile.Scenes.Select((scene, index) => scene.WithIndex(index)).ToArray();
            var changes = CreateChanges(currentScenes, targetScenes);
            return new SceneWorkspacePlan(
                SceneWorkspaceError.None,
                string.Empty,
                generation,
                profile.Guid,
                profile.Path,
                profile.Name,
                SceneWorkspaceFingerprint.ComputeProfile(profile),
                SceneWorkspaceFingerprint.ComputeCurrent(currentScenes),
                currentScenes,
                targetScenes,
                changes);
        }

        internal static SceneWorkspacePlan Failure(SceneWorkspaceError error, string message)
        {
            return new SceneWorkspacePlan(error, message, 0L, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Array.Empty<SceneWorkspaceSceneState>(), Array.Empty<SceneWorkspaceSceneState>(), Array.Empty<SceneWorkspaceChange>());
        }

        private static SceneWorkspacePlan Failure(SceneWorkspaceValidation validation, SceneWorkspaceSnapshot current, SceneWorkspaceProfileSnapshot profile)
        {
            return new SceneWorkspacePlan(
                validation.Error,
                validation.Message,
                0L,
                profile?.Guid,
                profile?.Path,
                profile?.Name,
                profile == null ? string.Empty : SceneWorkspaceFingerprint.ComputeProfile(profile),
                current == null ? string.Empty : SceneWorkspaceFingerprint.ComputeCurrent(current.Scenes),
                current?.Scenes,
                profile?.Scenes,
                Array.Empty<SceneWorkspaceChange>());
        }

        private static SceneWorkspaceChange[] CreateChanges(IReadOnlyList<SceneWorkspaceSceneState> current, IReadOnlyList<SceneWorkspaceSceneState> target)
        {
            var changes = new List<SceneWorkspaceChange>();
            var currentByGuid = current.ToDictionary(scene => scene.Guid, StringComparer.Ordinal);
            var targetByGuid = target.ToDictionary(scene => scene.Guid, StringComparer.Ordinal);

            foreach (var scene in current)
            {
                if (!targetByGuid.ContainsKey(scene.Guid))
                    changes.Add(new SceneWorkspaceChange(SceneWorkspaceChangeKind.Close, scene.Path, scene.Index, -1, scene.Loaded, false, scene.Active, false));
            }

            foreach (var targetScene in target)
            {
                if (!currentByGuid.TryGetValue(targetScene.Guid, out var currentScene))
                {
                    changes.Add(new SceneWorkspaceChange(SceneWorkspaceChangeKind.Open, targetScene.Path, -1, targetScene.Index, false, targetScene.Loaded, false, targetScene.Active));
                    if (targetScene.Active)
                        changes.Add(new SceneWorkspaceChange(SceneWorkspaceChangeKind.SetActive, targetScene.Path, -1, targetScene.Index, false, targetScene.Loaded, false, true));
                    continue;
                }

                var changed = false;
                if (currentScene.Loaded != targetScene.Loaded)
                {
                    var kind = targetScene.Loaded ? SceneWorkspaceChangeKind.Load : SceneWorkspaceChangeKind.Unload;
                    changes.Add(new SceneWorkspaceChange(kind, targetScene.Path, currentScene.Index, targetScene.Index, currentScene.Loaded, targetScene.Loaded, currentScene.Active, targetScene.Active));
                    changed = true;
                }
                if (currentScene.Index != targetScene.Index)
                {
                    changes.Add(new SceneWorkspaceChange(SceneWorkspaceChangeKind.Reorder, targetScene.Path, currentScene.Index, targetScene.Index, currentScene.Loaded, targetScene.Loaded, currentScene.Active, targetScene.Active));
                    changed = true;
                }
                if (!currentScene.Active && targetScene.Active)
                {
                    changes.Add(new SceneWorkspaceChange(SceneWorkspaceChangeKind.SetActive, targetScene.Path, currentScene.Index, targetScene.Index, currentScene.Loaded, targetScene.Loaded, false, true));
                    changed = true;
                }
                if (!changed)
                    changes.Add(new SceneWorkspaceChange(SceneWorkspaceChangeKind.Keep, targetScene.Path, currentScene.Index, targetScene.Index, currentScene.Loaded, targetScene.Loaded, currentScene.Active, targetScene.Active));
            }

            return changes.ToArray();
        }
    }
}
