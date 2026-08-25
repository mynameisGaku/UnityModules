// SPDX-License-Identifier: MIT

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 宣言済み static-reference scope から読み取った 1 asset の immutable bytes です。
    /// </summary>
    internal sealed class LocalizationKeyAuditCoverageAsset
    {
        /// <summary>asset path と読み取り状態を防御的に copy します。</summary>
        internal LocalizationKeyAuditCoverageAsset(
            string assetPath,
            byte[] bytes,
            bool exists = true,
            bool hasReparsePoint = false,
            bool isOversize = false,
            string readError = "")
        {
            AssetPath = assetPath ?? string.Empty;
            storedBytes = bytes == null ? System.Array.Empty<byte>() : (byte[])bytes.Clone();
            Exists = exists;
            HasReparsePoint = hasReparsePoint;
            IsOversize = isOversize;
            ReadError = readError ?? string.Empty;
        }

        private readonly byte[] storedBytes;

        /// <summary>Assets 起点の Unity asset path です。</summary>
        internal string AssetPath { get; }

        /// <summary>physical file が収集時に存在したかを示します。</summary>
        internal bool Exists { get; }

        /// <summary>scope から file までに reparse point があったかを示します。</summary>
        internal bool HasReparsePoint { get; }

        /// <summary>読み取り前に file size 上限を超えたかを示します。</summary>
        internal bool IsOversize { get; }

        /// <summary>個別 file の取得失敗です。</summary>
        internal string ReadError { get; }

        /// <summary>保持 byte 数です。</summary>
        internal int ByteCount => storedBytes.Length;

        /// <summary>parser 用の独立 byte copy を返します。</summary>
        internal byte[] CopyBytes()
        {
            return (byte[])storedBytes.Clone();
        }
    }
}
