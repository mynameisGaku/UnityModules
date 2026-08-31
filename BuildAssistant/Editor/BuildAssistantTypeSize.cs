using System;

namespace BuildAssistant.Editor
{
    /// <summary>管理型名ごとにまとめた格納項目について、検査済みの合計容量を表します。</summary>
    public sealed class BuildAssistantTypeSize
    {
        /// <summary>型ごとの格納容量を表す変更不能な行を作成します。</summary>
        /// <param name="typeName">アセンブリ修飾付きの管理型名、または不明な型を安定して表す識別キー。</param>
        /// <param name="packedBytes">各格納項目の容量を検査しながら合計したバイト数。</param>
        /// <param name="occurrenceCount">合計に含めた格納項目の数。</param>
        /// <param name="assetCount">合計に含めた異なる元の素材の識別キーの数。</param>
        /// <exception cref="ArgumentException"><paramref name="typeName"/>が空の場合に発生します。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="occurrenceCount"/>または<paramref name="assetCount"/>が0未満の場合に発生します。</exception>
        public BuildAssistantTypeSize(string typeName, ulong packedBytes, int occurrenceCount, int assetCount)
        {
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentException("型名または安定した識別キーが必要です。", nameof(typeName));
            if (occurrenceCount < 0)
                throw new ArgumentOutOfRangeException(nameof(occurrenceCount));
            if (assetCount < 0)
                throw new ArgumentOutOfRangeException(nameof(assetCount));

            TypeName = typeName;
            PackedBytes = packedBytes;
            OccurrenceCount = occurrenceCount;
            AssetCount = assetCount;
        }

        /// <summary>アセンブリ修飾付きの管理型名、または不明な型を安定して表す識別キーを取得します。</summary>
        public string TypeName { get; }

        /// <summary>全格納項目の合計バイト数を取得します。</summary>
        public ulong PackedBytes { get; }

        /// <summary>合計に含まれる格納項目の数を取得します。</summary>
        public int OccurrenceCount { get; }

        /// <summary>合計に含まれる異なる元の素材の識別キーの数を取得します。</summary>
        public int AssetCount { get; }
    }
}
