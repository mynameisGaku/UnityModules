using System;

namespace BuildAssistant.Editor
{
    /// <summary>Reports the checked sum of every packed occurrence attributed to one source asset.</summary>
    public sealed class BuildAssistantAssetSize
    {
        /// <summary>Creates an immutable per-asset packed-size row.</summary>
        /// <param name="assetPath">The source asset path or a stable generated key.</param>
        /// <param name="packedBytes">The checked sum of packed occurrence bytes.</param>
        /// <param name="occurrenceCount">The number of packed occurrences included in the sum.</param>
        public BuildAssistantAssetSize(string assetPath, ulong packedBytes, int occurrenceCount)
        {
            if (string.IsNullOrEmpty(assetPath))
                throw new ArgumentException("An asset path or stable key is required.", nameof(assetPath));
            if (occurrenceCount < 0)
                throw new ArgumentOutOfRangeException(nameof(occurrenceCount));

            AssetPath = assetPath;
            PackedBytes = packedBytes;
            OccurrenceCount = occurrenceCount;
        }

        /// <summary>Gets the source asset path or stable generated key.</summary>
        public string AssetPath { get; }

        /// <summary>Gets the sum of all packed occurrence bytes.</summary>
        public ulong PackedBytes { get; }

        /// <summary>Gets the number of packed occurrences included in the sum.</summary>
        public int OccurrenceCount { get; }
    }
}
