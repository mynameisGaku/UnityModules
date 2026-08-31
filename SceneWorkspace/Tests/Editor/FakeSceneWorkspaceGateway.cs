using System;
using System.Collections.Generic;
using System.Linq;
using SceneWorkspace.Editor;

namespace SceneWorkspace.Editor.Tests
{
    internal sealed class FakeSceneWorkspaceGateway : ISceneWorkspaceGateway
    {
        private readonly Queue<SceneWorkspaceSnapshot> currentCaptures = new Queue<SceneWorkspaceSnapshot>();
        private readonly Queue<SceneWorkspaceProfileSnapshot> profileCaptures = new Queue<SceneWorkspaceProfileSnapshot>();

        internal List<IReadOnlyList<SceneWorkspaceSceneState>> RestoreCalls { get; } = new List<IReadOnlyList<SceneWorkspaceSceneState>>();
        internal Action<int, IReadOnlyList<SceneWorkspaceSceneState>> RestoreHandler { get; set; }
        internal SceneWorkspaceSnapshot DefaultCurrent { get; set; }
        internal SceneWorkspaceProfileSnapshot DefaultProfile { get; set; }
        internal int CurrentCaptureCount { get; private set; }
        internal int ProfileCaptureCount { get; private set; }

        internal void EnqueueCurrent(params SceneWorkspaceSnapshot[] snapshots)
        {
            foreach (var snapshot in snapshots)
                currentCaptures.Enqueue(snapshot);
        }

        internal void EnqueueProfile(params SceneWorkspaceProfileSnapshot[] profiles)
        {
            foreach (var profile in profiles)
                profileCaptures.Enqueue(profile);
        }

        public SceneWorkspaceSnapshot CaptureCurrentSetup()
        {
            CurrentCaptureCount++;
            if (currentCaptures.Count > 0)
                return currentCaptures.Dequeue();
            if (DefaultCurrent != null)
                return DefaultCurrent;
            throw new InvalidOperationException("No current snapshot was configured.");
        }

        public SceneWorkspaceProfileSnapshot CaptureProfile(SceneWorkspaceProfile profile)
        {
            ProfileCaptureCount++;
            if (profileCaptures.Count > 0)
                return profileCaptures.Dequeue();
            if (DefaultProfile != null)
                return DefaultProfile;
            throw new InvalidOperationException("No profile snapshot was configured.");
        }

        public void RestoreSetup(IReadOnlyList<SceneWorkspaceSceneState> scenes)
        {
            var copy = Array.AsReadOnly(scenes.Select((scene, index) => scene.WithIndex(index)).ToArray());
            RestoreCalls.Add(copy);
            RestoreHandler?.Invoke(RestoreCalls.Count, copy);
        }
    }
}
