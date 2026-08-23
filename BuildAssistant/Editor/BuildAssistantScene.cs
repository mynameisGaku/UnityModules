using System;

namespace BuildAssistant.Editor
{
    /// <summary>Captures one ordered build-scene input without retaining a Unity object.</summary>
    public sealed class BuildAssistantScene
    {
        /// <summary>Creates an immutable scene snapshot.</summary>
        /// <param name="order">The zero-based order in the effective profile scene list.</param>
        /// <param name="guid">The Unity asset GUID, or an empty string when the path cannot be resolved.</param>
        /// <param name="assetPath">The project-relative scene asset path.</param>
        /// <param name="enabled">Whether the scene is included in the player build.</param>
        /// <param name="dependencyHash">The Unity dependency hash captured during preview.</param>
        public BuildAssistantScene(int order, string guid, string assetPath, bool enabled, string dependencyHash)
        {
            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order));

            Order = order;
            Guid = guid ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            Enabled = enabled;
            DependencyHash = dependencyHash ?? string.Empty;
        }

        /// <summary>Gets the zero-based order in the effective profile scene list.</summary>
        public int Order { get; }

        /// <summary>Gets the Unity asset GUID.</summary>
        public string Guid { get; }

        /// <summary>Gets the project-relative scene asset path.</summary>
        public string AssetPath { get; }

        /// <summary>Gets whether the scene is included in the player build.</summary>
        public bool Enabled { get; }

        /// <summary>Gets the dependency hash captured during preview.</summary>
        public string DependencyHash { get; }
    }
}
