using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildAssistant.Editor
{
    internal sealed class SizeAggregation
    {
        internal SizeAggregation(ulong packedContentBytes, ulong packedOverheadBytes, IReadOnlyList<BuildAssistantAssetSize> assets, IReadOnlyList<BuildAssistantTypeSize> types)
        {
            PackedContentBytes = packedContentBytes;
            PackedOverheadBytes = packedOverheadBytes;
            Assets = assets;
            Types = types;
        }

        internal ulong PackedContentBytes { get; }
        internal ulong PackedOverheadBytes { get; }
        internal IReadOnlyList<BuildAssistantAssetSize> Assets { get; }
        internal IReadOnlyList<BuildAssistantTypeSize> Types { get; }
    }

    internal static class SizeAggregator
    {
        internal static SizeAggregation Aggregate(IEnumerable<PackedAssetRow> rows, IEnumerable<ulong> packedOverheads)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));
            if (packedOverheads == null)
                throw new ArgumentNullException(nameof(packedOverheads));

            var assets = new Dictionary<string, MutableAssetSize>(StringComparer.Ordinal);
            var types = new Dictionary<string, MutableTypeSize>(StringComparer.Ordinal);
            ulong packedContentBytes = 0;
            foreach (var row in rows)
            {
                if (row == null)
                    continue;

                checked
                {
                    packedContentBytes += row.PackedBytes;
                    if (!assets.TryGetValue(row.AssetKey, out var asset))
                    {
                        asset = new MutableAssetSize();
                        assets.Add(row.AssetKey, asset);
                    }

                    asset.Bytes += row.PackedBytes;
                    asset.Occurrences++;
                    if (!types.TryGetValue(row.TypeName, out var type))
                    {
                        type = new MutableTypeSize();
                        types.Add(row.TypeName, type);
                    }

                    type.Bytes += row.PackedBytes;
                    type.Occurrences++;
                    type.Assets.Add(row.AssetKey);
                }
            }

            ulong packedOverheadBytes = 0;
            checked
            {
                foreach (var overhead in packedOverheads)
                    packedOverheadBytes += overhead;
            }

            var assetRows = assets.Select(pair => new BuildAssistantAssetSize(pair.Key, pair.Value.Bytes, pair.Value.Occurrences)).OrderByDescending(row => row.PackedBytes).ThenBy(row => row.AssetPath, StringComparer.Ordinal).ToArray();
            var typeRows = types.Select(pair => new BuildAssistantTypeSize(pair.Key, pair.Value.Bytes, pair.Value.Occurrences, pair.Value.Assets.Count)).OrderByDescending(row => row.PackedBytes).ThenBy(row => row.TypeName, StringComparer.Ordinal).ToArray();
            return new SizeAggregation(packedContentBytes, packedOverheadBytes, assetRows, typeRows);
        }

        private sealed class MutableAssetSize
        {
            internal ulong Bytes;
            internal int Occurrences;
        }

        private sealed class MutableTypeSize
        {
            internal readonly HashSet<string> Assets = new HashSet<string>(StringComparer.Ordinal);
            internal ulong Bytes;
            internal int Occurrences;
        }
    }
}
