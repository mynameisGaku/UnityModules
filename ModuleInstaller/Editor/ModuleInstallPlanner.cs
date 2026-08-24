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

                if (TryCreateLegacyConflict(entry, installedPackageNames, assetModuleFolders, out var legacyConflict))
                {
                    issues.Add(legacyConflict);
                    continue;
                }

                if (assetModuleFolders.Contains(entry.FolderName))
                {
                    issues.Add(CreateAssetCopyConflict(entry.FolderName));
                    continue;
                }

                if (installedPackageNames.Contains(entry.PackageName))
                {
                    installedCount++;
                    continue;
                }

                entries.Add(entry);
            }

            return new ModuleInstallPlan(entries, issues, installedCount);
        }

        internal static ModuleInstallPlan BuildUpdates(
            IEnumerable<string> packageNames,
            IReadOnlyDictionary<string, string> installedPackageVersions,
            ISet<string> assetModuleFolders)
        {
            if (packageNames == null)
            {
                throw new ArgumentNullException(nameof(packageNames));
            }

            installedPackageVersions ??= new Dictionary<string, string>(StringComparer.Ordinal);
            assetModuleFolders ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var installedPackageNames = new HashSet<string>(installedPackageVersions.Keys, StringComparer.Ordinal);
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

                if (TryCreateLegacyConflict(entry, installedPackageNames, assetModuleFolders, out var legacyConflict))
                {
                    issues.Add(legacyConflict);
                    continue;
                }

                if (assetModuleFolders.Contains(entry.FolderName))
                {
                    issues.Add(CreateAssetCopyConflict(entry.FolderName));
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

        private static ModuleInstallIssue CreateAssetCopyConflict(string folderName)
        {
            return new ModuleInstallIssue(
                ModuleInstallIssueKind.AssetCopyConflict,
                folderName,
                $"Assets/Modules/{folderName} already exists. Remove that copy before installing or updating the UPM package.");
        }

        private static bool TryCreateLegacyConflict(
            ModuleCatalogEntry entry,
            ISet<string> installedPackageNames,
            ISet<string> assetModuleFolders,
            out ModuleInstallIssue issue)
        {
            var conflicts = new List<string>();
            for (var index = 0; index < entry.LegacyPackageNames.Count; index++)
            {
                var legacyPackageName = entry.LegacyPackageNames[index];
                if (installedPackageNames.Contains(legacyPackageName))
                {
                    conflicts.Add(legacyPackageName);
                }

                var legacyFolderName = entry.LegacyFolderNames[index];
                if (assetModuleFolders.Contains(legacyFolderName))
                {
                    conflicts.Add($"Assets/Modules/{legacyFolderName}");
                }
            }

            if (conflicts.Count == 0)
            {
                issue = default;
                return false;
            }

            issue = new ModuleInstallIssue(
                ModuleInstallIssueKind.LegacyModuleConflict,
                entry.PackageName,
                $"{entry.DisplayName} conflicts with legacy module(s): {string.Join(", ", conflicts)}. Remove them manually before retrying; Module Manager never removes legacy modules automatically.");
            return true;
        }

        internal static bool IsUpdateRequired(string installedVersion, string targetVersion)
        {
            return SemanticVersion.TryParse(installedVersion, out var installed)
                && SemanticVersion.TryParse(targetVersion, out var target)
                && installed.CompareTo(target) < 0;
        }

        private readonly struct SemanticVersion : IComparable<SemanticVersion>
        {
            private const int MaxLength = 256;

            private readonly string major;
            private readonly string minor;
            private readonly string patch;
            private readonly string[] preRelease;

            private SemanticVersion(string major, string minor, string patch, string[] preRelease)
            {
                this.major = major;
                this.minor = minor;
                this.patch = patch;
                this.preRelease = preRelease;
            }

            internal static bool TryParse(string value, out SemanticVersion version)
            {
                version = default;
                if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
                {
                    return false;
                }

                var buildSeparator = value.IndexOf('+');
                if (buildSeparator >= 0)
                {
                    if (!TryParseIdentifiers(value.Substring(buildSeparator + 1), false, out _))
                    {
                        return false;
                    }

                    value = value.Substring(0, buildSeparator);
                }

                var preReleaseSeparator = value.IndexOf('-');
                var core = preReleaseSeparator >= 0 ? value.Substring(0, preReleaseSeparator) : value;
                var preReleaseText = preReleaseSeparator >= 0 ? value.Substring(preReleaseSeparator + 1) : null;
                var coreParts = core.Split('.');
                if (coreParts.Length != 3
                    || !IsNumericIdentifier(coreParts[0], true)
                    || !IsNumericIdentifier(coreParts[1], true)
                    || !IsNumericIdentifier(coreParts[2], true))
                {
                    return false;
                }

                if (preReleaseText == null)
                {
                    version = new SemanticVersion(coreParts[0], coreParts[1], coreParts[2], Array.Empty<string>());
                    return true;
                }

                if (!TryParseIdentifiers(preReleaseText, true, out var preRelease))
                {
                    return false;
                }

                version = new SemanticVersion(coreParts[0], coreParts[1], coreParts[2], preRelease);
                return true;
            }

            public int CompareTo(SemanticVersion other)
            {
                var result = CompareNumericIdentifiers(major, other.major);
                if (result != 0)
                {
                    return result;
                }

                result = CompareNumericIdentifiers(minor, other.minor);
                if (result != 0)
                {
                    return result;
                }

                result = CompareNumericIdentifiers(patch, other.patch);
                if (result != 0)
                {
                    return result;
                }

                if (preRelease.Length == 0 || other.preRelease.Length == 0)
                {
                    return preRelease.Length == other.preRelease.Length ? 0 : preRelease.Length == 0 ? 1 : -1;
                }

                var sharedLength = Math.Min(preRelease.Length, other.preRelease.Length);
                for (var index = 0; index < sharedLength; index++)
                {
                    result = ComparePreReleaseIdentifiers(preRelease[index], other.preRelease[index]);
                    if (result != 0)
                    {
                        return result;
                    }
                }

                return preRelease.Length.CompareTo(other.preRelease.Length);
            }

            private static bool TryParseIdentifiers(string value, bool rejectNumericLeadingZero, out string[] identifiers)
            {
                identifiers = value.Split('.');
                if (identifiers.Length == 0)
                {
                    return false;
                }

                foreach (var identifier in identifiers)
                {
                    if (string.IsNullOrEmpty(identifier))
                    {
                        return false;
                    }

                    var numeric = true;
                    foreach (var character in identifier)
                    {
                        var digit = character >= '0' && character <= '9';
                        var letter = character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z';
                        if (!digit && !letter && character != '-')
                        {
                            return false;
                        }

                        numeric &= digit;
                    }

                    if (numeric && rejectNumericLeadingZero && identifier.Length > 1 && identifier[0] == '0')
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool IsNumericIdentifier(string value, bool rejectLeadingZero)
            {
                if (string.IsNullOrEmpty(value) || rejectLeadingZero && value.Length > 1 && value[0] == '0')
                {
                    return false;
                }

                foreach (var character in value)
                {
                    if (character < '0' || character > '9')
                    {
                        return false;
                    }
                }

                return true;
            }

            private static int ComparePreReleaseIdentifiers(string left, string right)
            {
                var leftNumeric = IsNumericIdentifier(left, false);
                var rightNumeric = IsNumericIdentifier(right, false);
                if (leftNumeric && rightNumeric)
                {
                    return CompareNumericIdentifiers(left, right);
                }

                if (leftNumeric != rightNumeric)
                {
                    return leftNumeric ? -1 : 1;
                }

                return string.CompareOrdinal(left, right);
            }

            private static int CompareNumericIdentifiers(string left, string right)
            {
                return left.Length == right.Length ? string.CompareOrdinal(left, right) : left.Length.CompareTo(right.Length);
            }
        }
    }
}
