namespace ReferenceFinder
{
    /// <summary>
    /// Describes one GUID-preserving asset rename prepared by a preview.
    /// </summary>
    public sealed class AssetRenameEntry
    {
        internal AssetRenameEntry(string guid, string originalPath, string newPath)
        {
            Guid = guid;
            OriginalPath = originalPath;
            NewPath = newPath;
        }

        /// <summary>Gets the stable asset GUID verified during preview.</summary>
        public string Guid { get; }

        /// <summary>Gets the original AssetDatabase path.</summary>
        public string OriginalPath { get; }

        /// <summary>Gets the proposed AssetDatabase path.</summary>
        public string NewPath { get; }
    }
}
