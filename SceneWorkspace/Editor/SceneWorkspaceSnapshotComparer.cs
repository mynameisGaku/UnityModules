using System;
using System.Collections.Generic;

namespace SceneWorkspace.Editor
{
    /// <summary>Verifies exact order, identity, load state, active state, and clean state after restore.</summary>
    internal static class SceneWorkspaceSnapshotComparer
    {
        internal static bool Matches(IReadOnlyList<SceneWorkspaceSceneState> expected, IReadOnlyList<SceneWorkspaceSceneState> actual, out string difference)
        {
            if (expected == null || actual == null)
            {
                difference = "A scene setup is unavailable.";
                return false;
            }
            if (expected.Count != actual.Count)
            {
                difference = "The scene count does not match the confirmed setup.";
                return false;
            }

            for (var index = 0; index < expected.Count; index++)
            {
                var wanted = expected[index];
                var found = actual[index];
                if (wanted == null || found == null || !wanted.HasSameSetup(found))
                {
                    difference = "The scene at index " + index + " does not match the confirmed setup.";
                    return false;
                }
                if (found.Dirty)
                {
                    difference = "A scene became dirty while the setup was being restored.";
                    return false;
                }
            }

            difference = string.Empty;
            return true;
        }
    }
}
