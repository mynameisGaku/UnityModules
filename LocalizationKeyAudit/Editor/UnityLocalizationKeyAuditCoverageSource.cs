// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// v1 の Assets-only declared scope を physical file 単位で読み取ります。
    /// </summary>
    internal sealed class UnityLocalizationKeyAuditCoverageSource : ILocalizationKeyAuditCoverageSource
    {
        /// <summary>scope overlap を除去し、reparse point を追跡せず全 file を収集します。</summary>
        public IReadOnlyList<LocalizationKeyAuditCoverageAsset> ReadAssets(
            IReadOnlyList<string> declaredAssetPaths)
        {
            if (declaredAssetPaths == null)
            {
                throw new ArgumentNullException(nameof(declaredAssetPaths));
            }

            var assetsRoot = Path.GetFullPath(Application.dataPath);
            var projectRoot = Directory.GetParent(assetsRoot)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidDataException("Unity project root を取得できません。");
            }

            EnsureNoReparsePoint(assetsRoot, assetsRoot);
            var physicalByAssetPath = new Dictionary<string, string>(StringComparer.Ordinal);
            var declared = new List<string>(declaredAssetPaths.Count);
            for (var index = 0; index < declaredAssetPaths.Count; index++)
            {
                declared.Add(declaredAssetPaths[index]);
            }

            declared.Sort(StringComparer.Ordinal);
            var physicalPaths = new string[declared.Count];
            var isFile = new bool[declared.Count];
            var isDirectory = new bool[declared.Count];
            var isSupportedFile = new bool[declared.Count];
            for (var index = 0; index < declared.Count; index++)
            {
                var assetPath = declared[index];
                if (!IsDeclaredProjectPath(assetPath))
                {
                    throw new InvalidDataException($"v1 coverage scope は Assets-only です: {assetPath}");
                }

                physicalPaths[index] = ResolvePhysicalPath(projectRoot, assetsRoot, assetPath);
                isFile[index] = File.Exists(physicalPaths[index]);
                isDirectory[index] = Directory.Exists(physicalPaths[index]);
                isSupportedFile[index] = isFile[index] && IsSupportedYamlAssetExtension(physicalPaths[index]);
                if (isFile[index] && !isSupportedFile[index])
                {
                    ShouldIncludeYamlAssetFile(physicalPaths[index], true);
                }
            }

            var selectedTargetIndices = SelectNonOverlappingTargets(
                physicalPaths,
                isDirectory,
                isSupportedFile);
            long discoveryBytes = 0;
            var discoveredFileCount = 0;
            var discoveredDirectoryCount = 0;
            for (var selectedIndex = 0; selectedIndex < selectedTargetIndices.Count; selectedIndex++)
            {
                var index = selectedTargetIndices[selectedIndex];
                var assetPath = declared[index];
                var physicalPath = physicalPaths[index];
                if (isFile[index])
                {
                    discoveredFileCount = IncrementPhysicalDiscoveryCount(
                        discoveredFileCount,
                        LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles,
                        "file");
                    AddFile(assetsRoot, physicalPath, true, physicalByAssetPath, ref discoveryBytes);
                }
                else if (isDirectory[index])
                {
                    DiscoverDirectory(
                        assetsRoot,
                        physicalPath,
                        physicalByAssetPath,
                        ref discoveryBytes,
                        ref discoveredFileCount,
                        ref discoveredDirectoryCount);
                }
                else
                {
                    physicalByAssetPath[assetPath] = physicalPath;
                }
            }

            var physicalComparer = Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var uniquePhysicalPaths = new HashSet<string>(physicalComparer);
            foreach (var pair in physicalByAssetPath)
            {
                if (!uniquePhysicalPaths.Add(Path.GetFullPath(pair.Value)))
                {
                    throw new InvalidDataException(
                        $"複数の declared asset path が同じ physical path を指しています: {pair.Value}");
                }
            }

            var assetPaths = new List<string>(physicalByAssetPath.Keys);
            assetPaths.Sort(StringComparer.Ordinal);
            var assets = new List<LocalizationKeyAuditCoverageAsset>(assetPaths.Count);
            long actualReadBytes = 0;
            for (var index = 0; index < assetPaths.Count; index++)
            {
                var assetPath = assetPaths[index];
                var physicalPath = physicalByAssetPath[assetPath];
                if (!File.Exists(physicalPath))
                {
                    assets.Add(new LocalizationKeyAuditCoverageAsset(assetPath, Array.Empty<byte>(), false));
                    continue;
                }

                try
                {
                    EnsureNoReparsePoint(assetsRoot, physicalPath);
                    using (var stream = new FileStream(
                               physicalPath,
                               FileMode.Open,
                               FileAccess.Read,
                               FileShare.Read,
                               65536,
                               FileOptions.SequentialScan))
                    {
                        if (stream.Length > LocalizationKeyAuditLimits.MaximumCoverageAssetBytes)
                        {
                            assets.Add(new LocalizationKeyAuditCoverageAsset(
                                assetPath,
                                Array.Empty<byte>(),
                                true,
                                false,
                                true));
                            continue;
                        }

                        actualReadBytes = EnsureActualReadBudget(actualReadBytes, stream.Length);
                        var bytes = new byte[(int)stream.Length];
                        var offset = 0;
                        while (offset < bytes.Length)
                        {
                            var read = stream.Read(bytes, offset, bytes.Length - offset);
                            if (read == 0)
                            {
                                throw new EndOfStreamException("coverage file が読み取り中に短くなりました。");
                            }

                            offset += read;
                        }

                        if (stream.ReadByte() != -1)
                        {
                            throw new IOException("coverage file が読み取り中に変化しました。");
                        }

                        assets.Add(new LocalizationKeyAuditCoverageAsset(assetPath, bytes));
                    }
                }
                catch (LocalizationKeyAuditLimitException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    assets.Add(new LocalizationKeyAuditCoverageAsset(
                        assetPath,
                        Array.Empty<byte>(),
                        true,
                        false,
                        false,
                        $"{exception.GetType().Name}: {exception.Message}"));
                }
            }

            return assets;
        }

        /// <summary>metadata確認後に増大したfileもallocation前のactual aggregate上限で拒否します。</summary>
        internal static long EnsureActualReadBudget(long bytesAlreadyRead, long nextFileBytes)
        {
            if (bytesAlreadyRead < 0 || nextFileBytes < 0 ||
                bytesAlreadyRead > LocalizationKeyAuditLimits.MaximumCoverageTotalBytes ||
                nextFileBytes > LocalizationKeyAuditLimits.MaximumCoverageTotalBytes - bytesAlreadyRead)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"coverage actual read byte 数が上限 {LocalizationKeyAuditLimits.MaximumCoverageTotalBytes} を超えています。");
            }

            return bytesAlreadyRead + nextFileBytes;
        }

        /// <summary>ancestor directoryで同じsupported targetを一度だけ走査し、explicit unsupported/missingは保持します。</summary>
        internal static IReadOnlyList<int> SelectNonOverlappingTargets(
            IReadOnlyList<string> physicalPaths,
            IReadOnlyList<bool> isDirectory,
            IReadOnlyList<bool> isSupportedFile)
        {
            if (physicalPaths == null || isDirectory == null || isSupportedFile == null ||
                physicalPaths.Count != isDirectory.Count || physicalPaths.Count != isSupportedFile.Count)
            {
                throw new ArgumentException("declared physical target metadataの件数が一致しません。");
            }

            var order = new List<int>(physicalPaths.Count);
            for (var index = 0; index < physicalPaths.Count; index++)
            {
                order.Add(index);
            }

            order.Sort((left, right) =>
            {
                var comparison = physicalPaths[left].Length.CompareTo(physicalPaths[right].Length);
                return comparison != 0
                    ? comparison
                    : string.Compare(physicalPaths[left], physicalPaths[right], StringComparison.Ordinal);
            });

            var selected = new List<int>(order.Count);
            var directoryComparer = Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var selectedDirectories = new HashSet<string>(directoryComparer);
            for (var orderIndex = 0; orderIndex < order.Count; orderIndex++)
            {
                var candidate = order[orderIndex];
                var covered = HasSelectedDirectoryAncestor(
                    physicalPaths[candidate],
                    selectedDirectories);

                if (covered && (isDirectory[candidate] || isSupportedFile[candidate]))
                {
                    continue;
                }

                selected.Add(candidate);
                if (isDirectory[candidate])
                {
                    selectedDirectories.Add(Path.GetFullPath(physicalPaths[candidate]));
                }
            }

            selected.Sort();
            return selected;
        }

        /// <summary>candidateが同じphysical directoryまたはその子孫かをplatform comparerで調べます。</summary>
        private static bool HasSelectedDirectoryAncestor(
            string candidate,
            ISet<string> selectedDirectories)
        {
            var current = Path.GetFullPath(candidate);
            while (!string.IsNullOrEmpty(current))
            {
                if (selectedDirectories.Contains(current))
                {
                    return true;
                }

                var parent = Path.GetDirectoryName(current);
                if (string.Equals(parent, current, StringComparison.Ordinal))
                {
                    break;
                }

                current = parent;
            }

            return false;
        }

        /// <summary>directory tree を明示 stack で決定論的に列挙します。</summary>
        private static void DiscoverDirectory(
            string assetsRoot,
            string physicalRoot,
            IDictionary<string, string> physicalByAssetPath,
            ref long discoveryBytes,
            ref int discoveredFileCount,
            ref int discoveredDirectoryCount)
        {
            var pending = new Stack<string>();
            discoveredDirectoryCount = IncrementPhysicalDiscoveryCount(
                discoveredDirectoryCount,
                LocalizationKeyAuditLimits.MaximumPhysicalDirectories,
                "directory");
            pending.Push(physicalRoot);
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                EnsureNoReparsePoint(assetsRoot, directory);
                var directories = new List<string>();
                foreach (var childDirectory in Directory.EnumerateDirectories(directory))
                {
                    discoveredDirectoryCount = IncrementPhysicalDiscoveryCount(
                        discoveredDirectoryCount,
                        LocalizationKeyAuditLimits.MaximumPhysicalDirectories,
                        "directory");
                    if (!ShouldIgnoreName(Path.GetFileName(childDirectory)))
                    {
                        directories.Add(childDirectory);
                    }
                }

                directories.Sort(StringComparer.Ordinal);
                for (var index = directories.Count - 1; index >= 0; index--)
                {
                    pending.Push(directories[index]);
                }

                var files = new List<string>();
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    discoveredFileCount = IncrementPhysicalDiscoveryCount(
                        discoveredFileCount,
                        LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles,
                        "file");
                    if (!ShouldIgnoreFile(file) && ShouldIncludeYamlAssetFile(file, false))
                    {
                        files.Add(file);
                    }
                }

                files.Sort(StringComparer.Ordinal);
                for (var index = 0; index < files.Count; index++)
                {
                    AddFile(assetsRoot, files[index], false, physicalByAssetPath, ref discoveryBytes);
                }
            }
        }

        /// <summary>filesystem entryを保持・解析する前にglobal discovery budgetを消費します。</summary>
        internal static int IncrementPhysicalDiscoveryCount(int currentCount, int maximum, string itemKind)
        {
            if (currentCount < 0 || maximum <= 0 || currentCount >= maximum)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"coverage physical {itemKind} 数が上限 {maximum} 件を超えています。");
            }

            return currentCount + 1;
        }

        /// <summary>1 file を Assets path に変換し discovery 上限内で追加します。</summary>
        private static void AddFile(
            string assetsRoot,
            string physicalPath,
            bool isExplicit,
            IDictionary<string, string> physicalByAssetPath,
            ref long discoveryBytes)
        {
            EnsureNoReparsePoint(assetsRoot, physicalPath);
            if (!ShouldIncludeYamlAssetFile(physicalPath, isExplicit))
            {
                return;
            }

            var relative = Path.GetRelativePath(assetsRoot, physicalPath).Replace('\\', '/');
            var assetPath = relative == "." ? "Assets" : "Assets/" + relative;
            if (physicalByAssetPath.ContainsKey(assetPath))
            {
                return;
            }

            if (physicalByAssetPath.Count >= LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"coverage file 数が上限 {LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles} 件を超えています。");
            }

            discoveryBytes = checked(discoveryBytes + new FileInfo(physicalPath).Length);
            if (discoveryBytes > LocalizationKeyAuditLimits.MaximumCoverageTotalBytes)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"coverage discovery byte 数が上限 {LocalizationKeyAuditLimits.MaximumCoverageTotalBytes} を超えています。");
            }

            physicalByAssetPath[assetPath] = physicalPath;
        }

        /// <summary>folderでは未対応fileを除外し、明示指定ではcoverage不完全として扱えるよう拒否します。</summary>
        internal static bool ShouldIncludeYamlAssetFile(string physicalPath, bool isExplicit)
        {
            if (IsSupportedYamlAssetExtension(physicalPath))
            {
                return true;
            }

            if (isExplicit)
            {
                throw new InvalidDataException(
                    $"明示指定されたcoverage fileの拡張子はv1で未対応です: {physicalPath}");
            }

            return false;
        }

        /// <summary>project root 外へ出ない physical path を作ります。</summary>
        private static string ResolvePhysicalPath(string projectRoot, string assetsRoot, string assetPath)
        {
            var relative = assetPath == "Assets" ? string.Empty : assetPath.Substring("Assets/".Length);
            var physical = Path.GetFullPath(Path.Combine(assetsRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            var boundary = assetsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!string.Equals(physical, assetsRoot, StringComparison.OrdinalIgnoreCase) &&
                !physical.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"coverage path が Assets 外を指しています: {assetPath}");
            }

            var projectBoundary = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!physical.StartsWith(projectBoundary, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"coverage path が project 外を指しています: {assetPath}");
            }

            return physical;
        }

        /// <summary>Assets root から target までの既存 segment に reparse point がないことを確認します。</summary>
        private static void EnsureNoReparsePoint(string assetsRoot, string targetPath)
        {
            var root = Path.GetFullPath(assetsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var target = Path.GetFullPath(targetPath);
            var current = target;
            while (current.Length >= root.Length)
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException($"coverage path に reparse point があります: {current}");
                }

                if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                current = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(current))
                {
                    break;
                }
            }

            throw new InvalidDataException($"coverage path が Assets root 内にありません: {targetPath}");
        }

        /// <summary>v1 coverage scope の Assets path かを調べます。</summary>
        private static bool IsDeclaredProjectPath(string path)
        {
            return path == "Assets" ||
                (!string.IsNullOrEmpty(path) &&
                 path.StartsWith("Assets/", StringComparison.Ordinal) &&
                 path.IndexOf('\\') < 0 &&
                 path.IndexOf("/../", StringComparison.Ordinal) < 0 &&
                 !path.EndsWith("/..", StringComparison.Ordinal));
        }

        /// <summary>Unity meta と temporary/dot file を coverage 対象外にします。</summary>
        private static bool ShouldIgnoreFile(string physicalPath)
        {
            var name = Path.GetFileName(physicalPath);
            return name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || ShouldIgnoreName(name);
        }

        /// <summary>dot/tilde 名を追跡しません。</summary>
        private static bool ShouldIgnoreName(string name)
        {
            return string.IsNullOrEmpty(name) ||
                name.StartsWith(".", StringComparison.Ordinal) ||
                name.EndsWith("~", StringComparison.Ordinal);
        }

        /// <summary>v1 folder scan で direct static reference を認識する Unity YAML asset 種別です。</summary>
        private static bool IsSupportedYamlAssetExtension(string physicalPath)
        {
            var extension = Path.GetExtension(physicalPath);
            return string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase);
        }
    }
}
