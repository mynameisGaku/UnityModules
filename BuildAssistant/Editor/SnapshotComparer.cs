using System;
using System.Collections.Generic;

namespace BuildAssistant.Editor
{
    internal static class SnapshotComparer
    {
        internal static bool AreEquivalent(BuildAssistantPlan plan, EnvironmentSnapshot current, out string difference)
        {
            if (plan == null || current == null)
                throw new ArgumentNullException(plan == null ? nameof(plan) : nameof(current));

            var profile = current.Profile;
            if (plan.ProfileKind != profile.Kind || !Equal(plan.ProfileGuid, profile.Guid) || !Equal(plan.ProfileName, profile.Name) || !Equal(plan.ProfilePath, profile.AssetPath) || !Equal(plan.ProfileDependencyHash, profile.DependencyHash) || !Equal(plan.ProfileStableId, profile.StableId))
                return Different("The active build profile changed.", out difference);
            if (plan.Target != current.Target || plan.TargetGroup != current.TargetGroup || !Equal(plan.NamedBuildTarget, current.NamedBuildTarget) || plan.Subtarget != current.Subtarget)
                return Different("The active build target or subtarget changed.", out difference);
            if (plan.ScriptingBackend != current.ScriptingBackend)
                return Different("The scripting backend changed.", out difference);
            if (plan.Options != current.Options || plan.InvocationOptions != current.InvocationOptions || !Equal(plan.AssetBundleManifestPath, current.AssetBundleManifestPath))
                return Different("The normalized build options changed.", out difference);
            if (!SequenceEqual(plan.ExtraScriptingDefines, current.ExtraScriptingDefines) || !SequenceEqual(plan.EffectiveDefines, current.EffectiveDefines))
                return Different("The effective scripting defines changed.", out difference);
            if (!ScenesEqual(plan.Scenes, current.Scenes))
                return Different("The ordered scene list or a scene dependency changed.", out difference);

            difference = string.Empty;
            return true;
        }

        private static bool ScenesEqual(IReadOnlyList<BuildAssistantScene> left, IReadOnlyList<BuildAssistantScene> right)
        {
            if (left.Count != right.Count)
                return false;
            for (var index = 0; index < left.Count; index++)
            {
                var first = left[index];
                var second = right[index];
                if (first.Order != second.Order || first.Enabled != second.Enabled || !Equal(first.Guid, second.Guid) || !Equal(first.AssetPath, second.AssetPath) || !Equal(first.DependencyHash, second.DependencyHash))
                    return false;
            }

            return true;
        }

        private static bool SequenceEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left.Count != right.Count)
                return false;
            for (var index = 0; index < left.Count; index++)
            {
                if (!Equal(left[index], right[index]))
                    return false;
            }

            return true;
        }

        private static bool Equal(string left, string right) => StringComparer.Ordinal.Equals(left ?? string.Empty, right ?? string.Empty);

        private static bool Different(string value, out string difference)
        {
            difference = value;
            return false;
        }
    }
}
