using System;
using UnityEditor;
using UnityEngine;

namespace SceneWorkspace.Editor
{
    /// <summary>Stores one scene reference and its intended loaded and active state in a workspace profile.</summary>
    [Serializable]
    public sealed class SceneWorkspaceProfileEntry
    {
        [SerializeField] private SceneAsset scene;
        [SerializeField] private bool loaded = true;
        [SerializeField] private bool active;

        public SceneAsset Scene => scene;
        public bool Loaded => loaded;
        public bool Active => active;

        internal SceneWorkspaceProfileEntry(SceneAsset scene, bool loaded, bool active)
        {
            this.scene = scene;
            this.loaded = loaded;
            this.active = active;
        }

        internal SceneWorkspaceProfileEntry Clone()
        {
            return new SceneWorkspaceProfileEntry(scene, loaded, active);
        }
    }
}
