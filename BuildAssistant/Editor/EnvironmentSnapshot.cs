using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;

namespace BuildAssistant.Editor
{
    internal sealed class EnvironmentSnapshot
    {
        private readonly ReadOnlyCollection<string> extraScriptingDefines;
        private readonly ReadOnlyCollection<string> effectiveDefines;
        private readonly ReadOnlyCollection<BuildAssistantScene> scenes;

        internal EnvironmentSnapshot(ProfileSnapshot profile, BuildTarget target, BuildTargetGroup targetGroup, string namedBuildTarget, int subtarget, ScriptingImplementation scriptingBackend, BuildOptions options, string assetBundleManifestPath, IEnumerable<string> extraScriptingDefines, IEnumerable<string> effectiveDefines, IEnumerable<BuildAssistantScene> scenes, BuildOptions? invocationOptions = null)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Target = target;
            TargetGroup = targetGroup;
            NamedBuildTarget = namedBuildTarget ?? string.Empty;
            Subtarget = subtarget;
            ScriptingBackend = scriptingBackend;
            Options = options;
            InvocationOptions = invocationOptions ?? options;
            AssetBundleManifestPath = assetBundleManifestPath ?? string.Empty;
            this.extraScriptingDefines = Array.AsReadOnly((extraScriptingDefines ?? Enumerable.Empty<string>()).Select(value => value ?? string.Empty).ToArray());
            this.effectiveDefines = Array.AsReadOnly((effectiveDefines ?? Enumerable.Empty<string>()).Select(value => value ?? string.Empty).ToArray());
            this.scenes = Array.AsReadOnly((scenes ?? Enumerable.Empty<BuildAssistantScene>()).ToArray());
        }

        internal ProfileSnapshot Profile { get; }
        internal BuildTarget Target { get; }
        internal BuildTargetGroup TargetGroup { get; }
        internal string NamedBuildTarget { get; }
        internal int Subtarget { get; }
        internal ScriptingImplementation ScriptingBackend { get; }
        internal BuildOptions Options { get; }
        internal BuildOptions InvocationOptions { get; }
        internal string AssetBundleManifestPath { get; }
        internal IReadOnlyList<string> ExtraScriptingDefines => extraScriptingDefines;
        internal IReadOnlyList<string> EffectiveDefines => effectiveDefines;
        internal IReadOnlyList<BuildAssistantScene> Scenes => scenes;
    }
}
