// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// AssetDatabase type search と physical Unity YAML scan の和集合を raw bytes として収集します。
    /// </summary>
    internal sealed class UnityLocalizationKeyAuditRawSource : ILocalizationKeyAuditRawSource
    {
        /// <summary>Localization 1.5.12 の SharedTableData MonoScript GUID です。</summary>
        internal const string SharedTableDataScriptGuid = "5b11a58205ec3474ca216360e9fa74a8";

        /// <summary>1 YAML line の識別に保持する先頭 byte 数です。</summary>
        private const int DiscoveryLinePrefixBytes = 1024;

        /// <summary>
        /// typed search から漏れた malformed SharedTableData も physical scan で候補へ追加します。
        /// </summary>
        public IReadOnlyList<LocalizationKeyAuditRawAsset> ReadSharedTableDataAssets()
        {
            var physicalPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            var roots = GetDiscoveryRoots();
            var discoveredFileCount = 0;
            var discoveredDirectoryCount = 0;
            long discoveredByteCount = 0;
            for (var index = 0; index < roots.Count; index++)
            {
                DiscoverPhysicalCandidates(
                    roots[index],
                    physicalPaths,
                    ref discoveredFileCount,
                    ref discoveredDirectoryCount,
                    ref discoveredByteCount);
            }

            var guids = AssetDatabase.FindAssets("t:SharedTableData") ?? Array.Empty<string>();
            EnsureTypedCandidateCountWithinLimit(guids.Length);
            Array.Sort(guids, StringComparer.Ordinal);
            for (var index = 0; index < guids.Length; index++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    throw new InvalidDataException($"t:SharedTableData GUID {guids[index]} の asset path を取得できません。");
                }

                var physicalPath = ResolvePhysicalPath(assetPath);
                AddCandidatePath(physicalPaths, assetPath, physicalPath);
            }

            if (physicalPaths.Count > LocalizationKeyAuditLimits.MaximumSharedTableDataAssets)
            {
                throw new InvalidDataException(
                    $"SharedTableData candidate 数が上限 {LocalizationKeyAuditLimits.MaximumSharedTableDataAssets} 件を超えています。");
            }

            var paths = new List<string>(physicalPaths.Keys);
            paths.Sort(StringComparer.Ordinal);
            var assets = new List<LocalizationKeyAuditRawAsset>(paths.Count);
            long actualReadBytes = 0;
            for (var index = 0; index < paths.Count; index++)
            {
                assets.Add(ReadCandidate(paths[index], physicalPaths[paths[index]], ref actualReadBytes));
            }

            return assets;
        }

        /// <summary>typed candidate GUIDをsort/path解決する前に件数上限を検証します。</summary>
        internal static void EnsureTypedCandidateCountWithinLimit(int count)
        {
            if (count < 0 || count > LocalizationKeyAuditLimits.MaximumSharedTableDataAssets)
            {
                throw new InvalidDataException(
                    $"typed SharedTableData candidate 数が上限 {LocalizationKeyAuditLimits.MaximumSharedTableDataAssets} 件を超えています。");
            }
        }

        /// <summary>Assets と全 registered package の scan root を構築します。</summary>
        private static List<DiscoveryRoot> GetDiscoveryRoots()
        {
            var roots = new List<DiscoveryRoot>();
            var uniquePhysicalRoots = new HashSet<string>(GetPhysicalPathComparer());
            var assetsRoot = Path.GetFullPath(Application.dataPath);
            AddDiscoveryRoot(roots, uniquePhysicalRoots, new DiscoveryRoot("Assets", assetsRoot));

            var packages = PackageManagerPackageInfo.GetAllRegisteredPackages() ?? Array.Empty<PackageManagerPackageInfo>();
            Array.Sort(packages, ComparePackages);
            for (var index = 0; index < packages.Length; index++)
            {
                var package = packages[index];
                if (package == null || string.IsNullOrWhiteSpace(package.name) || string.IsNullOrWhiteSpace(package.resolvedPath))
                {
                    throw new InvalidDataException("registered package の name または resolvedPath が空です。");
                }

                AddDiscoveryRoot(
                    roots,
                    uniquePhysicalRoots,
                    new DiscoveryRoot("Packages/" + package.name, Path.GetFullPath(package.resolvedPath)));
            }

            return roots;
        }

        /// <summary>重複 physical root を 1 回だけ scan 対象へ追加します。</summary>
        private static void AddDiscoveryRoot(
            ICollection<DiscoveryRoot> roots,
            ISet<string> uniquePhysicalRoots,
            DiscoveryRoot root)
        {
            if (!Directory.Exists(root.PhysicalRoot))
            {
                throw new DirectoryNotFoundException($"physical discovery root がありません: {root.AssetPrefix}");
            }

            if (HasReparsePoint(root.PhysicalRoot))
            {
                throw new IOException($"physical discovery root が reparse point です: {root.AssetPrefix}");
            }

            if (uniquePhysicalRoots.Add(root.PhysicalRoot))
            {
                roots.Add(root);
            }
        }

        /// <summary>Unity が対象外にする dot/tilde path を除いて .asset YAML を全走査します。</summary>
        private static void DiscoverPhysicalCandidates(
            DiscoveryRoot root,
            IDictionary<string, string> candidates,
            ref int discoveredFileCount,
            ref int discoveredDirectoryCount,
            ref long discoveredByteCount)
        {
            var stack = new Stack<string>();
            discoveredDirectoryCount = IncrementPhysicalDiscoveryCount(
                discoveredDirectoryCount,
                LocalizationKeyAuditLimits.MaximumPhysicalDirectories,
                "directory");
            stack.Push(root.PhysicalRoot);
            while (stack.Count > 0)
            {
                var directory = stack.Pop();
                List<string> childDirectories;
                string[] files;
                try
                {
                    childDirectories = new List<string>();
                    foreach (var childDirectory in Directory.EnumerateDirectories(directory))
                    {
                        discoveredDirectoryCount = IncrementPhysicalDiscoveryCount(
                            discoveredDirectoryCount,
                            LocalizationKeyAuditLimits.MaximumPhysicalDirectories,
                            "directory");
                        childDirectories.Add(childDirectory);
                    }

                    var matchingFiles = new List<string>();
                    foreach (var candidate in Directory.EnumerateFiles(directory))
                    {
                        discoveredFileCount = IncrementPhysicalDiscoveryCount(
                            discoveredFileCount,
                            LocalizationKeyAuditLimits.MaximumPhysicalAssetFiles,
                            "file");

                        if (IsAssetFilePath(candidate))
                        {
                            matchingFiles.Add(candidate);
                        }
                    }

                    files = matchingFiles.ToArray();
                }
                catch (InvalidDataException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new IOException(
                        $"physical discovery directory を列挙できません: {root.AssetPrefix} ({exception.GetType().Name})",
                        exception);
                }

                childDirectories.Sort(StringComparer.Ordinal);
                for (var index = childDirectories.Count - 1; index >= 0; index--)
                {
                    var child = childDirectories[index];
                    if (ShouldIgnorePathName(Path.GetFileName(child)))
                    {
                        continue;
                    }

                    if (HasReparsePoint(child))
                    {
                        throw new IOException($"physical discovery path に reparse point があります: {root.AssetPrefix}");
                    }

                    stack.Push(child);
                }

                Array.Sort(files, StringComparer.Ordinal);
                for (var index = 0; index < files.Length; index++)
                {
                    var file = files[index];
                    if (ShouldIgnorePathName(Path.GetFileName(file)))
                    {
                        continue;
                    }

                    if (HasReparsePoint(file))
                    {
                        throw new IOException($"physical .asset file が reparse point です: {root.AssetPrefix}");
                    }

                    var length = new FileInfo(file).Length;
                    discoveredByteCount = checked(discoveredByteCount + length);
                    if (discoveredByteCount > LocalizationKeyAuditLimits.MaximumPhysicalDiscoveryBytes)
                    {
                        throw new InvalidDataException(
                            $"physical discovery byte 数が上限 {LocalizationKeyAuditLimits.MaximumPhysicalDiscoveryBytes} を超えています。");
                    }

                    if (!ContainsSharedTableDataScriptGuid(file))
                    {
                        continue;
                    }

                    var relativePath = Path.GetRelativePath(root.PhysicalRoot, file).Replace('\\', '/');
                    var assetPath = root.AssetPrefix + "/" + relativePath;
                    AddCandidatePath(candidates, assetPath, Path.GetFullPath(file));
                }
            }
        }

        /// <summary>physical entryを保持する前にglobal discovery budgetを消費します。</summary>
        internal static int IncrementPhysicalDiscoveryCount(int currentCount, int maximum, string itemKind)
        {
            if (currentCount < 0 || maximum <= 0 || currentCount >= maximum)
            {
                throw new InvalidDataException(
                    $"physical discovery {itemKind} 数が上限 {maximum} 件を超えています。");
            }

            return currentCount + 1;
        }

        /// <summary>physical fallbackで大小文字を問わずUnity .asset fileだけを選びます。</summary>
        internal static bool IsAssetFilePath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                string.Equals(Path.GetExtension(path), ".asset", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>全 file を bounded line-prefix scanner で読み、exact m_Script GUID を探します。</summary>
        internal static bool ContainsSharedTableDataScriptGuid(string physicalPath)
        {
            var prefix = new byte[DiscoveryLinePrefixBytes];
            var prefixLength = 0;
            var lineTruncated = false;
            var skipLineFeedAfterCarriageReturn = false;
            var buffer = new byte[65536];
            using (var stream = new FileStream(
                       physicalPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       buffer.Length,
                       FileOptions.SequentialScan))
            {
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (var index = 0; index < read; index++)
                    {
                        var value = buffer[index];
                        if (skipLineFeedAfterCarriageReturn)
                        {
                            skipLineFeedAfterCarriageReturn = false;
                            if (value == (byte)'\n')
                            {
                                continue;
                            }
                        }

                        if (value == (byte)'\r' || value == (byte)'\n')
                        {
                            if (LineContainsSharedTableDataScriptGuid(prefix, prefixLength))
                            {
                                return true;
                            }

                            if (IsTruncatedScriptLineIndeterminate(prefix, prefixLength, lineTruncated))
                            {
                                throw new InvalidDataException(
                                    $"physical .asset の m_Script line が {DiscoveryLinePrefixBytes} bytesを超え、SharedTableData候補か確定できません。");
                            }

                            prefixLength = 0;
                            lineTruncated = false;
                            skipLineFeedAfterCarriageReturn = value == (byte)'\r';
                        }
                        else if (prefixLength < prefix.Length)
                        {
                            prefix[prefixLength++] = value;
                        }
                        else
                        {
                            lineTruncated = true;
                        }
                    }
                }
            }

            if (prefixLength > 0 && LineContainsSharedTableDataScriptGuid(prefix, prefixLength))
            {
                return true;
            }

            if (IsTruncatedScriptLineIndeterminate(prefix, prefixLength, lineTruncated))
            {
                throw new InvalidDataException(
                    $"physical .asset の m_Script line が {DiscoveryLinePrefixBytes} bytesを超え、SharedTableData候補か確定できません。");
            }

            return false;
        }

        /// <summary>prefix上でexact m_Script keyが見えた長行をno-matchへ落とさず不確定とします。</summary>
        internal static bool IsTruncatedScriptLineIndeterminate(byte[] prefix, int length, bool wasTruncated)
        {
            if (!wasTruncated || prefix == null || length <= 0 || length > prefix.Length)
            {
                return false;
            }

            var line = System.Text.Encoding.ASCII.GetString(prefix, 0, length).TrimEnd('\r');
            var trimmed = line.TrimStart(' ', '\t');
            const string scriptKey = "m_Script";
            if (!trimmed.StartsWith(scriptKey, StringComparison.Ordinal))
            {
                return false;
            }

            var cursor = scriptKey.Length;
            while (cursor < trimmed.Length && (trimmed[cursor] == ' ' || trimmed[cursor] == '\t'))
            {
                cursor++;
            }

            return cursor == trimmed.Length || trimmed[cursor] == ':';
        }

        /// <summary>1 YAML line prefix の exact m_Script key と GUID を照合します。</summary>
        private static bool LineContainsSharedTableDataScriptGuid(byte[] prefix, int length)
        {
            var line = System.Text.Encoding.ASCII.GetString(prefix, 0, length).TrimEnd('\r');
            var trimmed = line.TrimStart(' ', '\t');
            const string scriptKey = "m_Script";
            if (!trimmed.StartsWith(scriptKey, StringComparison.Ordinal))
            {
                return false;
            }

            var cursor = scriptKey.Length;
            while (cursor < trimmed.Length && (trimmed[cursor] == ' ' || trimmed[cursor] == '\t'))
            {
                cursor++;
            }

            if (cursor >= trimmed.Length || trimmed[cursor] != ':')
            {
                return false;
            }

            var guidTagIndex = trimmed.IndexOf("guid:", cursor + 1, StringComparison.Ordinal);
            if (guidTagIndex < 0)
            {
                return false;
            }

            cursor = guidTagIndex + "guid:".Length;
            while (cursor < trimmed.Length && (trimmed[cursor] == ' ' || trimmed[cursor] == '\t'))
            {
                cursor++;
            }

            return cursor + SharedTableDataScriptGuid.Length <= trimmed.Length &&
                string.Compare(
                    trimmed,
                    cursor,
                    SharedTableDataScriptGuid,
                    0,
                    SharedTableDataScriptGuid.Length,
                    StringComparison.OrdinalIgnoreCase) == 0;
        }

        /// <summary>candidate path の path-to-physical mapping を一意に保ちます。</summary>
        internal static void AddCandidatePath(
            IDictionary<string, string> candidates,
            string assetPath,
            string physicalPath)
        {
            if (candidates.TryGetValue(assetPath, out var existing))
            {
                if (!string.Equals(existing, physicalPath, GetPhysicalPathComparison()))
                {
                    throw new InvalidDataException($"1 asset path が複数 physical file に対応しています: {assetPath}");
                }

                return;
            }

            if (candidates.Count >= LocalizationKeyAuditLimits.MaximumSharedTableDataAssets)
            {
                throw new InvalidDataException(
                    $"SharedTableData candidate 数が上限 {LocalizationKeyAuditLimits.MaximumSharedTableDataAssets} 件を超えています。");
            }

            candidates.Add(assetPath, physicalPath);
        }

        /// <summary>1 candidate を reparse/存在/size/read 状態付き raw asset にします。</summary>
        private static LocalizationKeyAuditRawAsset ReadCandidate(
            string assetPath,
            string physicalPath,
            ref long actualReadBytes)
        {
            try
            {
                var exists = File.Exists(physicalPath);
                if (!exists)
                {
                    return new LocalizationKeyAuditRawAsset(
                        assetPath,
                        physicalPath,
                        Array.Empty<byte>(),
                        exists: false);
                }

                var root = GetContainingRoot(assetPath);
                var hasReparsePoint = ContainsReparsePoint(root, physicalPath);
                if (hasReparsePoint)
                {
                    return new LocalizationKeyAuditRawAsset(
                        assetPath,
                        physicalPath,
                        Array.Empty<byte>(),
                        hasReparsePoint: true);
                }

                using (var stream = new FileStream(
                           physicalPath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read,
                           65536,
                           FileOptions.SequentialScan))
                {
                    if (stream.Length > LocalizationKeyAuditLimits.MaximumRawAssetBytes)
                    {
                        return new LocalizationKeyAuditRawAsset(
                            assetPath,
                            physicalPath,
                            Array.Empty<byte>(),
                            isOversize: true);
                    }

                    actualReadBytes = EnsureActualReadBudget(actualReadBytes, stream.Length);
                    var bytes = new byte[(int)stream.Length];
                    var offset = 0;
                    while (offset < bytes.Length)
                    {
                        var read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read == 0)
                        {
                            throw new EndOfStreamException("SharedTableData file が読み取り中に短くなりました。");
                        }

                        offset += read;
                    }

                    if (stream.ReadByte() != -1)
                    {
                        throw new IOException("SharedTableData file が読み取り中に変化しました。");
                    }

                    return new LocalizationKeyAuditRawAsset(assetPath, physicalPath, bytes);
                }
            }
            catch (LocalizationKeyAuditLimitException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return new LocalizationKeyAuditRawAsset(
                    assetPath,
                    physicalPath,
                    Array.Empty<byte>(),
                    exists: File.Exists(physicalPath),
                    readError: exception.GetType().Name);
            }
        }

        /// <summary>discovery後に増大したraw fileもallocation前のactual aggregate上限で拒否します。</summary>
        internal static long EnsureActualReadBudget(long bytesAlreadyRead, long nextFileBytes)
        {
            if (bytesAlreadyRead < 0 || nextFileBytes < 0 ||
                bytesAlreadyRead > LocalizationKeyAuditLimits.MaximumTotalRawBytes ||
                nextFileBytes > LocalizationKeyAuditLimits.MaximumTotalRawBytes - bytesAlreadyRead)
            {
                throw new LocalizationKeyAuditLimitException(
                    $"raw actual read byte 数が上限 {LocalizationKeyAuditLimits.MaximumTotalRawBytes} を超えています。");
            }

            return bytesAlreadyRead + nextFileBytes;
        }

        /// <summary>Unity asset path を registered root 内の absolute physical path へ変換します。</summary>
        private static string ResolvePhysicalPath(string assetPath)
        {
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return CombineInsideRoot(
                    Path.GetFullPath(Application.dataPath),
                    assetPath.Substring("Assets/".Length));
            }

            if (!assetPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unity asset path ではありません: {assetPath}");
            }

            var package = PackageManagerPackageInfo.FindForAssetPath(assetPath);
            if (package == null || string.IsNullOrWhiteSpace(package.name) || string.IsNullOrWhiteSpace(package.resolvedPath))
            {
                throw new InvalidDataException($"registered package root を解決できません: {assetPath}");
            }

            var prefix = "Packages/" + package.name + "/";
            if (!assetPath.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"package asset path と package name が一致しません: {assetPath}");
            }

            return CombineInsideRoot(
                Path.GetFullPath(package.resolvedPath),
                assetPath.Substring(prefix.Length));
        }

        /// <summary>asset path に対応する Assets または package physical root を返します。</summary>
        private static string GetContainingRoot(string assetPath)
        {
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return Path.GetFullPath(Application.dataPath);
            }

            var package = PackageManagerPackageInfo.FindForAssetPath(assetPath);
            if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath))
            {
                throw new InvalidDataException($"candidate の package root を解決できません: {assetPath}");
            }

            return Path.GetFullPath(package.resolvedPath);
        }

        /// <summary>root 外へ出ない relative path 結合を行います。</summary>
        private static string CombineInsideRoot(string root, string relativePath)
        {
            var fullRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var prefix = fullRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, GetPhysicalPathComparison()))
            {
                throw new InvalidDataException($"physical path が registered root の外を指しています: {fullPath}");
            }

            return fullPath;
        }

        /// <summary>root から file までに reparse point が 1 件でもあるかを調べます。</summary>
        private static bool ContainsReparsePoint(string root, string physicalPath)
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullPath = Path.GetFullPath(physicalPath);
            var prefix = fullRoot + Path.DirectorySeparatorChar;
            if (!string.Equals(fullRoot, fullPath, GetPhysicalPathComparison()) &&
                !fullPath.StartsWith(prefix, GetPhysicalPathComparison()))
            {
                throw new InvalidDataException($"physical path が scan root の外を指しています: {fullPath}");
            }

            if (HasReparsePoint(fullRoot))
            {
                return true;
            }

            var relative = Path.GetRelativePath(fullRoot, fullPath);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var current = fullRoot;
            for (var index = 0; index < segments.Length; index++)
            {
                if (segments[index].Length == 0 || segments[index] == ".")
                {
                    continue;
                }

                current = Path.Combine(current, segments[index]);
                if (HasReparsePoint(current))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>1 physical path の ReparsePoint attribute を調べます。</summary>
        private static bool HasReparsePoint(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }

        /// <summary>Unity が import 対象外にする dot/tilde name かを調べます。</summary>
        private static bool ShouldIgnorePathName(string name)
        {
            return string.IsNullOrEmpty(name) ||
                name.StartsWith(".", StringComparison.Ordinal) ||
                name.EndsWith("~", StringComparison.Ordinal);
        }

        /// <summary>OS の filesystem case rule に合わせた比較方法です。</summary>
        private static StringComparison GetPhysicalPathComparison()
        {
            return Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        /// <summary>OS の filesystem case rule に合わせた comparer です。</summary>
        private static StringComparer GetPhysicalPathComparer()
        {
            return Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }

        /// <summary>registered package を name、resolved path の順に並べます。</summary>
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

        /// <summary>physical discovery root と Unity asset prefix の組です。</summary>
        private readonly struct DiscoveryRoot
        {
            /// <summary>root pair を保持します。</summary>
            internal DiscoveryRoot(string assetPrefix, string physicalRoot)
            {
                AssetPrefix = assetPrefix;
                PhysicalRoot = physicalRoot;
            }

            /// <summary>Assets または Packages/package-name です。</summary>
            internal string AssetPrefix { get; }

            /// <summary>対応する absolute directory です。</summary>
            internal string PhysicalRoot { get; }
        }
    }
}
