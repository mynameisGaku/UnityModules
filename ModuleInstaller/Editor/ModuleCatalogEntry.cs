// SPDX-License-Identifier: MIT

using System;

namespace ModuleInstaller.Editor
{
    internal readonly struct ModuleCatalogEntry
    {
        internal ModuleCatalogEntry(
            string packageName,
            string folderName,
            string tag,
            string displayName,
            string summary)
        {
            PackageName = packageName ?? throw new ArgumentNullException(nameof(packageName));
            FolderName = folderName ?? throw new ArgumentNullException(nameof(folderName));
            Tag = tag ?? throw new ArgumentNullException(nameof(tag));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        }

        internal string PackageName { get; }
        internal string FolderName { get; }
        internal string Tag { get; }
        internal string DisplayName { get; }
        internal string Summary { get; }
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
    }
}
