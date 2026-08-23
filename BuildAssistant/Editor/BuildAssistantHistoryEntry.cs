using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;

namespace BuildAssistant.Editor
{
    /// <summary>Stores a detached, immutable terminal record for one Build Assistant run.</summary>
    public sealed class BuildAssistantHistoryEntry
    {
        private readonly ReadOnlyCollection<string> effectiveDefines;
        private readonly ReadOnlyCollection<BuildAssistantScene> scenes;
        private readonly ReadOnlyCollection<BuildAssistantAssetSize> assets;
        private readonly ReadOnlyCollection<BuildAssistantTypeSize> types;

        internal BuildAssistantHistoryEntry(string runId, DateTime createdAtUtc, DateTime startedAtUtc, DateTime completedAtUtc, BuildAssistantHistoryStatus status, BuildAssistantError error, string message, string outputRoot, string runDirectory, string artifactPath, BuildAssistantProfileKind profileKind, string profileGuid, string profileName, string profilePath, string profileDependencyHash, string profileStableId, BuildTarget target, BuildTargetGroup targetGroup, string namedBuildTarget, int subtarget, ScriptingImplementation scriptingBackend, BuildOptions options, IEnumerable<string> effectiveDefines, IEnumerable<BuildAssistantScene> scenes, int totalErrors, int totalWarnings, ulong totalOutputBytes, ulong packedContentBytes, ulong packedOverheadBytes, IEnumerable<BuildAssistantAssetSize> assets, IEnumerable<BuildAssistantTypeSize> types, string previousRunId, long totalOutputDeltaBytes, long packedContentDeltaBytes)
        {
            RunId = runId ?? string.Empty;
            CreatedAtUtc = createdAtUtc;
            StartedAtUtc = startedAtUtc;
            CompletedAtUtc = completedAtUtc;
            Status = status;
            Error = error;
            Message = message ?? string.Empty;
            OutputRoot = outputRoot ?? string.Empty;
            RunDirectory = runDirectory ?? string.Empty;
            ArtifactPath = artifactPath ?? string.Empty;
            ProfileKind = profileKind;
            ProfileGuid = profileGuid ?? string.Empty;
            ProfileName = profileName ?? string.Empty;
            ProfilePath = profilePath ?? string.Empty;
            ProfileDependencyHash = profileDependencyHash ?? string.Empty;
            ProfileStableId = profileStableId ?? string.Empty;
            Target = target;
            TargetGroup = targetGroup;
            NamedBuildTarget = namedBuildTarget ?? string.Empty;
            Subtarget = subtarget;
            ScriptingBackend = scriptingBackend;
            Options = options;
            this.effectiveDefines = Array.AsReadOnly((effectiveDefines ?? Enumerable.Empty<string>()).Select(value => value ?? string.Empty).ToArray());
            this.scenes = Array.AsReadOnly((scenes ?? Enumerable.Empty<BuildAssistantScene>()).ToArray());
            TotalErrors = totalErrors;
            TotalWarnings = totalWarnings;
            TotalOutputBytes = totalOutputBytes;
            PackedContentBytes = packedContentBytes;
            PackedOverheadBytes = packedOverheadBytes;
            this.assets = Array.AsReadOnly((assets ?? Enumerable.Empty<BuildAssistantAssetSize>()).ToArray());
            this.types = Array.AsReadOnly((types ?? Enumerable.Empty<BuildAssistantTypeSize>()).ToArray());
            PreviousRunId = previousRunId ?? string.Empty;
            TotalOutputDeltaBytes = totalOutputDeltaBytes;
            PackedContentDeltaBytes = packedContentDeltaBytes;
        }

        /// <summary>Gets the run identifier.</summary>
        public string RunId { get; }

        /// <summary>Gets the UTC preview time.</summary>
        public DateTime CreatedAtUtc { get; }

        /// <summary>Gets the UTC build invocation start time.</summary>
        public DateTime StartedAtUtc { get; }

        /// <summary>Gets the UTC terminal time recorded by Build Assistant.</summary>
        public DateTime CompletedAtUtc { get; }

        /// <summary>Gets the non-negative elapsed duration represented by the stored UTC timestamps.</summary>
        public TimeSpan Duration => CompletedAtUtc >= StartedAtUtc ? CompletedAtUtc - StartedAtUtc : TimeSpan.Zero;

        /// <summary>Gets the terminal run status.</summary>
        public BuildAssistantHistoryStatus Status { get; }

        /// <summary>Gets the bounded terminal error.</summary>
        public BuildAssistantError Error { get; }

        /// <summary>Gets the detached terminal diagnostic.</summary>
        public string Message { get; }

        /// <summary>Gets the output root used by the run.</summary>
        public string OutputRoot { get; }

        /// <summary>Gets the exclusive run directory.</summary>
        public string RunDirectory { get; }

        /// <summary>Gets the platform-specific player artifact path.</summary>
        public string ArtifactPath { get; }

        /// <summary>Gets the profile kind used by the run.</summary>
        public BuildAssistantProfileKind ProfileKind { get; }

        /// <summary>Gets the custom profile GUID, or an empty string for a platform profile.</summary>
        public string ProfileGuid { get; }

        /// <summary>Gets the profile display name.</summary>
        public string ProfileName { get; }

        /// <summary>Gets the custom profile asset path, or an empty string for a platform profile.</summary>
        public string ProfilePath { get; }

        /// <summary>Gets the effective fingerprint for settings, classic profiles, imported content, package manifests, StreamingAssets, and any custom profile dependency.</summary>
        public string ProfileDependencyHash { get; }

        /// <summary>Gets the stable profile identity used by comparison matching.</summary>
        public string ProfileStableId { get; }

        /// <summary>Gets the desktop standalone target.</summary>
        public BuildTarget Target { get; }

        /// <summary>Gets the target group.</summary>
        public BuildTargetGroup TargetGroup { get; }

        /// <summary>Gets the NamedBuildTarget name.</summary>
        public string NamedBuildTarget { get; }

        /// <summary>Gets the standalone subtarget value.</summary>
        public int Subtarget { get; }

        /// <summary>Gets the scripting backend.</summary>
        public ScriptingImplementation ScriptingBackend { get; }

        /// <summary>Gets the effective normalized build options recorded for comparison.</summary>
        public BuildOptions Options { get; }

        /// <summary>Gets a defensive read-only copy of the effective scripting defines.</summary>
        public IReadOnlyList<string> EffectiveDefines => effectiveDefines;

        /// <summary>Gets a defensive read-only copy of the ordered scene snapshot.</summary>
        public IReadOnlyList<BuildAssistantScene> Scenes => scenes;

        /// <summary>Gets the build-report error count.</summary>
        public int TotalErrors { get; }

        /// <summary>Gets the build-report warning count.</summary>
        public int TotalWarnings { get; }

        /// <summary>Gets Unity's total output size, kept separate from packed content and overhead.</summary>
        public ulong TotalOutputBytes { get; }

        /// <summary>Gets the checked sum of all packed asset occurrences.</summary>
        public ulong PackedContentBytes { get; }

        /// <summary>Gets the checked sum of all packed-file overhead values.</summary>
        public ulong PackedOverheadBytes { get; }

        /// <summary>Gets packed asset rows ordered by bytes descending and asset key ordinal ascending.</summary>
        public IReadOnlyList<BuildAssistantAssetSize> Assets => assets;

        /// <summary>Gets packed type rows ordered by bytes descending and type key ordinal ascending.</summary>
        public IReadOnlyList<BuildAssistantTypeSize> Types => types;

        /// <summary>Gets the previous compatible successful run identifier, or an empty string.</summary>
        public string PreviousRunId { get; }

        /// <summary>Gets total output bytes minus the previous comparable run's total output bytes.</summary>
        public long TotalOutputDeltaBytes { get; }

        /// <summary>Gets packed content bytes minus the previous comparable run's packed content bytes.</summary>
        public long PackedContentDeltaBytes { get; }
    }
}
