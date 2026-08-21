namespace ReferenceFinder
{
    /// <summary>
    /// Selects whether a search matches immediate or transitive asset dependencies.
    /// </summary>
    public enum AssetReferenceSearchMode
    {
        /// <summary>Matches assets that immediately depend on the target asset.</summary>
        Direct = 0,

        /// <summary>Matches assets that depend on the target through any dependency depth.</summary>
        Recursive = 1
    }
}
