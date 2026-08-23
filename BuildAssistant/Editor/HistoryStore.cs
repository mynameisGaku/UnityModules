using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using UnityEditor;
using UnityEngine;

namespace BuildAssistant.Editor
{
    internal sealed class HistoryStore
    {
        internal const int MaximumEntryCount = 20;
        private const int SchemaVersion = 1;
        private readonly BuildAssistantFileSystem fileSystem;
        private readonly string directoryPath;
        private readonly string historyPath;
        private readonly string historyBackupPath;
        private readonly string runStatePath;
        private readonly string runStateBackupPath;

        internal HistoryStore(string projectRoot, BuildAssistantFileSystem fileSystem = null)
        {
            if (string.IsNullOrWhiteSpace(projectRoot) || !Path.IsPathRooted(projectRoot))
                throw new ArgumentException("An absolute project root is required.", nameof(projectRoot));
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
                return new BuildAssistantHistory(backup, true, "The backup history was loaded because the primary history was missing or invalid.");
            return new BuildAssistantHistory(Array.Empty<BuildAssistantHistoryEntry>(), false, fileSystem.FileExists(historyPath) || fileSystem.FileExists(historyBackupPath) ? "No valid history document could be loaded." : string.Empty);
        }

        internal void Save(IEnumerable<BuildAssistantHistoryEntry> entries)
        {
            var normalized = NormalizeEntries(entries);
            var document = new HistoryDocument { schemaVersion = SchemaVersion, entries = normalized.Select(ToData).ToArray() };
            var preserveValidBackup = fileSystem.FileExists(historyPath) && !TryLoadHistory(historyPath, out _) && TryLoadHistory(historyBackupPath, out _);
            WriteAtomic(historyPath, historyBackupPath, JsonUtility.ToJson(document, true) + Environment.NewLine, preserveValidBackup);
        }

        internal void SaveRunState(RunState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            var document = new RunStateDocument { schemaVersion = SchemaVersion, completed = state.Completed, entry = ToData(state.Entry) };
            var preserveValidBackup = fileSystem.FileExists(runStatePath) && !TryLoadRunState(runStatePath, out _) && TryLoadRunState(runStateBackupPath, out _);
            WriteAtomic(runStatePath, runStateBackupPath, JsonUtility.ToJson(document, true) + Environment.NewLine, preserveValidBackup);
        }

        internal bool HasRunState => fileSystem.FileExists(runStatePath) || fileSystem.FileExists(runStateBackupPath);

        internal BuildAssistantHistory RecoverInterrupted(DateTime completedAtUtc)
        {
            var state = LoadRunState();
            if (state == null)
                return Load();

            var history = Load();
            var persistedTerminal = history.Entries.FirstOrDefault(entry => StringComparer.Ordinal.Equals(entry.RunId, state.Entry.RunId) && entry.Status != BuildAssistantHistoryStatus.Interrupted);
            var terminal = state.Completed ? state.Entry : persistedTerminal ?? state.AsInterrupted(completedAtUtc).Entry;
            var entries = history.Entries.Where(entry => !StringComparer.Ordinal.Equals(entry.RunId, terminal.RunId)).Concat(new[] { terminal });
            Save(entries);
            var message = state.Completed ? "A completed run-state record was recovered into history." : persistedTerminal != null ? "The persisted terminal history result was kept while a stale running state was removed." : "An interrupted run was recorded without restarting it.";
            try
            {
                DeleteRunState();
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                message += " Run-state cleanup will be retried: " + exception.Message;
            }

            return new BuildAssistantHistory(NormalizeEntries(entries), history.RecoveredFromBackup, message);
        }

        internal void DeleteRunState()
        {
            fileSystem.DeleteFile(runStateBackupPath);
            fileSystem.DeleteFile(runStatePath);
        }

        internal static string SerializeExport(BuildAssistantHistoryEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            var document = new ExportDocument { schemaVersion = SchemaVersion, entry = ToData(entry) };
            return JsonUtility.ToJson(document, true) + Environment.NewLine;
        }

        private RunState LoadRunState()
        {
            if (TryLoadRunState(runStatePath, out var primary))
                return primary;
            if (TryLoadRunState(runStateBackupPath, out var backup))
                return backup;
            if (HasRunState)
                throw new InvalidDataException("No valid run-state document could be loaded.");
            return null;
        }

        private bool TryLoadHistory(string path, out BuildAssistantHistoryEntry[] entries)
        {
            entries = null;
            if (!fileSystem.FileExists(path))
                return false;
            try
            {
                var document = JsonUtility.FromJson<HistoryDocument>(fileSystem.ReadAllText(path));
                if (document == null || document.schemaVersion != SchemaVersion || document.entries == null || document.entries.Length > MaximumEntryCount)
                    return false;
                entries = document.entries.Select(FromData).ToArray();
                return entries.All(ValidateEntry);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is InvalidDataException || exception is FormatException || exception is OverflowException || exception is IOException || exception is UnauthorizedAccessException || exception is NullReferenceException)
            {
                return false;
            }
        }

        private bool TryLoadRunState(string path, out RunState state)
        {
            state = null;
            if (!fileSystem.FileExists(path))
                return false;
            try
            {
                var document = JsonUtility.FromJson<RunStateDocument>(fileSystem.ReadAllText(path));
                if (document == null || document.schemaVersion != SchemaVersion || document.entry == null)
                    return false;
                var entry = FromData(document.entry);
                if (!ValidateEntry(entry))
                    return false;
                state = new RunState(document.completed, entry);
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is InvalidDataException || exception is FormatException || exception is OverflowException || exception is IOException || exception is UnauthorizedAccessException || exception is NullReferenceException)
            {
                return false;
            }
        }

        private void WriteAtomic(string path, string backupPath, string json, bool preserveValidBackup)
        {
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

        private static BuildAssistantHistoryEntry[] NormalizeEntries(IEnumerable<BuildAssistantHistoryEntry> entries)
        {
            return (entries ?? Enumerable.Empty<BuildAssistantHistoryEntry>()).Where(entry => entry != null).GroupBy(entry => entry.RunId, StringComparer.Ordinal).Select(group => group.OrderByDescending(entry => entry.CompletedAtUtc).First()).OrderByDescending(entry => entry.CompletedAtUtc).ThenByDescending(entry => entry.RunId, StringComparer.Ordinal).Take(MaximumEntryCount).ToArray();
        }

        private static bool ValidateEntry(BuildAssistantHistoryEntry entry)
        {
            return entry != null && !string.IsNullOrEmpty(entry.RunId) && entry.TotalErrors >= 0 && entry.TotalWarnings >= 0 && Enum.IsDefined(typeof(BuildAssistantHistoryStatus), entry.Status) && Enum.IsDefined(typeof(BuildAssistantError), entry.Error) && Enum.IsDefined(typeof(BuildAssistantProfileKind), entry.ProfileKind) && entry.CompletedAtUtc >= entry.StartedAtUtc && entry.Scenes.All(scene => scene != null) && entry.Assets.All(asset => asset != null) && entry.Types.All(type => type != null);
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
                throw new InvalidDataException("A history entry is incomplete.");
            var scenes = data.scenes.Select(scene => new BuildAssistantScene(scene.order, scene.guid, scene.assetPath, scene.enabled, scene.dependencyHash)).ToArray();
            var assets = data.assets.Select(asset => new BuildAssistantAssetSize(asset.assetPath, ParseUnsigned(asset.packedBytes), asset.occurrenceCount)).ToArray();
            var types = data.types.Select(type => new BuildAssistantTypeSize(type.typeName, ParseUnsigned(type.packedBytes), type.occurrenceCount, type.assetCount)).ToArray();
            return new BuildAssistantHistoryEntry(data.runId, ParseUtc(data.createdAtUtc), ParseUtc(data.startedAtUtc), ParseUtc(data.completedAtUtc), (BuildAssistantHistoryStatus)data.status, (BuildAssistantError)data.error, data.message, data.outputRoot, data.runDirectory, data.artifactPath, (BuildAssistantProfileKind)data.profileKind, data.profileGuid, data.profileName, data.profilePath, data.profileDependencyHash, data.profileStableId, (BuildTarget)data.target, (BuildTargetGroup)data.targetGroup, data.namedBuildTarget, data.subtarget, (ScriptingImplementation)data.scriptingBackend, (BuildOptions)data.options, data.effectiveDefines, scenes, data.totalErrors, data.totalWarnings, ParseUnsigned(data.totalOutputBytes), ParseUnsigned(data.packedContentBytes), ParseUnsigned(data.packedOverheadBytes), assets, types, data.previousRunId, ParseSigned(data.totalOutputDeltaBytes), ParseSigned(data.packedContentDeltaBytes));
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
