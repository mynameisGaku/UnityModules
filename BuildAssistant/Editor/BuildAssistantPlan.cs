using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;

namespace BuildAssistant.Editor
{
    /// <summary>Describes one immutable, stale-checkable desktop standalone build.</summary>
    public sealed class BuildAssistantPlan
    {
        private readonly ReadOnlyCollection<string> extraScriptingDefines;
        private readonly ReadOnlyCollection<string> effectiveDefines;
        private readonly ReadOnlyCollection<BuildAssistantScene> scenes;

        internal BuildAssistantPlan(BuildAssistantError error, string message, string runId, DateTime createdAtUtc, string outputRoot, string runDirectory, string artifactPath, OutputRootMode outputRootMode, ProfileSnapshot profile, BuildTarget target, BuildTargetGroup targetGroup, string namedBuildTarget, int subtarget, ScriptingImplementation scriptingBackend, BuildOptions options, BuildOptions invocationOptions, string assetBundleManifestPath, IEnumerable<string> extraScriptingDefines, IEnumerable<string> effectiveDefines, IEnumerable<BuildAssistantScene> scenes, BuildAssistantHistoryEntry previousComparableSuccess)
        {
            Error = error;
            Message = message ?? string.Empty;
            RunId = runId ?? string.Empty;
            CreatedAtUtc = createdAtUtc;
            OutputRoot = outputRoot ?? string.Empty;
            RunDirectory = runDirectory ?? string.Empty;
            ArtifactPath = artifactPath ?? string.Empty;
            OutputRootMode = outputRootMode;
            ProfileKind = profile?.Kind ?? BuildAssistantProfileKind.Platform;
            ProfileGuid = profile?.Guid ?? string.Empty;
            ProfileName = profile?.Name ?? string.Empty;
            ProfilePath = profile?.AssetPath ?? string.Empty;
            ProfileDependencyHash = profile?.DependencyHash ?? string.Empty;
            ProfileStableId = profile?.StableId ?? string.Empty;
            Target = target;
            TargetGroup = targetGroup;
            NamedBuildTarget = namedBuildTarget ?? string.Empty;
            Subtarget = subtarget;
            ScriptingBackend = scriptingBackend;
            Options = options;
            InvocationOptions = invocationOptions;
            AssetBundleManifestPath = assetBundleManifestPath ?? string.Empty;
            this.extraScriptingDefines = Array.AsReadOnly((extraScriptingDefines ?? Enumerable.Empty<string>()).Select(value => value ?? string.Empty).ToArray());
            this.effectiveDefines = Array.AsReadOnly((effectiveDefines ?? Enumerable.Empty<string>()).Select(value => value ?? string.Empty).ToArray());
            this.scenes = Array.AsReadOnly((scenes ?? Enumerable.Empty<BuildAssistantScene>()).ToArray());
            PreviousComparableSuccess = previousComparableSuccess;
        }

        /// <summary>Gets the preview error, or None when the plan is ready.</summary>
        public BuildAssistantError Error { get; }

        /// <summary>Gets a diagnostic message suitable for an editor UI.</summary>
        public string Message { get; }

        /// <summary>Gets whether the plan passed preview validation.</summary>
        public bool IsReady => Error == BuildAssistantError.None;

        /// <summary>Gets the stable run identifier generated during preview.</summary>
        public string RunId { get; }

        /// <summary>Gets the UTC time supplied to deterministic plan creation.</summary>
        public DateTime CreatedAtUtc { get; }

        /// <summary>Gets the normalized absolute output root.</summary>
        public string OutputRoot { get; }

        /// <summary>Gets the normalized absolute directory reserved exclusively for this run.</summary>
        public string RunDirectory { get; }

        /// <summary>Gets the platform-specific player artifact path.</summary>
        public string ArtifactPath { get; }

        /// <summary>Gets whether the plan uses the platform profile or a custom BuildProfile asset.</summary>
        public BuildAssistantProfileKind ProfileKind { get; }

        /// <summary>Gets the custom BuildProfile asset GUID, or an empty string for the platform profile.</summary>
        public string ProfileGuid { get; }

        /// <summary>Gets the captured profile display name.</summary>
        public string ProfileName { get; }

        /// <summary>Gets the custom BuildProfile asset path, or an empty string for the platform profile.</summary>
        public string ProfilePath { get; }

        /// <summary>Gets the effective fingerprint for settings, classic profiles, imported content, package manifests, StreamingAssets, and any custom profile dependency.</summary>
        public string ProfileDependencyHash { get; }

        /// <summary>Gets the stable profile identity used for compatible history comparisons.</summary>
        public string ProfileStableId { get; }

        /// <summary>Gets the captured desktop standalone build target.</summary>
        public BuildTarget Target { get; }

        /// <summary>Gets the captured build target group.</summary>
        public BuildTargetGroup TargetGroup { get; }

        /// <summary>Gets the captured NamedBuildTarget name.</summary>
        public string NamedBuildTarget { get; }

        /// <summary>Gets the captured standalone subtarget value.</summary>
        public int Subtarget { get; }

        /// <summary>Gets the captured scripting backend.</summary>
        public ScriptingImplementation ScriptingBackend { get; }

        /// <summary>Gets effective normalized BuildOptions, including custom-profile build mode and compression flags.</summary>
        public BuildOptions Options { get; }

        /// <summary>Gets the optional captured AssetBundle manifest path.</summary>
        public string AssetBundleManifestPath { get; }

        /// <summary>Gets a defensive read-only copy of additional build-only scripting defines.</summary>
        public IReadOnlyList<string> ExtraScriptingDefines => extraScriptingDefines;

        /// <summary>Gets a defensive read-only copy of the effective global, profile, and build-only defines.</summary>
        public IReadOnlyList<string> EffectiveDefines => effectiveDefines;

        /// <summary>Gets a defensive read-only copy of every ordered scene snapshot, including disabled scenes.</summary>
        public IReadOnlyList<BuildAssistantScene> Scenes => scenes;

        /// <summary>Gets the latest successful compatible history entry captured at preview time, when available.</summary>
        public BuildAssistantHistoryEntry PreviousComparableSuccess { get; }

        internal OutputRootMode OutputRootMode { get; }
        internal BuildOptions InvocationOptions { get; }
    }
}
