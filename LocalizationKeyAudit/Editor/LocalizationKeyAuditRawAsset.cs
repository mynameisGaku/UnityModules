// SPDX-License-Identifier: MIT

using System;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// typed Localization API を使わず収集した 1 SharedTableData file の状態です。
    /// </summary>
    internal sealed class LocalizationKeyAuditRawAsset
    {
        /// <summary>
        /// raw file の path、読み取り状態、byte snapshot を防御的に保持します。
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

        /// <summary>Unity project relative asset path です。</summary>
        internal string AssetPath { get; }

        /// <summary>raw byte を読んだ absolute physical path です。</summary>
        internal string PhysicalPath { get; }

        /// <summary>収集時に通常 file が存在したかを示します。</summary>
        internal bool Exists { get; }

        /// <summary>root から file までに reparse point があったかを示します。</summary>
        internal bool HasReparsePoint { get; }

        /// <summary>読み取り前に 1 file 上限を超えていたかを示します。</summary>
        internal bool IsOversize { get; }

        /// <summary>raw file の読み取りを完了できなかった理由です。</summary>
        internal string ReadError { get; }

        /// <summary>収集済み byte 数です。</summary>
        internal int ByteCount => m_Bytes.Length;

        /// <summary>preflight parser 用に独立 byte copy を返します。</summary>
        internal byte[] CopyBytes()
        {
            return (byte[])m_Bytes.Clone();
        }

        /// <summary>外部変更を許さない raw byte snapshot です。</summary>
        private readonly byte[] m_Bytes;
    }
}

