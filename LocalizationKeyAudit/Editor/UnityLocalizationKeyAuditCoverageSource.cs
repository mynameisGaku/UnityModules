// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 宣言済み Assets と registered Packages scope を physical file 単位で読み取ります。
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

            var physicalByAssetPath = new Dictionary<string, string>(StringComparer.Ordinal);
            var rootByAssetPath = new Dictionary<string, CoverageRoot>(StringComparer.Ordinal);
            var declared = new List<string>(declaredAssetPaths.Count);
            for (var index = 0; index < declaredAssetPaths.Count; index++)
            {
                declared.Add(declaredAssetPaths[index]);
            }

            declared.Sort(StringComparer.Ordinal);
            var rootsByPrefix = CreateCoverageRoots(projectRoot, assetsRoot, declared);
            var physicalPaths = new string[declared.Count];
            var roots = new CoverageRoot[declared.Count];
            var isFile = new bool[declared.Count];
            var isDirectory = new bool[declared.Count];
            var isSupportedFile = new bool[declared.Count];
            for (var index = 0; index < declared.Count; index++)
            {
                var assetPath = declared[index];
                if (!IsDeclaredProjectPath(assetPath))
                {
                    throw new InvalidDataException($"coverage scope path が不正です: {assetPath}");
                }

                roots[index] = ResolveCoverageRoot(assetPath, rootsByPrefix);
                physicalPaths[index] = ResolvePhysicalPath(roots[index], assetPath);
                EnsureNoReparsePoint(roots[index].PhysicalRoot, physicalPaths[index]);
                isFile[index] = File.Exists(physicalPaths[index]);
                isDirectory[index] = Directory.Exists(physicalPaths[index]);
                isSupportedFile[index] = isFile[index] && IsSupportedYamlAssetExtension(physicalPaths[index]);
                if (isFile[index] && !isSupportedFile[index])
                {
                    ShouldIncludeYamlAssetFile(physicalPaths[index], true);
                }
            }

            EnsureDistinctDeclaredTargets(physicalPaths, declared);

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
                try
                {
                    if (isFile[index])
                    {
                        discoveredFileCount = IncrementPhysicalDiscoveryCount(
                            discoveredFileCount,
                            LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles,
                            "file");
                        AddFile(
                            roots[index],
                            physicalPath,
                            true,
                            physicalByAssetPath,
                            rootByAssetPath,
                            ref discoveryBytes);
                    }
                    else if (isDirectory[index])
                    {
                        DiscoverDirectory(
                            roots[index],
                            physicalPath,
                            physicalByAssetPath,
                            rootByAssetPath,
                            ref discoveryBytes,
                            ref discoveredFileCount,
                            ref discoveredDirectoryCount);
                    }
                    else
                    {
                        physicalByAssetPath[assetPath] = physicalPath;
                        rootByAssetPath[assetPath] = roots[index];
                    }
                }
                catch (LocalizationKeyAuditLimitException)
                {
                    throw;
                }
                catch (InvalidDataException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(
                        $"coverage scope を読み取れません: {assetPath} ({exception.GetType().Name})");
                }
            }

            var uniquePhysicalPaths = new HashSet<string>(GetPhysicalPathComparer());
            foreach (var pair in physicalByAssetPath)
            {
                string normalized;
                try
                {
                    normalized = NormalizePhysicalRoot(pair.Value);
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(
                        $"coverage asset path を検証できません: {pair.Key} ({exception.GetType().Name})");
                }

                if (!uniquePhysicalPaths.Add(normalized))
                {
                    throw new InvalidDataException(
                        $"複数の declared asset path が同じ physical path を指しています: {pair.Key}");
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
                    EnsureNoReparsePoint(rootByAssetPath[assetPath].PhysicalRoot, physicalPath);
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
                        exception.GetType().Name));
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

        /// <summary>case差を含むdeclared targetのphysical重複をscan前に拒否します。</summary>
        private static void EnsureDistinctDeclaredTargets(
            IReadOnlyList<string> physicalPaths,
            IReadOnlyList<string> declaredPaths)
        {
            if (physicalPaths.Count != declaredPaths.Count)
            {
                throw new ArgumentException("declared path と physical target の件数が一致しません。");
            }

            var unique = new HashSet<string>(GetPhysicalPathComparer());
            for (var index = 0; index < physicalPaths.Count; index++)
            {
                string normalized;
                try
                {
                    normalized = NormalizePhysicalRoot(physicalPaths[index]);
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(
                        $"coverage path を検証できません: {declaredPaths[index]} ({exception.GetType().Name})");
                }

                if (!unique.Add(normalized))
                {
                    throw new InvalidDataException(
                        $"複数の declared asset path が同じ physical target を指しています: {declaredPaths[index]}");
                }
            }
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
            CoverageRoot root,
            string physicalRoot,
            IDictionary<string, string> physicalByAssetPath,
            IDictionary<string, CoverageRoot> rootByAssetPath,
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
                EnsureNoReparsePoint(root.PhysicalRoot, directory);
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
                    AddFile(
                        root,
                        files[index],
                        false,
                        physicalByAssetPath,
                        rootByAssetPath,
                        ref discoveryBytes);
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

        /// <summary>1 file を対応する Unity asset path に変換し discovery 上限内で追加します。</summary>
        private static void AddFile(
            CoverageRoot root,
            string physicalPath,
            bool isExplicit,
            IDictionary<string, string> physicalByAssetPath,
            IDictionary<string, CoverageRoot> rootByAssetPath,
            ref long discoveryBytes)
        {
            EnsureNoReparsePoint(root.PhysicalRoot, physicalPath);
            if (!ShouldIncludeYamlAssetFile(physicalPath, isExplicit))
            {
                return;
            }

            var relative = Path.GetRelativePath(root.PhysicalRoot, physicalPath).Replace('\\', '/');
            var assetPath = relative == "." ? root.AssetPrefix : root.AssetPrefix + "/" + relative;
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
            rootByAssetPath[assetPath] = root;
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
                    $"明示指定されたcoverage fileの拡張子は未対応です: {Path.GetExtension(physicalPath)}");
            }

            return false;
        }

        /// <summary>宣言されたlogical rootだけをregistered physical rootへ固定します。</summary>
        private static Dictionary<string, CoverageRoot> CreateCoverageRoots(
            string projectRoot,
            string assetsRoot,
            IReadOnlyList<string> declaredPaths)
        {
            var needsAssets = false;
            var requestedPackageNames = new SortedSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < declaredPaths.Count; index++)
            {
                var path = declaredPaths[index];
                if (!IsDeclaredProjectPath(path))
                {
                    throw new InvalidDataException($"coverage scope path が不正です: {path}");
                }

                if (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    needsAssets = true;
                }
                else if (TryGetPackageName(path, out var packageName))
                {
                    requestedPackageNames.Add(packageName);
                }
            }

            var requestedRootCount = (needsAssets ? 1 : 0) + requestedPackageNames.Count;
            if (requestedRootCount != 1)
            {
                throw new InvalidDataException(
                    "1 回の監査で宣言できるlogical rootはAssetsまたは1つのregistered packageだけです。");
            }

            var roots = new Dictionary<string, CoverageRoot>(StringComparer.Ordinal);
            if (needsAssets)
            {
                string fullProjectRoot;
                try
                {
                    fullProjectRoot = NormalizePhysicalRoot(projectRoot);
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(
                        $"Unity project root を検証できません: {exception.GetType().Name}");
                }

                var assetsCoverageRoot = CreateCoverageRoot("Assets", assetsRoot);
                var fullAssetsRoot = assetsCoverageRoot.PhysicalRoot;
                if (!IsSameOrDescendantPhysicalPath(fullAssetsRoot, fullProjectRoot) ||
                    string.Equals(fullAssetsRoot, fullProjectRoot, GetPhysicalPathComparison()))
                {
                    throw new InvalidDataException("Assets root が Unity project root 内にありません。");
                }

                AddCoverageRoot(roots, assetsCoverageRoot);
            }

            if (requestedPackageNames.Count == 0)
            {
                return roots;
            }

            PackageManagerPackageInfo[] packages;
            try
            {
                packages = PackageManagerPackageInfo.GetAllRegisteredPackages() ??
                    Array.Empty<PackageManagerPackageInfo>();
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"registered package 一覧を取得できません: {exception.GetType().Name}");
            }

            Array.Sort(packages, ComparePackages);
            for (var index = 0; index < packages.Length; index++)
            {
                var package = packages[index];
                if (package == null || string.IsNullOrEmpty(package.name) ||
                    !requestedPackageNames.Contains(package.name))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(package.resolvedPath))
                {
                    throw new InvalidDataException($"registered package の resolvedPath が空です: {package.name}");
                }

                var prefix = "Packages/" + package.name;
                if (roots.ContainsKey(prefix))
                {
                    throw new InvalidDataException($"registered package name が重複しています: {package.name}");
                }

                AddCoverageRoot(
                    roots,
                    CreateCoverageRoot(prefix, package.resolvedPath));
            }

            foreach (var packageName in requestedPackageNames)
            {
                if (!roots.ContainsKey("Packages/" + packageName))
                {
                    throw new InvalidDataException($"registered package root を解決できません: Packages/{packageName}");
                }
            }

            return roots;
        }

        /// <summary>存在しreparseを通らない一意なphysical rootだけを追加します。</summary>
        private static void AddCoverageRoot(
            IDictionary<string, CoverageRoot> roots,
            CoverageRoot candidate)
        {
            if (!Directory.Exists(candidate.PhysicalRoot))
            {
                throw new DirectoryNotFoundException($"coverage physical root がありません: {candidate.AssetPrefix}");
            }

            try
            {
                EnsureNoReparsePointInRootAncestors(candidate.PhysicalRoot, candidate.AssetPrefix);
                EnsureNoReparsePoint(candidate.PhysicalRoot, candidate.PhysicalRoot);
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"coverage physical root を検証できません: {candidate.AssetPrefix} ({exception.GetType().Name})");
            }
            foreach (var existing in roots.Values)
            {
                if (IsSameOrDescendantPhysicalPath(candidate.PhysicalRoot, existing.PhysicalRoot) ||
                    IsSameOrDescendantPhysicalPath(existing.PhysicalRoot, candidate.PhysicalRoot))
                {
                    throw new InvalidDataException(
                        $"複数の coverage root が同じ physical tree を指しています: {existing.AssetPrefix}, {candidate.AssetPrefix}");
                }
            }

            roots.Add(candidate.AssetPrefix, candidate);
        }

        /// <summary>physical root正規化失敗をlogical package identityだけで報告します。</summary>
        private static CoverageRoot CreateCoverageRoot(string assetPrefix, string physicalRoot)
        {
            try
            {
                return new CoverageRoot(assetPrefix, NormalizePhysicalRoot(physicalRoot));
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"coverage physical root を解決できません: {assetPrefix} ({exception.GetType().Name})");
            }
        }

        /// <summary>logical asset pathをexactなAssetsまたはregistered package rootへ対応させます。</summary>
        private static CoverageRoot ResolveCoverageRoot(
            string assetPath,
            IReadOnlyDictionary<string, CoverageRoot> roots)
        {
            var prefix = assetPath == "Assets" || assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                ? "Assets"
                : TryGetPackageName(assetPath, out var packageName)
                    ? "Packages/" + packageName
                    : string.Empty;
            if (prefix.Length == 0 || !roots.TryGetValue(prefix, out var root))
            {
                throw new InvalidDataException($"coverage root を解決できません: {assetPath}");
            }

            return root;
        }

        /// <summary>registered root外へ出ないphysical pathを作ります。</summary>
        private static string ResolvePhysicalPath(CoverageRoot root, string assetPath)
        {
            try
            {
                var relative = assetPath == root.AssetPrefix
                    ? string.Empty
                    : assetPath.Substring(root.AssetPrefix.Length + 1);
                var physical = Path.GetFullPath(Path.Combine(
                    root.PhysicalRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsSameOrDescendantPhysicalPath(physical, root.PhysicalRoot))
                {
                    throw new InvalidDataException(
                        $"coverage path が registered root 外を指しています: {assetPath}");
                }

                return physical;
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"coverage path を解決できません: {assetPath} ({exception.GetType().Name})");
            }
        }

        /// <summary>rootからtargetまでの既存segmentにreparse pointがないことを確認します。</summary>
        private static void EnsureNoReparsePoint(string rootPath, string targetPath)
        {
            var root = NormalizePhysicalRoot(rootPath);
            var target = Path.GetFullPath(targetPath);
            if (!IsSameOrDescendantPhysicalPath(target, root))
            {
                throw new InvalidDataException("coverage path が registered root 内にありません。");
            }

            var current = target;
            while (!string.IsNullOrEmpty(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidDataException("coverage path が reparse point を通ります。");
                    }
                }
                catch (FileNotFoundException)
                {
                }
                catch (DirectoryNotFoundException)
                {
                }

                if (string.Equals(current, root, GetPhysicalPathComparison()))
                {
                    return;
                }

                current = Path.GetDirectoryName(current);
            }

            throw new InvalidDataException("coverage path が registered root 内にありません。");
        }

        /// <summary>filesystem rootからcoverage rootまでのreparse ancestorを拒否します。</summary>
        private static void EnsureNoReparsePointInRootAncestors(string rootPath, string assetPrefix)
        {
            var current = NormalizePhysicalRoot(rootPath);
            var fileSystemRoot = Path.GetPathRoot(current);
            if (string.IsNullOrEmpty(fileSystemRoot))
            {
                throw new InvalidDataException($"coverage physical root を検証できません: {assetPrefix}");
            }

            while (!string.IsNullOrEmpty(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException(
                        $"coverage root が reparse point を通ります: {assetPrefix}");
                }

                if (string.Equals(
                        NormalizePhysicalRoot(current),
                        NormalizePhysicalRoot(fileSystemRoot),
                        GetPhysicalPathComparison()))
                {
                    return;
                }

                current = Path.GetDirectoryName(current);
            }

            throw new InvalidDataException($"coverage physical root を検証できません: {assetPrefix}");
        }

        /// <summary>AssetsまたはPackages/package-nameをrootに持つ安全なdeclared pathかを調べます。</summary>
        private static bool IsDeclaredProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                path.Length > LocalizationKeyAuditLimits.MaximumTextCharacters ||
                path.IndexOf('\\') >= 0 ||
                path.IndexOf('\0') >= 0)
            {
                return false;
            }

            var segments = path.Split('/');
            for (var index = 0; index < segments.Length; index++)
            {
                if (segments[index].Length == 0 || segments[index] == "." || segments[index] == ".." ||
                    segments[index].IndexOf('~') >= 0 || segments[index].IndexOf(':') >= 0 ||
                    segments[index].EndsWith(".", StringComparison.Ordinal) ||
                    segments[index].EndsWith(" ", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return segments[0] == "Assets" ||
                (segments[0] == "Packages" && segments.Length >= 2);
        }

        /// <summary>Packages/package-name[/...]からexactなpackage nameを取り出します。</summary>
        private static bool TryGetPackageName(string assetPath, out string packageName)
        {
            packageName = string.Empty;
            const string prefix = "Packages/";
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var separator = assetPath.IndexOf('/', prefix.Length);
            packageName = separator < 0
                ? assetPath.Substring(prefix.Length)
                : assetPath.Substring(prefix.Length, separator - prefix.Length);
            return packageName.Length > 0;
        }

        /// <summary>physical pathがroot自身または子孫かをplatform規則で調べます。</summary>
        private static bool IsSameOrDescendantPhysicalPath(string path, string root)
        {
            var fullPath = NormalizePhysicalRoot(path);
            var fullRoot = NormalizePhysicalRoot(root);
            if (string.Equals(fullPath, fullRoot, GetPhysicalPathComparison()))
            {
                return true;
            }

            var boundary = fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                fullRoot.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    ? fullRoot
                    : fullRoot + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(boundary, GetPhysicalPathComparison());
        }

        /// <summary>drive rootを壊さずphysical root末尾separatorだけを除きます。</summary>
        private static string NormalizePhysicalRoot(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var pathRoot = Path.GetPathRoot(fullPath) ?? string.Empty;
            while (fullPath.Length > pathRoot.Length &&
                   (fullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                    fullPath.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)))
            {
                fullPath = fullPath.Substring(0, fullPath.Length - 1);
            }

            return fullPath;
        }

        /// <summary>OSのfilesystem case ruleに合わせた比較方法です。</summary>
        private static StringComparison GetPhysicalPathComparison()
        {
            return Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        /// <summary>OSのfilesystem case ruleに合わせたcomparerです。</summary>
        private static StringComparer GetPhysicalPathComparer()
        {
            return Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }

        /// <summary>registered packageをname、resolved pathの順に並べます。</summary>
        private static int ComparePackages(PackageManagerPackageInfo left, PackageManagerPackageInfo right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            var comparison = string.Compare(left.name, right.name, StringComparison.Ordinal);
            return comparison != 0
                ? comparison
                : string.Compare(left.resolvedPath, right.resolvedPath, StringComparison.Ordinal);
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

        /// <summary>folder scanでdirect static referenceを認識するUnity YAML asset種別です。</summary>
        private static bool IsSupportedYamlAssetExtension(string physicalPath)
        {
            var extension = Path.GetExtension(physicalPath);
            return string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>logical Unity prefixとregistered physical directoryの固定対応です。</summary>
        private readonly struct CoverageRoot
        {
            /// <summary>検証済みroot pairを保持します。</summary>
            internal CoverageRoot(string assetPrefix, string physicalRoot)
            {
                AssetPrefix = assetPrefix;
                PhysicalRoot = physicalRoot;
            }

            /// <summary>AssetsまたはPackages/package-nameです。</summary>
            internal string AssetPrefix { get; }

            /// <summary>対応するabsolute physical directoryです。</summary>
            internal string PhysicalRoot { get; }
        }
    }
}
