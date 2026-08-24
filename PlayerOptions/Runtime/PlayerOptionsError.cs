// SPDX-License-Identifier: MIT

namespace PlayerOptions
{
    /// <summary>player optionの読込、変更、適用、保存が失敗した理由。</summary>
    public enum PlayerOptionsError
    {
        /// <summary>失敗していない。</summary>
        None = 0,

        /// <summary>option値または現在の実行環境との組合せが不正。</summary>
        InvalidOptions = 1,

        /// <summary>保存文書が欠落field、不正値、または解釈不能なJSONを含む。</summary>
        CorruptData = 2,

        /// <summary>保存文書が未対応の正のschema versionを持つ。</summary>
        UnsupportedSchemaVersion = 3,

        /// <summary>保存先から文書を読み取れなかった。</summary>
        StorageReadFailed = 4,

        /// <summary>保存先へ文書を書き込めなかった。</summary>
        StorageWriteFailed = 5,

        /// <summary>現在状態をJSON文書へ変換できなかった。</summary>
        SerializationFailed = 6,

        /// <summary>Unity runtimeへの適用が失敗し、変更済みの同期値は復元できた。画面要求はwarningも確認する。</summary>
        ApplyFailed = 7,

        /// <summary>Unity runtimeへの適用失敗後に、変更済みの同期値を完全には復元できなかった。</summary>
        RollbackFailed = 8,

        /// <summary>画面または品質などの現在状態を安全に確認できなかった。</summary>
        RuntimeUnavailable = 9,

        /// <summary>Unity main thread以外から操作された。</summary>
        MainThreadRequired = 10,

        /// <summary>別操作または変更通知の処理中。</summary>
        Busy = 11,

        /// <summary>旧schemaからcurrent schemaへのmigration処理が例外で失敗した。</summary>
        MigrationFailed = 12,
    }
}
