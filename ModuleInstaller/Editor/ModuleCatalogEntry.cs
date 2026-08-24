// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace ModuleInstaller.Editor
{
    internal readonly struct ModuleCatalogEntry
    {
        internal ModuleCatalogEntry(
            string packageName,
            string folderName,
            string tag,
            string displayName,
            string summary,
            IReadOnlyList<string> legacyPackageNames,
            IReadOnlyList<string> legacyFolderNames)
        {
            PackageName = packageName ?? throw new ArgumentNullException(nameof(packageName));
            FolderName = folderName ?? throw new ArgumentNullException(nameof(folderName));
            Tag = tag ?? throw new ArgumentNullException(nameof(tag));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            LegacyPackageNames = Copy(legacyPackageNames);
            LegacyFolderNames = Copy(legacyFolderNames);
            if (LegacyPackageNames.Count != LegacyFolderNames.Count)
            {
                throw new ArgumentException("Legacy package and folder counts must match.");
            }
        }

        internal string PackageName { get; }
        internal string FolderName { get; }
        internal string Tag { get; }
        internal string DisplayName { get; }
        internal string Summary { get; }
        internal IReadOnlyList<string> LegacyPackageNames { get; }
        internal IReadOnlyList<string> LegacyFolderNames { get; }
        internal string Version
        {
            get
            {
                var markerIndex = Tag.LastIndexOf("-v", StringComparison.Ordinal);
                return markerIndex >= 0 ? Tag.Substring(markerIndex + 2) : string.Empty;
            }
        }

        internal string GitUrl =>
            $"https://github.com/mynameisGaku/UnityModules.git?path=/{FolderName}#{Tag}";

        internal string ReadmeUrl =>
            $"https://github.com/mynameisGaku/UnityModules/blob/{Tag}/{FolderName}/README.md";

        private static IReadOnlyList<string> Copy(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                if (string.IsNullOrEmpty(source[index]))
                {
                    throw new ArgumentException("Legacy identifiers cannot be null or empty.");
                }

                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }
    }
}
