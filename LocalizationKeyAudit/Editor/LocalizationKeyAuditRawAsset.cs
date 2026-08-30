// SPDX-License-Identifier: MIT

using System;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 公式のローカライズAPIで型として読み取らずに収集した、1つの共有テーブルデータファイルの状態です。
    /// </summary>
    internal sealed class LocalizationKeyAuditRawAsset
    {
        /// <summary>
        /// 未加工ファイルのパス、読み取り状態、バイト列スナップショットを防御的に保持します。
        /// </summary>
        internal LocalizationKeyAuditRawAsset(
            string assetPath,
            string physicalPath,
            byte[] bytes,
            bool exists = true,
            bool hasReparsePoint = false,
            bool isOversize = false,
            string readError = "")
        {
            AssetPath = assetPath ?? string.Empty;
            PhysicalPath = physicalPath ?? string.Empty;
            Exists = exists;
            HasReparsePoint = hasReparsePoint;
            IsOversize = isOversize;
            ReadError = readError ?? string.Empty;
            m_Bytes = bytes == null ? Array.Empty<byte>() : (byte[])bytes.Clone();
        }

        /// <summary>Unityプロジェクト相対のアセットパスです。</summary>
        internal string AssetPath { get; }

        /// <summary>未加工バイト列を読んだ絶対物理パスです。</summary>
        internal string PhysicalPath { get; }

        /// <summary>収集時に通常ファイルが存在したかを示します。</summary>
        internal bool Exists { get; }

        /// <summary>ルートからファイルまでに再解析ポイントがあったかを示します。</summary>
        internal bool HasReparsePoint { get; }

        /// <summary>読み取り前にファイル1件の上限を超えていたかを示します。</summary>
        internal bool IsOversize { get; }

        /// <summary>未加工ファイルの読み取りを完了できなかった理由です。</summary>
        internal string ReadError { get; }

        /// <summary>収集済みバイト数です。</summary>
        internal int ByteCount => m_Bytes.Length;

        /// <summary>事前検査の解析処理用に独立したバイト列複製を返します。</summary>
        internal byte[] CopyBytes()
        {
            return (byte[])m_Bytes.Clone();
        }

        /// <summary>外部変更を許さない未加工バイト列スナップショットです。</summary>
        private readonly byte[] m_Bytes;
    }
}
