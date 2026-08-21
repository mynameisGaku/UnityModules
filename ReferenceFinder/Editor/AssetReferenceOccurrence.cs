namespace ReferenceFinder
{
    /// <summary>
    /// Identifies one serialized object-reference property that can be replaced safely.
    /// </summary>
    public sealed class AssetReferenceOccurrence
    {
        internal AssetReferenceOccurrence(
            string assetPath,
            long ownerLocalFileId,
            string ownerName,
            string ownerTypeName,
            string propertyPath)
        {
            AssetPath = assetPath;
            OwnerLocalFileId = ownerLocalFileId;
            OwnerName = ownerName;
            OwnerTypeName = ownerTypeName;
            PropertyPath = propertyPath;
        }

        /// <summary>Gets the AssetDatabase path containing the reference.</summary>
        public string AssetPath { get; }

        /// <summary>Gets the persistent local file identifier of the serialized owner.</summary>
        public long OwnerLocalFileId { get; }

        /// <summary>Gets the serialized owner's current name.</summary>
        public string OwnerName { get; }

        /// <summary>Gets the serialized owner's concrete type name.</summary>
        public string OwnerTypeName { get; }

        /// <summary>Gets the exact SerializedProperty path that contains the reference.</summary>
        public string PropertyPath { get; }
    }
}
