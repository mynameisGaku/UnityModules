// SPDX-License-Identifier: MIT

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 宣言済みの静的参照走査範囲から読み取った1アセットの不変バイト列です。
    /// </summary>
    internal sealed class LocalizationKeyAuditCoverageAsset
    {
        /// <summary>アセットパスと読み取り状態を防御的に複製します。</summary>
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

        /// <summary>Assets起点のUnityアセットパスです。</summary>
        internal string AssetPath { get; }

        /// <summary>物理ファイルが収集時に存在したかを示します。</summary>
        internal bool Exists { get; }

        /// <summary>走査範囲からファイルまでに再解析ポイントがあったかを示します。</summary>
        internal bool HasReparsePoint { get; }

        /// <summary>読み取り前にファイルサイズ上限を超えたかを示します。</summary>
        internal bool IsOversize { get; }

        /// <summary>個別ファイルの取得失敗です。</summary>
        internal string ReadError { get; }

        /// <summary>保持バイト数です。</summary>
        internal int ByteCount => storedBytes.Length;

        /// <summary>解析処理用の独立したバイト列複製を返します。</summary>
        internal byte[] CopyBytes()
        {
            return (byte[])storedBytes.Clone();
        }
    }
}
