using UnityEngine;

namespace PlayModeTuning.Editor
{
    /// <summary>Names one scene Component and one top-level serialized property to capture manually.</summary>
    public sealed class PlayModeTuningPropertySelection
    {
        /// <summary>Creates a selection; Start reports null, unstable, nested, array, and unsupported targets.</summary>
        public PlayModeTuningPropertySelection(Component target, string propertyPath)
        {
            Target = target;
            PropertyPath = propertyPath ?? string.Empty;
        }

        public Component Target { get; }
        public string PropertyPath { get; }
    }
}
