namespace BuildAssistant.Editor
{
    internal sealed class ProfileSnapshot
    {
        internal ProfileSnapshot(BuildAssistantProfileKind kind, string guid, string name, string assetPath, string dependencyHash, string stableId)
        {
            Kind = kind;
            Guid = guid ?? string.Empty;
            Name = name ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            DependencyHash = dependencyHash ?? string.Empty;
            StableId = stableId ?? string.Empty;
        }

        internal BuildAssistantProfileKind Kind { get; }
        internal string Guid { get; }
        internal string Name { get; }
        internal string AssetPath { get; }
        internal string DependencyHash { get; }
        internal string StableId { get; }
    }
}

