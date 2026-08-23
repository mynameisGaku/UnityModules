namespace BuildAssistant.Editor
{
    internal sealed class PackedAssetRow
    {
        internal PackedAssetRow(string assetKey, string typeName, ulong packedBytes)
        {
            AssetKey = string.IsNullOrEmpty(assetKey) ? "[generated]" : assetKey;
            TypeName = string.IsNullOrEmpty(typeName) ? "[unknown]" : typeName;
            PackedBytes = packedBytes;
        }

        internal string AssetKey { get; }
        internal string TypeName { get; }
        internal ulong PackedBytes { get; }
    }
}
