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
            IReadOnlyList<string> legacyFolderNames,
            string guideRelativePath)
        {
            PackageName = packageName ?? throw new ArgumentNullException(nameof(packageName));
            FolderName = folderName ?? throw new ArgumentNullException(nameof(folderName));
            Tag = tag ?? throw new ArgumentNullException(nameof(tag));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            if (string.IsNullOrEmpty(guideRelativePath)
                || guideRelativePath.StartsWith("/", StringComparison.Ordinal)
                || guideRelativePath.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                throw new ArgumentException("説明文書の相対パスには、安全なパッケージ内パスが必要です。", nameof(guideRelativePath));
            }

            GuideRelativePath = guideRelativePath;
            LegacyPackageNames = Copy(legacyPackageNames);
            LegacyFolderNames = Copy(legacyFolderNames);
            if (LegacyPackageNames.Count != LegacyFolderNames.Count)
            {
                throw new ArgumentException("旧パッケージと旧フォルダーの件数が一致している必要があります。");
            }
        }

        internal string PackageName { get; }
        internal string FolderName { get; }
        internal string Tag { get; }
        internal string DisplayName { get; }
        internal string Summary { get; }
        internal string GuideRelativePath { get; }
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

        internal string GuideUrl =>
            $"https://github.com/mynameisGaku/UnityModules/blob/{Tag}/{FolderName}/{GuideRelativePath}";

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
                    throw new ArgumentException("旧識別子に未設定または空の値は使えません。");
                }

                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }
    }
}
