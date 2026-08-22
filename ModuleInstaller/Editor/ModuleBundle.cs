// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace ModuleInstaller.Editor
{
    internal enum ModuleBundleTier
    {
        Recommended,
        Specialized
    }

    internal sealed class ModuleBundle
    {
        internal ModuleBundle(
            string id,
            string displayName,
            string summary,
            ModuleBundleTier tier,
            string useWhen,
            string firstStep,
            string changeScope,
            params string[] packageNames)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            Tier = tier;
            UseWhen = useWhen ?? throw new ArgumentNullException(nameof(useWhen));
            FirstStep = firstStep ?? throw new ArgumentNullException(nameof(firstStep));
            ChangeScope = changeScope ?? throw new ArgumentNullException(nameof(changeScope));
            PackageNames = packageNames ?? throw new ArgumentNullException(nameof(packageNames));
        }

        internal string Id { get; }
        internal string DisplayName { get; }
        internal string Summary { get; }
        internal ModuleBundleTier Tier { get; }
        internal string UseWhen { get; }
        internal string FirstStep { get; }
        internal string ChangeScope { get; }
        internal IReadOnlyList<string> PackageNames { get; }
    }
}
