// SPDX-License-Identifier: MIT

using System;

namespace PlayerOptions
{
    /// <summary>current schema本体を読む前にversionだけを安全に分類する内部JSON header。</summary>
    [Serializable]
    internal sealed class PlayerOptionsDocumentHeader
    {
        private const int MissingInt = int.MinValue;

        /// <summary>schema version欠落をsentinelで検出できるheaderを作る。</summary>
        internal PlayerOptionsDocumentHeader()
        {
            SchemaVersion = MissingInt;
        }

        /// <summary>保存文書のschema version。</summary>
        public int SchemaVersion;

        /// <summary>schema version fieldがJSON内に存在した場合はtrue。</summary>
        internal bool HasSchemaVersion => SchemaVersion != MissingInt;
    }
}
