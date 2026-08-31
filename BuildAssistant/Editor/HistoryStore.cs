using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BuildAssistant.Editor
{
    internal sealed class HistoryStore
    {
        internal const int MaximumEntryCount = 20;
        internal const int MaximumDocumentBytes = 16 * 1024 * 1024;
        internal const int MaximumDefineCount = 4096;
        internal const int MaximumSceneCount = 4096;
        internal const int MaximumAssetCount = 100000;
        internal const int MaximumTypeCount = 32768;
        internal const int MaximumShortTextLength = 4096;
        internal const int MaximumPathLength = 32768;
        private const int MaximumMessageLength = 32768;
        private const int SchemaVersion = 1;
        private static readonly BuildOptions SupportedBuildOptions = BuildOptions.DetailedBuildReport | BuildOptions.Development | BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging | BuildOptions.WaitForPlayerConnection | BuildOptions.EnableCodeCoverage | BuildOptions.EnableDeepProfilingSupport | BuildOptions.CompressWithLz4 | BuildOptions.CompressWithLz4HC | BuildOptions.SymlinkSources;
        private static readonly BuildOptions DevelopmentOnlyBuildOptions = BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging | BuildOptions.WaitForPlayerConnection | BuildOptions.EnableCodeCoverage | BuildOptions.EnableDeepProfilingSupport;
        private static readonly string[] NoMembers = Array.Empty<string>();
        private static readonly string[] HistoryDocumentMembers = { "schemaVersion", "entries" };
        private static readonly string[] RunStateDocumentMembers = { "schemaVersion", "completed", "entry" };
        private static readonly string[] EntryMembers = { "runId", "createdAtUtc", "startedAtUtc", "completedAtUtc", "status", "error", "message", "outputRoot", "runDirectory", "artifactPath", "profileKind", "profileGuid", "profileName", "profilePath", "profileDependencyHash", "profileStableId", "target", "targetGroup", "namedBuildTarget", "subtarget", "scriptingBackend", "options", "effectiveDefines", "scenes", "totalErrors", "totalWarnings", "totalOutputBytes", "packedContentBytes", "packedOverheadBytes", "assets", "types", "previousRunId", "totalOutputDeltaBytes", "packedContentDeltaBytes" };
        private static readonly string[] EntryStringMembers = { "runId", "createdAtUtc", "startedAtUtc", "completedAtUtc", "message", "outputRoot", "runDirectory", "artifactPath", "profileGuid", "profileName", "profilePath", "profileDependencyHash", "profileStableId", "namedBuildTarget", "totalOutputBytes", "packedContentBytes", "packedOverheadBytes", "previousRunId", "totalOutputDeltaBytes", "packedContentDeltaBytes" };
        private static readonly string[] EntryNumberMembers = { "status", "error", "profileKind", "target", "targetGroup", "subtarget", "scriptingBackend", "options", "totalErrors", "totalWarnings" };
        private static readonly string[] SceneMembers = { "order", "guid", "assetPath", "enabled", "dependencyHash" };
        private static readonly string[] AssetMembers = { "assetPath", "packedBytes", "occurrenceCount" };
        private static readonly string[] TypeMembers = { "typeName", "packedBytes", "occurrenceCount", "assetCount" };
        private readonly BuildAssistantFileSystem fileSystem;
        private readonly string directoryPath;
        private readonly string historyPath;
        private readonly string historyBackupPath;
        private readonly string runStatePath;
        private readonly string runStateBackupPath;

        internal HistoryStore(string projectRoot, BuildAssistantFileSystem fileSystem = null)
        {
            if (string.IsNullOrWhiteSpace(projectRoot) || !Path.IsPathRooted(projectRoot))
                throw new ArgumentException("プロジェクトの絶対パスが必要です。", nameof(projectRoot));
            this.fileSystem = fileSystem ?? new BuildAssistantFileSystem();
            directoryPath = Path.Combine(Path.GetFullPath(projectRoot), "Library", "BuildAssistant");
            historyPath = Path.Combine(directoryPath, "history.json");
            historyBackupPath = historyPath + ".bak";
            runStatePath = Path.Combine(directoryPath, "run-state.json");
            runStateBackupPath = runStatePath + ".bak";
        }

        internal BuildAssistantHistory Load()
        {
            if (TryLoadHistory(historyPath, out var primary))
                return new BuildAssistantHistory(primary, false, string.Empty);
            if (TryLoadHistory(historyBackupPath, out var backup))
                return new BuildAssistantHistory(backup, true, "履歴の主ファイルがないか無効だったため、予備ファイルを読み込みました。");
            return new BuildAssistantHistory(Array.Empty<BuildAssistantHistoryEntry>(), false, FileMayExist(historyPath) || FileMayExist(historyBackupPath) ? "有効な履歴ファイルを読み込めませんでした。" : string.Empty);
        }

        internal void Save(IEnumerable<BuildAssistantHistoryEntry> entries, string requiredRunId = "")
        {
            var normalized = NormalizeEntries(entries, requiredRunId);
            if (normalized.Any(entry => !ValidateEntry(entry)))
                throw new InvalidDataException("保存しようとした履歴に、時刻、状態、件数、または出力先の不整合があります。");
            var document = new HistoryDocument { schemaVersion = SchemaVersion, entries = normalized.Select(ToData).ToArray() };
            var primaryExists = fileSystem.FileExists(historyPath);
            var backupExists = fileSystem.FileExists(historyBackupPath);
            var primaryValid = primaryExists && TryLoadHistory(historyPath, out _);
            var backupValid = backupExists && TryLoadHistory(historyBackupPath, out _);
            if ((primaryExists || backupExists) && !primaryValid && !backupValid)
                throw new InvalidDataException("既存の履歴に有効な主ファイルまたは予備ファイルがないため、上書きできません。");
            var preserveValidBackup = primaryExists && !primaryValid && backupValid;
            WriteAtomic(historyPath, historyBackupPath, JsonUtility.ToJson(document, true) + Environment.NewLine, preserveValidBackup);
        }

        internal void SaveRunState(RunState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (!ValidateRunState(state))
                throw new InvalidDataException("保存しようとした実行状態に、時刻、状態、件数、または出力先の不整合があります。");
            var document = new RunStateDocument { schemaVersion = SchemaVersion, completed = state.Completed, entry = ToData(state.Entry) };
            var primaryExists = fileSystem.FileExists(runStatePath);
            var backupExists = fileSystem.FileExists(runStateBackupPath);
            var primaryValid = primaryExists && TryLoadRunState(runStatePath, out _);
            var backupValid = backupExists && TryLoadRunState(runStateBackupPath, out _);
            if ((primaryExists || backupExists) && !primaryValid && !backupValid)
                throw new InvalidDataException("既存の実行状態に有効な主ファイルまたは予備ファイルがないため、上書きできません。");
            var preserveValidBackup = primaryExists && !primaryValid && backupValid;
            WriteAtomic(runStatePath, runStateBackupPath, JsonUtility.ToJson(document, true) + Environment.NewLine, preserveValidBackup);
        }

        internal bool HasRunState => FileMayExist(runStatePath) || FileMayExist(runStateBackupPath);

        internal bool HasUnreadableHistory
        {
            get
            {
                var primaryExists = FileMayExist(historyPath);
                var backupExists = FileMayExist(historyBackupPath);
                return (primaryExists || backupExists) && !(primaryExists && TryLoadHistory(historyPath, out _)) && !(backupExists && TryLoadHistory(historyBackupPath, out _));
            }
        }

        internal BuildAssistantHistory RecoverInterrupted(DateTime completedAtUtc)
        {
            var state = LoadRunState();
            if (state == null)
                return Load();

            var history = Load();
            var persistedTerminal = history.Entries.FirstOrDefault(entry => StringComparer.Ordinal.Equals(entry.RunId, state.Entry.RunId) && entry.Status != BuildAssistantHistoryStatus.Interrupted);
            var terminal = persistedTerminal ?? (state.Completed ? state.Entry : state.AsInterrupted(completedAtUtc).Entry);
            var entries = history.Entries.Where(entry => !StringComparer.Ordinal.Equals(entry.RunId, terminal.RunId)).Concat(new[] { terminal });
            Save(entries, terminal.RunId);
            var message = state.Completed ? "完了済みの実行状態を履歴へ復旧しました。" : persistedTerminal != null ? "保存済みの終了結果を維持し、古い実行中状態を取り除きました。" : "中断された実行を、自動再実行せず履歴へ記録しました。";
            try
            {
                DeleteRunState();
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                message += " 実行状態の後片付けは次回に再試行します。";
            }

            return new BuildAssistantHistory(NormalizeEntries(entries, terminal.RunId), history.RecoveredFromBackup, message);
        }

        internal void DeleteRunState()
        {
            fileSystem.DeleteFile(runStateBackupPath);
            fileSystem.DeleteFile(runStatePath);
        }

        /// <summary>主・予備の両方が無効な実行状態だけを、削除せず別名へ隔離します。</summary>
        internal IReadOnlyList<string> QuarantineInvalidRunState(string quarantineId)
        {
            if (string.IsNullOrWhiteSpace(quarantineId) || quarantineId.Any(character => !char.IsLetterOrDigit(character) && character != '-'))
                throw new ArgumentException("隔離識別子には英数字またはハイフンが必要です。", nameof(quarantineId));
            if (TryLoadRunState(runStatePath, out _) || TryLoadRunState(runStateBackupPath, out _))
                throw new InvalidOperationException("有効な実行状態は隔離できません。");

            var quarantined = new List<string>();
            MoveInvalidRunState(runStatePath, quarantineId, quarantined);
            MoveInvalidRunState(runStateBackupPath, quarantineId, quarantined);
            return quarantined.AsReadOnly();
        }

        /// <summary>有効な内容が一つもない履歴ファイルだけを、削除せず別名へ隔離します。</summary>
        internal IReadOnlyList<string> QuarantineInvalidHistory(string quarantineId)
        {
            if (string.IsNullOrWhiteSpace(quarantineId) || quarantineId.Any(character => !char.IsLetterOrDigit(character) && character != '-'))
                throw new ArgumentException("隔離識別子には英数字またはハイフンが必要です。", nameof(quarantineId));
            if (!HasUnreadableHistory)
                throw new InvalidOperationException("有効な履歴があるか、隔離する履歴がありません。");

            var quarantined = new List<string>();
            MoveInvalidHistory(historyPath, quarantineId, quarantined);
            MoveInvalidHistory(historyBackupPath, quarantineId, quarantined);
            return quarantined.AsReadOnly();
        }

        internal static string SerializeExport(BuildAssistantHistoryEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (!ValidateEntry(entry))
                throw new InvalidDataException("書き出そうとした履歴に、時刻、状態、件数、または出力先の不整合があります。");
            var document = new ExportDocument { schemaVersion = SchemaVersion, entry = ToData(entry) };
            var json = JsonUtility.ToJson(document, true) + Environment.NewLine;
            EnsureDocumentSize(json);
            return json;
        }

        private RunState LoadRunState()
        {
            if (TryLoadRunState(runStatePath, out var primary))
                return primary;
            if (TryLoadRunState(runStateBackupPath, out var backup))
                return backup;
            if (HasRunState)
                throw new InvalidDataException("有効な実行状態ファイルを読み込めませんでした。");
            return null;
        }

        private void MoveInvalidRunState(string sourcePath, string quarantineId, ICollection<string> quarantined)
        {
            if (!fileSystem.FileExists(sourcePath))
                return;
            var destinationPath = sourcePath + ".invalid-" + quarantineId;
            if (fileSystem.FileExists(destinationPath) || fileSystem.DirectoryExists(destinationPath))
                throw new IOException("実行状態の隔離先が既に存在します。");
            fileSystem.MoveFile(sourcePath, destinationPath);
            quarantined.Add(destinationPath);
        }

        private void MoveInvalidHistory(string sourcePath, string quarantineId, ICollection<string> quarantined)
        {
            if (!fileSystem.FileExists(sourcePath))
                return;
            var destinationPath = sourcePath + ".invalid-" + quarantineId;
            if (fileSystem.FileExists(destinationPath) || fileSystem.DirectoryExists(destinationPath))
                throw new IOException("履歴の隔離先が既に存在します。");
            fileSystem.MoveFile(sourcePath, destinationPath);
            quarantined.Add(destinationPath);
        }

        private bool TryLoadHistory(string path, out BuildAssistantHistoryEntry[] entries)
        {
            entries = null;
            try
            {
                if (!fileSystem.FileExists(path))
                    return false;
                var json = fileSystem.ReadAllTextBounded(path, MaximumDocumentBytes);
                if (!HasExpectedHistoryStructure(json))
                    return false;
                var document = JsonUtility.FromJson<HistoryDocument>(json);
                if (document == null || document.schemaVersion != SchemaVersion || document.entries == null || document.entries.Length > MaximumEntryCount)
                    return false;
                entries = document.entries.Select(FromData).ToArray();
                return entries.All(ValidateEntry);
            }
            catch (Exception exception) when (IsFileSystemException(exception) || exception is InvalidOperationException || exception is InvalidDataException || exception is FormatException || exception is OverflowException || exception is NullReferenceException)
            {
                return false;
            }
        }

        private bool TryLoadRunState(string path, out RunState state)
        {
            state = null;
            try
            {
                if (!fileSystem.FileExists(path))
                    return false;
                var json = fileSystem.ReadAllTextBounded(path, MaximumDocumentBytes);
                if (!HasExpectedRunStateStructure(json))
                    return false;
                var document = JsonUtility.FromJson<RunStateDocument>(json);
                if (document == null || document.schemaVersion != SchemaVersion || document.entry == null)
                    return false;
                var entry = FromData(document.entry);
                var loadedState = new RunState(document.completed, entry);
                if (!ValidateRunState(loadedState))
                    return false;
                state = loadedState;
                return true;
            }
            catch (Exception exception) when (IsFileSystemException(exception) || exception is InvalidOperationException || exception is InvalidDataException || exception is FormatException || exception is OverflowException || exception is NullReferenceException)
            {
                return false;
            }
        }

        private void WriteAtomic(string path, string backupPath, string json, bool preserveValidBackup)
        {
            EnsureDocumentSize(json);
            fileSystem.CreateDirectory(directoryPath);
            var operationId = Guid.NewGuid().ToString("N");
            var temporaryPath = path + "." + operationId + ".tmp";
            var replacementBackupPath = preserveValidBackup ? path + "." + operationId + ".invalid" : backupPath;
            try
            {
                fileSystem.WriteAllTextFlushed(temporaryPath, json, FileMode.CreateNew);
                if (fileSystem.FileExists(path))
                    fileSystem.ReplaceFile(temporaryPath, path, replacementBackupPath);
                else
                    fileSystem.MoveFile(temporaryPath, path);
            }
            finally
            {
                try
                {
                    fileSystem.DeleteFile(temporaryPath);
                }
                catch (Exception exception) when (IsFileSystemException(exception))
                {
                }
                if (preserveValidBackup)
                {
                    try
                    {
                        fileSystem.DeleteFile(replacementBackupPath);
                    }
                    catch (Exception exception) when (IsFileSystemException(exception))
                    {
                    }
                }
            }
        }

        private static bool IsFileSystemException(Exception exception) => exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException || exception is ArgumentException || exception is NotSupportedException;

        /// <summary>存在確認を拒否された場合は、対象を安全側に「存在する可能性あり」と扱います。</summary>
        private bool FileMayExist(string path)
        {
            try
            {
                return fileSystem.FileExists(path);
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                return true;
            }
        }

        /// <summary>履歴文書の各要素に、書き出し時の全項目が一度ずつ存在するか確認します。</summary>
        private static bool HasExpectedHistoryStructure(string json)
        {
            if (!JsonDocumentShape.TryParse(json, out var root) || !HasTypedMembers(root, HistoryDocumentMembers, NoMembers, new[] { "schemaVersion" }, NoMembers) || !TryGetArray(root, "entries", out var entryShapes) || entryShapes.Count > MaximumEntryCount)
                return false;
            for (var index = 0; index < entryShapes.Count; index++)
            {
                if (!HasExpectedEntryStructure(entryShapes[index]))
                    return false;
            }
            return true;
        }

        /// <summary>実行状態文書の完了印と履歴項目が、省略されず一度ずつ存在するか確認します。</summary>
        private static bool HasExpectedRunStateStructure(string json)
        {
            if (!JsonDocumentShape.TryParse(json, out var root) || !HasTypedMembers(root, RunStateDocumentMembers, NoMembers, new[] { "schemaVersion" }, new[] { "completed" }) || !root.TryGetMember("entry", out var entryShape))
                return false;
            return HasExpectedEntryStructure(entryShape);
        }

        /// <summary>一件の履歴に、単純値と全ての入れ子配列がそろっているか確認します。</summary>
        private static bool HasExpectedEntryStructure(JsonDocumentShape shape)
        {
            if (!HasTypedMembers(shape, EntryMembers, EntryStringMembers, EntryNumberMembers, NoMembers))
                return false;
            if (!TryGetArray(shape, "effectiveDefines", out var defineShapes) || defineShapes.Count > MaximumDefineCount || defineShapes.Any(value => !value.IsString))
                return false;
            if (!TryGetArray(shape, "scenes", out var sceneShapes) || sceneShapes.Count > MaximumSceneCount || !HasMatchingObjects(sceneShapes, SceneMembers, new[] { "guid", "assetPath", "dependencyHash" }, new[] { "order" }, new[] { "enabled" }))
                return false;
            if (!TryGetArray(shape, "assets", out var assetShapes) || assetShapes.Count > MaximumAssetCount || !HasMatchingObjects(assetShapes, AssetMembers, new[] { "assetPath", "packedBytes" }, new[] { "occurrenceCount" }, NoMembers))
                return false;
            return TryGetArray(shape, "types", out var typeShapes) && typeShapes.Count <= MaximumTypeCount && HasMatchingObjects(typeShapes, TypeMembers, new[] { "typeName", "packedBytes" }, new[] { "occurrenceCount", "assetCount" }, NoMembers);
        }

        /// <summary>指定した全項目だけを持ち、各項目の値種別が正しいか確認します。</summary>
        private static bool HasTypedMembers(JsonDocumentShape shape, IReadOnlyList<string> allMembers, IReadOnlyList<string> stringMembers, IReadOnlyList<string> numberMembers, IReadOnlyList<string> booleanMembers)
        {
            if (!shape.HasExactMembers(allMembers))
                return false;
            for (var index = 0; index < stringMembers.Count; index++)
            {
                if (!shape.TryGetMember(stringMembers[index], out var value) || !value.IsString)
                    return false;
            }
            for (var index = 0; index < numberMembers.Count; index++)
            {
                if (!shape.TryGetMember(numberMembers[index], out var value) || !value.IsInteger)
                    return false;
            }
            for (var index = 0; index < booleanMembers.Count; index++)
            {
                if (!shape.TryGetMember(booleanMembers[index], out var value) || !value.IsBoolean)
                    return false;
            }
            return true;
        }

        /// <summary>配列項目を取得し、別の値種別だった場合は失敗します。</summary>
        private static bool TryGetArray(JsonDocumentShape shape, string memberName, out IReadOnlyList<JsonDocumentShape> items)
        {
            items = null;
            return shape.TryGetMember(memberName, out var value) && value.TryGetItems(out items);
        }

        /// <summary>入れ子の全要素が存在し、各要素に必要な単純値だけが一度ずつあるか確認します。</summary>
        private static bool HasMatchingObjects(IReadOnlyList<JsonDocumentShape> shapes, IReadOnlyList<string> members, IReadOnlyList<string> stringMembers, IReadOnlyList<string> numberMembers, IReadOnlyList<string> booleanMembers)
        {
            for (var index = 0; index < shapes.Count; index++)
            {
                if (!HasTypedMembers(shapes[index], members, stringMembers, numberMembers, booleanMembers))
                    return false;
            }
            return true;
        }

        private static BuildAssistantHistoryEntry[] NormalizeEntries(IEnumerable<BuildAssistantHistoryEntry> entries, string requiredRunId = "")
        {
            var candidates = (entries ?? Enumerable.Empty<BuildAssistantHistoryEntry>()).Where(entry => entry != null).ToArray();
            if (string.IsNullOrEmpty(requiredRunId))
                return candidates.GroupBy(entry => entry.RunId, StringComparer.Ordinal).Select(group => group.OrderByDescending(entry => entry.CompletedAtUtc).First()).OrderByDescending(entry => entry.CompletedAtUtc).ThenByDescending(entry => entry.RunId, StringComparer.Ordinal).Take(MaximumEntryCount).ToArray();

            var required = candidates.LastOrDefault(entry => StringComparer.Ordinal.Equals(entry.RunId, requiredRunId));
            if (required == null)
                throw new InvalidDataException("今回の実行結果が保存対象の履歴に含まれていません。");

            var selected = new List<BuildAssistantHistoryEntry> { required };
            var seenRunIds = new HashSet<string>(StringComparer.Ordinal) { requiredRunId };
            foreach (var candidate in candidates)
            {
                if (seenRunIds.Add(candidate.RunId))
                    selected.Add(candidate);
                if (selected.Count == MaximumEntryCount)
                    break;
            }
            return selected.ToArray();
        }

        private static bool ValidateEntry(BuildAssistantHistoryEntry entry)
        {
            if (entry == null || !IsBoundedRequiredText(entry.RunId, MaximumShortTextLength) || entry.TotalErrors < 0 || entry.TotalWarnings < 0)
                return false;
            if (!Enum.IsDefined(typeof(BuildAssistantHistoryStatus), entry.Status) || !Enum.IsDefined(typeof(BuildAssistantError), entry.Error) || !Enum.IsDefined(typeof(BuildAssistantProfileKind), entry.ProfileKind))
                return false;
            if (!ValidateBuildSettings(entry))
                return false;
            if (entry.CreatedAtUtc > entry.StartedAtUtc || entry.StartedAtUtc > entry.CompletedAtUtc)
                return false;
            if (!AreNestedCountsSupported(entry.EffectiveDefines.Count, entry.Scenes.Count, entry.Assets.Count, entry.Types.Count))
                return false;
            if (!IsBoundedText(entry.Message, MaximumMessageLength) || !IsBoundedRequiredText(entry.OutputRoot, MaximumPathLength) || !IsBoundedRequiredText(entry.RunDirectory, MaximumPathLength) || !IsBoundedRequiredText(entry.ArtifactPath, MaximumPathLength))
                return false;
            if (!IsBoundedText(entry.ProfileGuid, MaximumShortTextLength) || !IsBoundedText(entry.ProfileName, MaximumShortTextLength) || !IsBoundedText(entry.ProfilePath, MaximumPathLength) || !IsBoundedRequiredText(entry.ProfileDependencyHash, MaximumShortTextLength) || !IsBoundedRequiredText(entry.ProfileStableId, MaximumShortTextLength) || !IsBoundedText(entry.PreviousRunId, MaximumShortTextLength))
                return false;
            if (!SafeBuildOutput.IsContained(entry.OutputRoot, entry.RunDirectory) || !SafeBuildOutput.IsContained(entry.RunDirectory, entry.ArtifactPath))
                return false;
            if (!ValidateStatusAndError(entry.Status, entry.Error))
                return false;
            return entry.EffectiveDefines.All(value => IsBoundedRequiredText(value, MaximumShortTextLength))
                && entry.Scenes.Select((scene, index) => scene != null && scene.Order == index && IsBoundedText(scene.Guid, MaximumShortTextLength) && IsBoundedText(scene.AssetPath, MaximumPathLength) && IsBoundedText(scene.DependencyHash, MaximumShortTextLength)).All(valid => valid)
                && entry.Assets.All(asset => asset != null && IsBoundedRequiredText(asset.AssetPath, MaximumPathLength) && asset.OccurrenceCount > 0)
                && entry.Types.All(type => type != null && IsBoundedRequiredText(type.TypeName, MaximumPathLength) && type.OccurrenceCount > 0 && type.AssetCount > 0);
        }

        /// <summary>終了状態と失敗理由が、実際の生成経路で組み合わせ可能かを確認します。</summary>
        private static bool ValidateStatusAndError(BuildAssistantHistoryStatus status, BuildAssistantError error)
        {
            switch (status)
            {
                case BuildAssistantHistoryStatus.Succeeded:
                    return error == BuildAssistantError.None || error == BuildAssistantError.ReportReadFailed;
                case BuildAssistantHistoryStatus.Failed:
                    return error == BuildAssistantError.BuildReportUnavailable
                        || error == BuildAssistantError.ReportReadFailed
                        || error == BuildAssistantError.BuildInvocationFailed
                        || error == BuildAssistantError.StalePlan
                        || error == BuildAssistantError.OutputReservationFailed
                        || error == BuildAssistantError.UnsafeOutputPath
                        || error == BuildAssistantError.OutputAlreadyExists;
                case BuildAssistantHistoryStatus.Interrupted:
                    return error == BuildAssistantError.BuildInvocationFailed;
                default:
                    return false;
            }
        }

        /// <summary>実行中と終了済みの印が、履歴項目の状態と矛盾していないかを確認します。</summary>
        private static bool ValidateRunState(RunState state)
        {
            if (state == null || !ValidateEntry(state.Entry))
                return false;
            if (state.Completed)
                return true;
            return state.Entry.Status == BuildAssistantHistoryStatus.Interrupted
                && state.Entry.Error == BuildAssistantError.BuildInvocationFailed
                && state.Entry.CompletedAtUtc == state.Entry.StartedAtUtc;
        }

        /// <summary>履歴のビルド設定が、本モジュールで実行できるデスクトップ通常プレイヤーの組み合わせか検査します。</summary>
        private static bool ValidateBuildSettings(BuildAssistantHistoryEntry entry)
        {
            var desktopTarget = entry.Target == BuildTarget.StandaloneWindows64 || entry.Target == BuildTarget.StandaloneOSX || entry.Target == BuildTarget.StandaloneLinux64;
            if (!desktopTarget || entry.TargetGroup != BuildTargetGroup.Standalone || !StringComparer.Ordinal.Equals(entry.NamedBuildTarget, "Standalone"))
                return false;
            if (entry.Subtarget != (int)StandaloneBuildSubtarget.Default && entry.Subtarget != (int)StandaloneBuildSubtarget.Player)
                return false;
            if (entry.ScriptingBackend != ScriptingImplementation.Mono2x && entry.ScriptingBackend != ScriptingImplementation.IL2CPP)
                return false;
            if ((entry.Options & BuildOptions.DetailedBuildReport) == BuildOptions.None || (entry.Options & ~SupportedBuildOptions) != BuildOptions.None)
                return false;
            if ((entry.Options & BuildOptions.CompressWithLz4) != BuildOptions.None && (entry.Options & BuildOptions.CompressWithLz4HC) != BuildOptions.None)
                return false;
            if ((entry.Options & BuildOptions.Development) == BuildOptions.None && (entry.Options & DevelopmentOnlyBuildOptions) != BuildOptions.None)
                return false;
            return entry.ScriptingBackend == ScriptingImplementation.Mono2x || (entry.Options & BuildOptions.EnableCodeCoverage) == BuildOptions.None;
        }

        internal static bool AreNestedCountsSupported(int defineCount, int sceneCount, int assetCount, int typeCount)
        {
            return defineCount >= 0 && defineCount <= MaximumDefineCount
                && sceneCount >= 0 && sceneCount <= MaximumSceneCount
                && assetCount >= 0 && assetCount <= MaximumAssetCount
                && typeCount >= 0 && typeCount <= MaximumTypeCount;
        }

        private static bool IsBoundedRequiredText(string value, int maximumLength) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;
        private static bool IsBoundedText(string value, int maximumLength) => value != null && value.Length <= maximumLength;

        private static void EnsureDocumentSize(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaximumDocumentBytes)
                throw new InvalidDataException("履歴ファイルが安全に扱える最大容量を超えています。");
        }

        private static EntryData ToData(BuildAssistantHistoryEntry entry)
        {
            return new EntryData
            {
                runId = entry.RunId,
                createdAtUtc = entry.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                startedAtUtc = entry.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                completedAtUtc = entry.CompletedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                status = (int)entry.Status,
                error = (int)entry.Error,
                message = entry.Message,
                outputRoot = entry.OutputRoot,
                runDirectory = entry.RunDirectory,
                artifactPath = entry.ArtifactPath,
                profileKind = (int)entry.ProfileKind,
                profileGuid = entry.ProfileGuid,
                profileName = entry.ProfileName,
                profilePath = entry.ProfilePath,
                profileDependencyHash = entry.ProfileDependencyHash,
                profileStableId = entry.ProfileStableId,
                target = (int)entry.Target,
                targetGroup = (int)entry.TargetGroup,
                namedBuildTarget = entry.NamedBuildTarget,
                subtarget = entry.Subtarget,
                scriptingBackend = (int)entry.ScriptingBackend,
                options = (int)entry.Options,
                effectiveDefines = entry.EffectiveDefines.ToArray(),
                scenes = entry.Scenes.Select(scene => new SceneData { order = scene.Order, guid = scene.Guid, assetPath = scene.AssetPath, enabled = scene.Enabled, dependencyHash = scene.DependencyHash }).ToArray(),
                totalErrors = entry.TotalErrors,
                totalWarnings = entry.TotalWarnings,
                totalOutputBytes = entry.TotalOutputBytes.ToString(CultureInfo.InvariantCulture),
                packedContentBytes = entry.PackedContentBytes.ToString(CultureInfo.InvariantCulture),
                packedOverheadBytes = entry.PackedOverheadBytes.ToString(CultureInfo.InvariantCulture),
                assets = entry.Assets.Select(asset => new AssetData { assetPath = asset.AssetPath, packedBytes = asset.PackedBytes.ToString(CultureInfo.InvariantCulture), occurrenceCount = asset.OccurrenceCount }).ToArray(),
                types = entry.Types.Select(type => new TypeData { typeName = type.TypeName, packedBytes = type.PackedBytes.ToString(CultureInfo.InvariantCulture), occurrenceCount = type.OccurrenceCount, assetCount = type.AssetCount }).ToArray(),
                previousRunId = entry.PreviousRunId,
                totalOutputDeltaBytes = entry.TotalOutputDeltaBytes.ToString(CultureInfo.InvariantCulture),
                packedContentDeltaBytes = entry.PackedContentDeltaBytes.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static BuildAssistantHistoryEntry FromData(EntryData data)
        {
            if (data == null || data.scenes == null || data.assets == null || data.types == null || data.effectiveDefines == null || data.scenes.Any(scene => scene == null) || data.assets.Any(asset => asset == null) || data.types.Any(type => type == null))
                throw new InvalidDataException("履歴項目に必要な情報がありません。");
            var createdAtUtc = ParseUtc(data.createdAtUtc);
            var parsedStartedAtUtc = ParseUtc(data.startedAtUtc);
            var parsedCompletedAtUtc = ParseUtc(data.completedAtUtc);
            if (parsedCompletedAtUtc < parsedStartedAtUtc)
                throw new InvalidDataException("履歴項目の完了時刻が開始時刻より前です。");
            var startedAtUtc = parsedStartedAtUtc < createdAtUtc ? createdAtUtc : parsedStartedAtUtc;
            var completedAtUtc = parsedCompletedAtUtc < startedAtUtc ? startedAtUtc : parsedCompletedAtUtc;
            var scenes = data.scenes.Select(scene => new BuildAssistantScene(scene.order, scene.guid, scene.assetPath, scene.enabled, scene.dependencyHash)).ToArray();
            var assets = data.assets.Select(asset => new BuildAssistantAssetSize(asset.assetPath, ParseUnsigned(asset.packedBytes), asset.occurrenceCount)).ToArray();
            var types = data.types.Select(type => new BuildAssistantTypeSize(type.typeName, ParseUnsigned(type.packedBytes), type.occurrenceCount, type.assetCount)).ToArray();
            return new BuildAssistantHistoryEntry(data.runId, createdAtUtc, startedAtUtc, completedAtUtc, (BuildAssistantHistoryStatus)data.status, (BuildAssistantError)data.error, data.message, data.outputRoot, data.runDirectory, data.artifactPath, (BuildAssistantProfileKind)data.profileKind, data.profileGuid, data.profileName, data.profilePath, data.profileDependencyHash, data.profileStableId, (BuildTarget)data.target, (BuildTargetGroup)data.targetGroup, data.namedBuildTarget, data.subtarget, (ScriptingImplementation)data.scriptingBackend, (BuildOptions)data.options, data.effectiveDefines, scenes, data.totalErrors, data.totalWarnings, ParseUnsigned(data.totalOutputBytes), ParseUnsigned(data.packedContentBytes), ParseUnsigned(data.packedOverheadBytes), assets, types, data.previousRunId, ParseSigned(data.totalOutputDeltaBytes), ParseSigned(data.packedContentDeltaBytes));
        }

        private static DateTime ParseUtc(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
        private static ulong ParseUnsigned(string value) => ulong.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
        private static long ParseSigned(string value) => long.Parse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

        [Serializable]
        private sealed class HistoryDocument { public int schemaVersion; public EntryData[] entries; }
        [Serializable]
        private sealed class RunStateDocument { public int schemaVersion; public bool completed; public EntryData entry; }
        [Serializable]
        private sealed class ExportDocument { public int schemaVersion; public EntryData entry; }
        [Serializable]
        private sealed class EntryData
        {
            public string runId; public string createdAtUtc; public string startedAtUtc; public string completedAtUtc; public int status; public int error; public string message; public string outputRoot; public string runDirectory; public string artifactPath; public int profileKind; public string profileGuid; public string profileName; public string profilePath; public string profileDependencyHash; public string profileStableId; public int target; public int targetGroup; public string namedBuildTarget; public int subtarget; public int scriptingBackend; public int options; public string[] effectiveDefines; public SceneData[] scenes; public int totalErrors; public int totalWarnings; public string totalOutputBytes; public string packedContentBytes; public string packedOverheadBytes; public AssetData[] assets; public TypeData[] types; public string previousRunId; public string totalOutputDeltaBytes; public string packedContentDeltaBytes;
        }
        [Serializable]
        private sealed class SceneData { public int order; public string guid; public string assetPath; public bool enabled; public string dependencyHash; }
        [Serializable]
        private sealed class AssetData { public string assetPath; public string packedBytes; public int occurrenceCount; }
        [Serializable]
        private sealed class TypeData { public string typeName; public string packedBytes; public int occurrenceCount; public int assetCount; }
    }
}
