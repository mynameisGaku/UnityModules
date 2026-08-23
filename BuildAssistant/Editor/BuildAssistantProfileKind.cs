namespace BuildAssistant.Editor
{
    /// <summary>Identifies whether Unity is using its platform profile or an explicit custom BuildProfile asset.</summary>
    public enum BuildAssistantProfileKind
    {
        /// <summary>The platform profile is active because no custom profile is active.</summary>
        Platform = 0,
        /// <summary>An explicit custom BuildProfile asset is active.</summary>
        Custom = 1
    }
}
