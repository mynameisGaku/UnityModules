// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace ModuleInstaller.Editor
{
    internal static class ModuleInstallPlanner
    {
        internal static ModuleInstallPlan Build(
            IEnumerable<string> packageNames,
            ISet<string> installedPackageNames,
            ISet<string> assetModuleFolders)
        {
            if (packageNames == null)
            {
                throw new ArgumentNullException(nameof(packageNames));
            }

            installedPackageNames ??= new HashSet<string>(StringComparer.Ordinal);
            assetModuleFolders ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<ModuleCatalogEntry>();
            var issues = new List<ModuleInstallIssue>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var installedCount = 0;

            foreach (var packageName in packageNames)
            {
                if (string.IsNullOrEmpty(packageName) || !seen.Add(packageName))
                {
                    continue;
                }

                if (!ModuleCatalog.TryFindEntry(packageName, out var entry))
                {
                    issues.Add(new ModuleInstallIssue(
                        ModuleInstallIssueKind.UnknownPackage,
                        packageName,
                        $"Unknown package: {packageName}"));
                    continue;
                }

                if (installedPackageNames.Contains(entry.PackageName))
                {
                    installedCount++;
                    continue;
                }

                if (assetModuleFolders.Contains(entry.FolderName))
                {
                    issues.Add(new ModuleInstallIssue(
                        ModuleInstallIssueKind.AssetCopyConflict,
                        entry.FolderName,
                        $"Assets/Modules/{entry.FolderName} already exists. Remove that copy before installing the UPM package."));
                    continue;
                }

                entries.Add(entry);
            }

            return new ModuleInstallPlan(entries, issues, installedCount);
        }

        internal static ModuleInstallPlan BuildUpdates(
            IEnumerable<string> packageNames,
            IReadOnlyDictionary<string, string> installedPackageVersions)
        {
            if (packageNames == null)
            {
                throw new ArgumentNullException(nameof(packageNames));
            }

            installedPackageVersions ??= new Dictionary<string, string>(StringComparer.Ordinal);
            var entries = new List<ModuleCatalogEntry>();
            var issues = new List<ModuleInstallIssue>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var currentCount = 0;

            foreach (var packageName in packageNames)
            {
                if (string.IsNullOrEmpty(packageName) || !seen.Add(packageName))
                {
                    continue;
                }

                if (!ModuleCatalog.TryFindEntry(packageName, out var entry))
                {
                    issues.Add(new ModuleInstallIssue(
                        ModuleInstallIssueKind.UnknownPackage,
                        packageName,
                        $"Unknown package: {packageName}"));
                    continue;
                }

                if (!installedPackageVersions.TryGetValue(entry.PackageName, out var installedVersion))
                {
                    continue;
                }

                if (!IsUpdateRequired(installedVersion, entry.Version))
                {
                    currentCount++;
                    continue;
                }

                entries.Add(entry);
            }

            return new ModuleInstallPlan(entries, issues, currentCount);
        }

        internal static bool IsUpdateRequired(string installedVersion, string targetVersion)
        {
            return Version.TryParse(installedVersion, out var installed)
                && Version.TryParse(targetVersion, out var target)
                && installed.CompareTo(target) < 0;
        }
    }
}
