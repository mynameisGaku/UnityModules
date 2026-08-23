using System;

namespace BuildAssistant.Editor
{
    /// <summary>Reports the checked sum of packed occurrences grouped by managed type name.</summary>
    public sealed class BuildAssistantTypeSize
    {
        /// <summary>Creates an immutable per-type packed-size row.</summary>
        /// <param name="typeName">The assembly-qualified managed type name, or a stable unknown-type key.</param>
        /// <param name="packedBytes">The checked sum of packed occurrence bytes.</param>
        /// <param name="occurrenceCount">The number of packed occurrences included in the sum.</param>
        /// <param name="assetCount">The number of distinct source-asset keys included in the sum.</param>
        public BuildAssistantTypeSize(string typeName, ulong packedBytes, int occurrenceCount, int assetCount)
        {
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentException("A type name or stable key is required.", nameof(typeName));
            if (occurrenceCount < 0)
                throw new ArgumentOutOfRangeException(nameof(occurrenceCount));
            if (assetCount < 0)
                throw new ArgumentOutOfRangeException(nameof(assetCount));

            TypeName = typeName;
            PackedBytes = packedBytes;
            OccurrenceCount = occurrenceCount;
            AssetCount = assetCount;
        }

        /// <summary>Gets the assembly-qualified managed type name or stable unknown-type key.</summary>
        public string TypeName { get; }

        /// <summary>Gets the sum of all packed occurrence bytes.</summary>
        public ulong PackedBytes { get; }

        /// <summary>Gets the number of packed occurrences included in the sum.</summary>
        public int OccurrenceCount { get; }

        /// <summary>Gets the number of distinct source-asset keys included in the sum.</summary>
        public int AssetCount { get; }
    }
}
