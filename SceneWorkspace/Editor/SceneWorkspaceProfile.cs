using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneWorkspace.Editor
{
    /// <summary>Stores an ordered editor-only scene setup selected explicitly by the user.</summary>
    [CreateAssetMenu(fileName = "SceneWorkspaceProfile", menuName = "Scene Workspace/Profile")]
    public sealed class SceneWorkspaceProfile : ScriptableObject
    {
        [SerializeField] private SceneWorkspaceProfileEntry[] entries = Array.Empty<SceneWorkspaceProfileEntry>();

        /// <summary>Returns detached entry values so callers cannot mutate the serialized array indirectly.</summary>
        public IReadOnlyList<SceneWorkspaceProfileEntry> Entries
        {
            get
            {
                var copy = new SceneWorkspaceProfileEntry[entries?.Length ?? 0];
                for (var index = 0; index < copy.Length; index++)
                    copy[index] = entries[index]?.Clone();
                return Array.AsReadOnly(copy);
            }
        }

        internal void ReplaceEntries(SceneWorkspaceProfileEntry[] value)
        {
            var source = value ?? Array.Empty<SceneWorkspaceProfileEntry>();
            entries = new SceneWorkspaceProfileEntry[source.Length];
            for (var index = 0; index < source.Length; index++)
                entries[index] = source[index]?.Clone();
        }
    }
}
