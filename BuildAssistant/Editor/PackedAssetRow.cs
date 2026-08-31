namespace BuildAssistant.Editor
{
    internal sealed class PackedAssetRow
    {
        internal PackedAssetRow(string assetKey, string typeName, ulong packedBytes)
        {
            AssetKey = string.IsNullOrEmpty(assetKey) ? "[生成物]" : assetKey;
            TypeName = string.IsNullOrEmpty(typeName) ? "[不明]" : typeName;
            PackedBytes = packedBytes;
        }

        internal string AssetKey { get; }
        internal string TypeName { get; }
        internal ulong PackedBytes { get; }
    }
}
