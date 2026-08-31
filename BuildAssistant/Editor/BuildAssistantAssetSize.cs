using System;

namespace BuildAssistant.Editor
{
    /// <summary>1つの元の素材に属する全格納項目について、検査済みの合計容量を表します。</summary>
    public sealed class BuildAssistantAssetSize
    {
        /// <summary>素材ごとの格納容量を表す変更不能な行を作成します。</summary>
        /// <param name="assetPath">元の素材のパス、または安定して生成される識別キー。</param>
        /// <param name="packedBytes">各格納項目の容量を検査しながら合計したバイト数。</param>
        /// <param name="occurrenceCount">合計に含めた格納項目の数。</param>
        /// <exception cref="ArgumentException"><paramref name="assetPath"/>が空の場合に発生します。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="occurrenceCount"/>が0未満の場合に発生します。</exception>
        public BuildAssistantAssetSize(string assetPath, ulong packedBytes, int occurrenceCount)
        {
            if (string.IsNullOrEmpty(assetPath))
                throw new ArgumentException("元の素材のパスまたは安定した識別キーが必要です。", nameof(assetPath));
            if (occurrenceCount < 0)
                throw new ArgumentOutOfRangeException(nameof(occurrenceCount));

            AssetPath = assetPath;
            PackedBytes = packedBytes;
            OccurrenceCount = occurrenceCount;
        }

        /// <summary>元の素材のパス、または安定して生成された識別キーを取得します。</summary>
        public string AssetPath { get; }

        /// <summary>全格納項目の合計バイト数を取得します。</summary>
        public ulong PackedBytes { get; }

        /// <summary>合計に含まれる格納項目の数を取得します。</summary>
        public int OccurrenceCount { get; }
    }
}
