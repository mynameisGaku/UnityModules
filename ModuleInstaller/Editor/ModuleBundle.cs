// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace ModuleInstaller.Editor
{
    internal sealed class ModuleBundle
    {
        internal ModuleBundle(string id, string displayName, string summary, params string[] packageNames)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            PackageNames = packageNames ?? throw new ArgumentNullException(nameof(packageNames));
        }

        internal string Id { get; }
        internal string DisplayName { get; }
        internal string Summary { get; }
        internal IReadOnlyList<string> PackageNames { get; }
    }
}
