// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace ModuleInstaller.Editor
{
    internal enum ModuleInstallIssueKind
    {
        UnknownPackage = 0,
        AssetCopyConflict = 1
    }

    internal readonly struct ModuleInstallIssue
    {
        internal ModuleInstallIssue(ModuleInstallIssueKind kind, string value, string message)
        {
            Kind = kind;
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        internal ModuleInstallIssueKind Kind { get; }
        internal string Value { get; }
        internal string Message { get; }
    }

    internal sealed class ModuleInstallPlan
    {
        internal ModuleInstallPlan(
            IReadOnlyList<ModuleCatalogEntry> entries,
            IReadOnlyList<ModuleInstallIssue> issues,
            int installedCount)
        {
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
            Issues = issues ?? throw new ArgumentNullException(nameof(issues));
            InstalledCount = installedCount;
        }

        internal IReadOnlyList<ModuleCatalogEntry> Entries { get; }
        internal IReadOnlyList<ModuleInstallIssue> Issues { get; }
        internal int InstalledCount { get; }
        internal bool CanStart => Entries.Count > 0 && Issues.Count == 0;
    }
}
