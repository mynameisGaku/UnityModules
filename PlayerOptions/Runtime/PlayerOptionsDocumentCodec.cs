// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace PlayerOptions
{
    /// <summary>version付きJSON文書と公開stateの相互変換を所有する。</summary>
    internal sealed class PlayerOptionsDocumentCodec
    {
        private readonly PlayerOptionsMigrationPipeline _migrations;

        /// <summary>読込時に使用する逐次migration pipelineを指定する。</summary>
        internal PlayerOptionsDocumentCodec(PlayerOptionsMigrationPipeline migrations)
        {
            _migrations = migrations ?? throw new ArgumentNullException(nameof(migrations));
        }

        /// <summary>保存文字列をstateへ変換し、未対応または破損を分類する。</summary>
        internal bool TryDecode(
            string contents,
            out PlayerOptionsState state,
            out bool wasMigrated,
            out PlayerOptionsError error,
            out string message)
        {
            state = default;
            wasMigrated = false;
            error = PlayerOptionsError.None;
            message = string.Empty;

            if (string.IsNullOrEmpty(contents))
            {
                error = PlayerOptionsError.CorruptData;
                message = "保存文書が空です。";
                return false;
            }

            if (contents.Length > PlayerOptionsSchema.MaximumDocumentLength)
            {
                error = PlayerOptionsError.CorruptData;
                message = "保存文書が許容長を超えています。";
                return false;
            }

            var header = new PlayerOptionsDocumentHeader();
            try
            {
                JsonUtility.FromJsonOverwrite(contents, header);
            }
            catch (Exception exception)
            {
                error = PlayerOptionsError.CorruptData;
                message = $"保存文書のschema headerをJSONとして読めませんでした: {SafeMessage(exception)}";
                return false;
            }

            if (!header.HasSchemaVersion || header.SchemaVersion <= 0)
            {
                error = PlayerOptionsError.CorruptData;
                message = "保存文書のschema versionが欠落または0以下です。";
                return false;
            }

            if (header.SchemaVersion > _migrations.TargetVersion)
            {
                error = PlayerOptionsError.UnsupportedSchemaVersion;
                message = $"schema version {header.SchemaVersion} は対応版 {_migrations.TargetVersion} より新しいため読み込めません。";
                return false;
            }

            var document = new PlayerOptionsDocument();
            try
            {
                JsonUtility.FromJsonOverwrite(contents, document);
            }
            catch (Exception exception)
            {
                error = PlayerOptionsError.CorruptData;
                message = $"保存文書をJSONとして読めませんでした: {SafeMessage(exception)}";
                return false;
            }

            if (document.SchemaVersion == int.MinValue || document.SchemaVersion <= 0)
            {
                error = PlayerOptionsError.CorruptData;
                message = "保存文書のschema versionが欠落または0以下です。";
                return false;
            }

            PlayerOptionsDocument current;
            try
            {
                if (!_migrations.TryMigrate(
                        document,
                        out current,
                        out wasMigrated,
                        out error,
                        out message))
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                wasMigrated = false;
                error = PlayerOptionsError.MigrationFailed;
                message = $"保存文書のmigration処理が例外で失敗しました: {SafeMessage(exception)}";
                return false;
            }

            if (!current.HasAllRequiredFields)
            {
                error = PlayerOptionsError.CorruptData;
                message = "保存文書に必須fieldがありません。";
                return false;
            }

            try
            {
                state = current.ToState();
            }
            catch (OverflowException exception)
            {
                error = PlayerOptionsError.CorruptData;
                message = $"保存文書のrefresh rateがuint範囲外です: {SafeMessage(exception)}";
                return false;
            }

            return true;
        }

        /// <summary>正規化済みstateをcurrent schemaのJSON文書へ変換する。</summary>
        internal bool TryEncode(PlayerOptionsState state, out string contents, out string message)
        {
            contents = null;
            message = string.Empty;
            try
            {
                contents = JsonUtility.ToJson(PlayerOptionsDocument.FromState(state));
            }
            catch (Exception exception)
            {
                message = $"player optionをJSONへ変換できませんでした: {SafeMessage(exception)}";
                return false;
            }

            if (string.IsNullOrEmpty(contents) || contents.Length > PlayerOptionsSchema.MaximumDocumentLength)
            {
                contents = null;
                message = "player optionのJSON文書が空、または許容長を超えています。";
                return false;
            }

            return true;
        }

        private static string SafeMessage(Exception exception)
        {
            var message = string.IsNullOrWhiteSpace(exception?.Message)
                ? exception?.GetType().Name ?? "Unknown error"
                : exception.Message;
            return message.Length <= 1024 ? message : message.Substring(0, 1024);
        }
    }
}
