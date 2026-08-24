// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace PlayerOptions
{
    /// <summary>一つの旧versionを直後のversionへ変換する内部migration境界。</summary>
    internal interface IPlayerOptionsDocumentMigration
    {
        /// <summary>入力schema version。</summary>
        int SourceVersion { get; }

        /// <summary>出力schema version。SourceVersionの直後でなければならない。</summary>
        int TargetVersion { get; }

        /// <summary>一つの旧文書を直後のversionへ変換する。</summary>
        bool TryMigrate(
            PlayerOptionsDocument source,
            out PlayerOptionsDocument migrated,
            out string message);
    }

    /// <summary>登録済みのNからN+1への変換だけをcurrent schemaまで順番に適用する。</summary>
    internal sealed class PlayerOptionsMigrationPipeline
    {
        /// <summary>v1.0.0には実在する旧schemaがないため空のproduction pipelineを使う。</summary>
        internal static readonly PlayerOptionsMigrationPipeline Default =
            new PlayerOptionsMigrationPipeline(
                PlayerOptionsSchema.CurrentVersion,
                Array.Empty<IPlayerOptionsDocumentMigration>());

        private readonly IPlayerOptionsDocumentMigration[] _migrations;

        /// <summary>production target version向けmigration一覧を作る。</summary>
        internal PlayerOptionsMigrationPipeline(IReadOnlyList<IPlayerOptionsDocumentMigration> migrations)
            : this(PlayerOptionsSchema.CurrentVersion, migrations)
        {
        }

        /// <summary>target versionとmigration一覧をsnapshotし、重複、範囲外、gapを拒否する。</summary>
        internal PlayerOptionsMigrationPipeline(
            int targetVersion,
            IReadOnlyList<IPlayerOptionsDocumentMigration> migrations)
        {
            if (targetVersion <= 0) throw new ArgumentOutOfRangeException(nameof(targetVersion));
            if (migrations == null) throw new ArgumentNullException(nameof(migrations));
            if (migrations.Count != targetVersion - 1)
            {
                throw new ArgumentException("schema version 1からtarget versionまでのmigrationを連続して指定してください。", nameof(migrations));
            }

            TargetVersion = targetVersion;
            _migrations = new IPlayerOptionsDocumentMigration[migrations.Count];
            for (var index = 0; index < migrations.Count; index++)
            {
                var migration = migrations[index] ?? throw new ArgumentException("migration一覧にnullがあります。", nameof(migrations));
                if (migration.SourceVersion <= 0 ||
                    migration.SourceVersion >= targetVersion ||
                    migration.TargetVersion != migration.SourceVersion + 1)
                {
                    throw new ArgumentException("migrationはtarget version未満の正のNからN+1だけを変換してください。", nameof(migrations));
                }

                for (var previous = 0; previous < index; previous++)
                {
                    if (_migrations[previous].SourceVersion == migration.SourceVersion)
                    {
                        throw new ArgumentException("同じsource versionのmigrationが重複しています。", nameof(migrations));
                    }
                }

                _migrations[index] = migration;
            }

            for (var sourceVersion = 1; sourceVersion < targetVersion; sourceVersion++)
            {
                if (Find(sourceVersion) == null)
                {
                    throw new ArgumentException("migration一覧にschema versionのgapがあります。", nameof(migrations));
                }
            }
        }

        /// <summary>このpipelineが文書を変換するschema version。</summary>
        internal int TargetVersion { get; }

        /// <summary>文書をこのpipelineのtarget schemaまで順番に変換する。</summary>
        internal bool TryMigrate(
            PlayerOptionsDocument source,
            out PlayerOptionsDocument migrated,
            out bool wasMigrated,
            out PlayerOptionsError error,
            out string message)
        {
            migrated = source;
            wasMigrated = false;
            error = PlayerOptionsError.None;
            message = string.Empty;

            if (source == null)
            {
                error = PlayerOptionsError.CorruptData;
                message = "保存文書がnullです。";
                return false;
            }

            if (source.SchemaVersion <= 0)
            {
                error = PlayerOptionsError.CorruptData;
                message = "保存文書のschema versionが欠落または0以下です。";
                return false;
            }

            if (source.SchemaVersion > TargetVersion)
            {
                error = PlayerOptionsError.UnsupportedSchemaVersion;
                message = $"schema version {source.SchemaVersion} は対応版 {TargetVersion} より新しいため読み込めません。";
                return false;
            }

            while (migrated.SchemaVersion < TargetVersion)
            {
                var migration = Find(migrated.SchemaVersion);
                if (migration == null)
                {
                    error = PlayerOptionsError.UnsupportedSchemaVersion;
                    message = $"schema version {migrated.SchemaVersion} から次版へのmigrationがありません。";
                    return false;
                }

                if (!migration.TryMigrate(migrated, out var next, out var migrationMessage) || next == null)
                {
                    error = PlayerOptionsError.CorruptData;
                    message = string.IsNullOrEmpty(migrationMessage)
                        ? $"schema version {migrated.SchemaVersion} をmigrationできませんでした。"
                        : migrationMessage;
                    return false;
                }

                if (next.SchemaVersion != migration.TargetVersion)
                {
                    error = PlayerOptionsError.CorruptData;
                    message = "migration結果のschema versionが契約と一致しません。";
                    return false;
                }

                migrated = next;
                wasMigrated = true;
            }

            return true;
        }

        private IPlayerOptionsDocumentMigration Find(int sourceVersion)
        {
            for (var index = 0; index < _migrations.Length; index++)
            {
                if (_migrations[index].SourceVersion == sourceVersion) return _migrations[index];
            }

            return null;
        }
    }
}
