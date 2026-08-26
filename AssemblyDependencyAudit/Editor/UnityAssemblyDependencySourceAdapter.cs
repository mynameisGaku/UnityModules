using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using RegisteredPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// AssetDatabase と物理 file を使って現 project の asmdef と asmref を取得します。
    /// </summary>
    internal sealed class UnityAssemblyDependencySourceAdapter : IAssemblyDependencySourceAdapter, IAssemblyReferenceSourceAdapter
    {
        /// <summary>BOMの有無を許可し、不正byteを拒否するUTF-8 reader設定です。</summary>
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>assembly asset探索で訪問できるdirectory数です。</summary>
        internal const int MaximumAssemblyAssetDirectories = 100000;

        /// <summary>assembly asset探索で確認できるfile entry数です。</summary>
        internal const int MaximumAssemblyAssetFileEntries = 500000;

        /// <summary>1件のassembly assetまたはmetaで読めるbyte数です。</summary>
        internal const long MaximumAssemblyAssetSourceBytes = 1048576;

        /// <summary>asmdefまたはasmrefの各読取phaseで読めるassetとmetaの総byte数です。</summary>
        internal const long MaximumAssemblyAssetTotalBytes = 67108864;

        /// <summary>assembly assetの物理pathを解決できなかった理由です。</summary>
        private enum PhysicalFileResolutionFailure
        {
            /// <summary>失敗はありません。</summary>
            None,

            /// <summary>fileが存在しない、または承認済みrootへ対応付けられません。</summary>
            Unavailable,

            /// <summary>承認済みroot配下のreparse pointを通ります。</summary>
            UnsafeReparsePath
        }

        /// <summary>
        /// AssetDatabase と Assets・登録済みPackagesの物理列挙をunionし、全asmdefを読み取ります。
        /// 1件でも列挙または読取に失敗した場合は部分的な一覧を返しません。
        /// </summary>
        public bool TryReadAll(
            out IReadOnlyList<AssemblyDefinitionSource> sources,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            sources = Array.Empty<AssemblyDefinitionSource>();
            error = AssemblyDependencyAuditError.None;
            errorMessage = string.Empty;

            try
            {
                if (!TryCollectTypedAssetPaths(out var typedAssetPaths, out error, out errorMessage))
                {
                    return false;
                }

                if (!TryCollectSearchRoots(out var searchRoots, out errorMessage))
                {
                    error = AssemblyDependencyAuditError.SourceUnavailable;
                    return false;
                }

                if (!TryCollectPhysicalAssetPaths(
                        searchRoots,
                        ".asmdef",
                        out var physicalAssetPaths,
                        out error,
                        out errorMessage))
                {
                    return false;
                }

                var assetPaths = AssemblyDefinitionSourcePathUtility.MergeAssetPaths(typedAssetPaths, physicalAssetPaths);
                if (assetPaths.Count > AssemblyDependencyAnalyzer.MaximumAssemblyDefinitions)
                {
                    error = AssemblyDependencyAuditError.TooManyAssemblyDefinitions;
                    errorMessage = $"asmdef 数が上限 {AssemblyDependencyAnalyzer.MaximumAssemblyDefinitions} 件を超えています。";
                    return false;
                }

                var collected = new List<AssemblyDefinitionSource>(assetPaths.Count);
                long totalBytes = 0;
                for (var index = 0; index < assetPaths.Count; index++)
                {
                    var assetPath = assetPaths[index];
                    if (!TryResolvePhysicalFile(assetPath, searchRoots, out var physicalPath, out var resolutionFailure))
                    {
                        SetPhysicalResolutionError(assetPath, resolutionFailure, out error, out errorMessage);
                        return false;
                    }

                    if (!TryReadBoundedRequiredUtf8(
                            physicalPath,
                            ref totalBytes,
                            out var json,
                            out error,
                            out var readError))
                    {
                        errorMessage = $"{assetPath} を読み取れませんでした: {readError}";
                        return false;
                    }

                    var guid = AssetDatabase.AssetPathToGUID(assetPath) ?? string.Empty;
                    if (!TryReadBoundedOptionalUtf8(
                            physicalPath + ".meta",
                            ref totalBytes,
                            out var metaExists,
                            out var metaText,
                            out error,
                            out var metaError))
                    {
                        errorMessage = $"{assetPath}.meta を読み取れませんでした: {metaError}";
                        return false;
                    }

                    if (metaExists)
                    {
                        if (!AssemblyDefinitionSourcePathUtility.TryExtractExactlyOneGuidFromMeta(metaText, out var rawGuid))
                        {
                            error = AssemblyDependencyAuditError.SourceUnavailable;
                            errorMessage = $"{assetPath}.meta にexactly-oneの有効なguid fieldがありません。";
                            return false;
                        }

                        guid = rawGuid;
                    }

                    collected.Add(new AssemblyDefinitionSource(assetPath, guid, json));
                }

                sources = collected.AsReadOnly();
                return true;
            }
            catch (Exception exception)
            {
                sources = Array.Empty<AssemblyDefinitionSource>();
                error = AssemblyDependencyAuditError.SourceUnavailable;
                errorMessage = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// AssetDatabase と Assets・登録済みPackagesの物理列挙をunionし、全asmrefを読み取ります。
        /// JSON と任意metaは厳密UTF-8で読み、metaに有効な生GUIDがあれば優先します。
        /// </summary>
        public bool TryReadAllAssemblyReferences(
            out IReadOnlyList<AssemblyReferenceSource> sources,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            sources = Array.Empty<AssemblyReferenceSource>();
            error = AssemblyDependencyAuditError.None;
            errorMessage = string.Empty;

            try
            {
                if (!TryCollectAssemblyReferenceTypedAssetPaths(out var typedAssetPaths, out error, out errorMessage) ||
                    !TryCollectSearchRoots(out var searchRoots, out errorMessage))
                {
                    if (error == AssemblyDependencyAuditError.None)
                    {
                        error = AssemblyDependencyAuditError.SourceUnavailable;
                    }

                    return false;
                }

                if (!TryCollectPhysicalAssemblyReferenceAssetPaths(
                        searchRoots,
                        out var physicalAssetPaths,
                        out error,
                        out errorMessage))
                {
                    return false;
                }

                var assetPaths = AssemblyDefinitionSourcePathUtility.MergeAssemblyReferenceAssetPaths(
                    typedAssetPaths,
                    physicalAssetPaths);
                if (assetPaths.Count > AssemblyReferenceAnalyzer.MaximumAssemblyReferences)
                {
                    error = AssemblyDependencyAuditError.TooManyAssemblyReferences;
                    errorMessage = $"asmref 数が上限 {AssemblyReferenceAnalyzer.MaximumAssemblyReferences} 件を超えています。";
                    return false;
                }

                var collected = new List<AssemblyReferenceSource>(assetPaths.Count);
                long totalBytes = 0;
                for (var index = 0; index < assetPaths.Count; index++)
                {
                    var assetPath = assetPaths[index];
                    if (!TryResolveAssemblyReferencePhysicalFile(
                            assetPath,
                            searchRoots,
                            out var physicalPath,
                            out var resolutionFailure))
                    {
                        SetPhysicalResolutionError(assetPath, resolutionFailure, out error, out errorMessage);
                        return false;
                    }

                    if (!TryReadBoundedRequiredUtf8(
                            physicalPath,
                            ref totalBytes,
                            out var json,
                            out error,
                            out var readError))
                    {
                        errorMessage = $"{assetPath} を読み取れませんでした: {readError}";
                        return false;
                    }

                    var guid = AssetDatabase.AssetPathToGUID(assetPath) ?? string.Empty;
                    if (!TryReadBoundedOptionalUtf8(
                            physicalPath + ".meta",
                            ref totalBytes,
                            out var metaExists,
                            out var metaText,
                            out error,
                            out var metaError))
                    {
                        errorMessage = $"{assetPath}.meta を読み取れませんでした: {metaError}";
                        return false;
                    }

                    if (metaExists)
                    {
                        if (!AssemblyDefinitionSourcePathUtility.TryExtractExactlyOneGuidFromMeta(metaText, out var rawGuid))
                        {
                            error = AssemblyDependencyAuditError.SourceUnavailable;
                            errorMessage = $"{assetPath}.meta にexactly-oneの有効なguid fieldがありません。";
                            return false;
                        }

                        guid = rawGuid;
                    }

                    collected.Add(new AssemblyReferenceSource(assetPath, guid, json));
                }

                sources = collected.AsReadOnly();
                return true;
            }
            catch (Exception exception)
            {
                sources = Array.Empty<AssemblyReferenceSource>();
                error = AssemblyDependencyAuditError.SourceUnavailable;
                errorMessage = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Unity compiler が解決した asmdef path を返します。例外時は false を返します。
        /// </summary>
        public bool TryResolveReferencePath(string reference, out string assetPath)
        {
            try
            {
                assetPath = AssemblyDefinitionSourcePathUtility.NormalizeAssetPath(
                    CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyReference(reference));
                return !string.IsNullOrEmpty(assetPath);
            }
            catch (Exception)
            {
                assetPath = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// AssetDatabase が型として認識した asmdef path を取得します。
        /// GUIDからpathを戻せない項目が1件でもあれば失敗します。
        /// </summary>
        private static bool TryCollectTypedAssetPaths(
            out IReadOnlyList<string> assetPaths,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            var collected = new List<string>();
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            if (guids == null)
            {
                assetPaths = Array.Empty<string>();
                error = AssemblyDependencyAuditError.SourceUnavailable;
                errorMessage = "AssetDatabase から asmdef 一覧を取得できませんでした。";
                return false;
            }

            if (guids.Length > AssemblyDependencyAnalyzer.MaximumAssemblyDefinitions)
            {
                assetPaths = Array.Empty<string>();
                error = AssemblyDependencyAuditError.TooManyAssemblyDefinitions;
                errorMessage = $"AssetDatabaseのasmdef数が上限 {AssemblyDependencyAnalyzer.MaximumAssemblyDefinitions} 件を超えています。";
                return false;
            }

            for (var index = 0; index < guids.Length; index++)
            {
                var guid = guids[index] ?? string.Empty;
                var assetPath = AssemblyDefinitionSourcePathUtility.NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(assetPath))
                {
                    assetPaths = Array.Empty<string>();
                    error = AssemblyDependencyAuditError.SourceUnavailable;
                    errorMessage = "AssetDatabase の asmdef GUID から asset path を取得できませんでした。";
                    return false;
                }

                collected.Add(assetPath);
            }

            assetPaths = collected.AsReadOnly();
            error = AssemblyDependencyAuditError.None;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// AssetDatabase が型として認識した asmref path を取得します。
        /// GUIDからpathを戻せない項目が1件でもあれば失敗します。
        /// </summary>
        private static bool TryCollectAssemblyReferenceTypedAssetPaths(
            out IReadOnlyList<string> assetPaths,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            var collected = new List<string>();
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionReferenceAsset");
            if (guids == null)
            {
                assetPaths = Array.Empty<string>();
                error = AssemblyDependencyAuditError.SourceUnavailable;
                errorMessage = "AssetDatabase から asmref 一覧を取得できませんでした。";
                return false;
            }

            if (guids.Length > AssemblyReferenceAnalyzer.MaximumAssemblyReferences)
            {
                assetPaths = Array.Empty<string>();
                error = AssemblyDependencyAuditError.TooManyAssemblyReferences;
                errorMessage = $"AssetDatabaseのasmref数が上限 {AssemblyReferenceAnalyzer.MaximumAssemblyReferences} 件を超えています。";
                return false;
            }

            for (var index = 0; index < guids.Length; index++)
            {
                var guid = guids[index] ?? string.Empty;
                var assetPath = AssemblyDefinitionSourcePathUtility.NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(assetPath))
                {
                    assetPaths = Array.Empty<string>();
                    error = AssemblyDependencyAuditError.SourceUnavailable;
                    errorMessage = "AssetDatabase の asmref GUID から asset path を取得できませんでした。";
                    return false;
                }

                collected.Add(assetPath);
            }

            assetPaths = collected.AsReadOnly();
            error = AssemblyDependencyAuditError.None;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Assets と登録済みPackageごとの論理root・物理rootを決定論的に取得します。
        /// </summary>
        private static bool TryCollectSearchRoots(out IReadOnlyList<SearchRoot> searchRoots, out string errorMessage)
        {
            var collected = new List<SearchRoot>();
            var rootPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!TryAddSearchRoot("Assets", FileUtil.GetPhysicalPath("Assets"), rootPaths, collected, out errorMessage))
            {
                searchRoots = Array.Empty<SearchRoot>();
                return false;
            }

            var packages = RegisteredPackageInfo.GetAllRegisteredPackages();
            if (packages == null)
            {
                searchRoots = Array.Empty<SearchRoot>();
                errorMessage = "登録済みPackage一覧を取得できませんでした。";
                return false;
            }

            Array.Sort(packages, ComparePackages);
            for (var index = 0; index < packages.Length; index++)
            {
                var package = packages[index];
                if (package == null)
                {
                    searchRoots = Array.Empty<SearchRoot>();
                    errorMessage = "登録済みPackage一覧にnullが含まれています。";
                    return false;
                }

                var assetPath = AssemblyDefinitionSourcePathUtility.NormalizeAssetPath(package.assetPath).TrimEnd('/');
                var physicalPath = string.IsNullOrEmpty(package.resolvedPath)
                    ? FileUtil.GetPhysicalPath(assetPath)
                    : package.resolvedPath;
                if (!TryAddSearchRoot(assetPath, physicalPath, rootPaths, collected, out errorMessage))
                {
                    searchRoots = Array.Empty<SearchRoot>();
                    return false;
                }
            }

            collected.Sort((left, right) => string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal));
            searchRoots = collected.AsReadOnly();
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// 1つの検索rootを検証し、同じ論理rootの物理path不一致を拒否します。
        /// </summary>
        private static bool TryAddSearchRoot(
            string assetPath,
            string physicalPath,
            IDictionary<string, string> rootPaths,
            ICollection<SearchRoot> roots,
            out string errorMessage)
        {
            var normalizedAssetPath = AssemblyDefinitionSourcePathUtility.NormalizeAssetPath(assetPath).TrimEnd('/');
            if (!string.Equals(normalizedAssetPath, "Assets", StringComparison.Ordinal) &&
                !normalizedAssetPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                errorMessage = $"監査対象外のroot pathです: {normalizedAssetPath}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(physicalPath))
            {
                errorMessage = $"{normalizedAssetPath} の物理rootを解決できませんでした。";
                return false;
            }

            var fullPhysicalPath = Path.GetFullPath(physicalPath);
            if (!Directory.Exists(fullPhysicalPath))
            {
                errorMessage = $"{normalizedAssetPath} の物理rootがありません: {fullPhysicalPath}";
                return false;
            }

            if (rootPaths.TryGetValue(normalizedAssetPath, out var existingPhysicalPath))
            {
                if (!string.Equals(existingPhysicalPath, fullPhysicalPath, GetPhysicalPathComparison()))
                {
                    errorMessage = $"{normalizedAssetPath} に複数の物理rootがあります。";
                    return false;
                }

                errorMessage = string.Empty;
                return true;
            }

            rootPaths.Add(normalizedAssetPath, fullPhysicalPath);
            roots.Add(new SearchRoot(normalizedAssetPath, fullPhysicalPath));
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// 各rootを再帰なしで列挙し、dot始まり・末尾tilde・reparse directoryを降りません。
        /// </summary>
        private static bool TryCollectPhysicalAssetPaths(
            IReadOnlyList<SearchRoot> searchRoots,
            string extension,
            out IReadOnlyList<string> assetPaths,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            var collected = new SortedSet<string>(StringComparer.Ordinal);
            var directoryCount = 0;
            var fileEntryCount = 0;
            var maximumSources = string.Equals(extension, ".asmref", StringComparison.OrdinalIgnoreCase)
                ? AssemblyReferenceAnalyzer.MaximumAssemblyReferences
                : AssemblyDependencyAnalyzer.MaximumAssemblyDefinitions;
            error = AssemblyDependencyAuditError.None;
            try
            {
                for (var rootIndex = 0; rootIndex < searchRoots.Count; rootIndex++)
                {
                    directoryCount++;
                    if (directoryCount > MaximumAssemblyAssetDirectories)
                    {
                        assetPaths = Array.Empty<string>();
                        error = AssemblyDependencyAuditError.AssemblyAssetTraversalLimitExceeded;
                        errorMessage = $"source探索directory数が上限 {MaximumAssemblyAssetDirectories} を超えています。";
                        return false;
                    }

                    var root = searchRoots[rootIndex];
                    var directories = new Stack<string>();
                    directories.Push(root.PhysicalPath);
                    while (directories.Count > 0)
                    {
                        var current = directories.Pop();
                        foreach (var file in Directory.EnumerateFiles(current, "*", SearchOption.TopDirectoryOnly))
                        {
                            fileEntryCount++;
                            if (fileEntryCount > MaximumAssemblyAssetFileEntries)
                            {
                                assetPaths = Array.Empty<string>();
                                error = AssemblyDependencyAuditError.AssemblyAssetTraversalLimitExceeded;
                                errorMessage = $"source探索file entry数が上限 {MaximumAssemblyAssetFileEntries} を超えています。";
                                return false;
                            }

                            if (AssemblyDefinitionSourcePathUtility.IsIgnoredFileName(Path.GetFileName(file)) ||
                                !string.Equals(Path.GetExtension(file), extension, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                            {
                                assetPaths = Array.Empty<string>();
                                error = AssemblyDependencyAuditError.SourceUnavailable;
                                errorMessage = $"reparse pointのassembly asset fileは監査できません: {file}";
                                return false;
                            }

                            string assetPath;
                            bool mapped;
                            if (string.Equals(extension, ".asmref", StringComparison.OrdinalIgnoreCase))
                            {
                                mapped = AssemblyDefinitionSourcePathUtility.TryMapPhysicalAssemblyReferenceFileToAssetPath(
                                    root.AssetPath,
                                    root.PhysicalPath,
                                    file,
                                    out assetPath);
                            }
                            else
                            {
                                mapped = AssemblyDefinitionSourcePathUtility.TryMapPhysicalFileToAssetPath(
                                    root.AssetPath,
                                    root.PhysicalPath,
                                    file,
                                    out assetPath);
                            }

                            if (!mapped)
                            {
                                assetPaths = Array.Empty<string>();
                                error = AssemblyDependencyAuditError.SourceUnavailable;
                                errorMessage = $"{extension} の物理pathをasset pathへ変換できませんでした: {file}";
                                return false;
                            }

                            collected.Add(assetPath);
                            if (collected.Count > maximumSources)
                            {
                                assetPaths = Array.Empty<string>();
                                error = string.Equals(extension, ".asmref", StringComparison.OrdinalIgnoreCase)
                                    ? AssemblyDependencyAuditError.TooManyAssemblyReferences
                                    : AssemblyDependencyAuditError.TooManyAssemblyDefinitions;
                                errorMessage = $"{extension}数が上限 {maximumSources} 件を超えています。";
                                return false;
                            }
                        }

                        foreach (var child in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
                        {
                            directoryCount++;
                            if (directoryCount > MaximumAssemblyAssetDirectories)
                            {
                                assetPaths = Array.Empty<string>();
                                error = AssemblyDependencyAuditError.AssemblyAssetTraversalLimitExceeded;
                                errorMessage = $"source探索directory数が上限 {MaximumAssemblyAssetDirectories} を超えています。";
                                return false;
                            }

                            var childAttributes = File.GetAttributes(child);
                            if (AssemblyDefinitionSourcePathUtility.IsIgnoredDirectoryName(Path.GetFileName(child)) ||
                                (childAttributes & (FileAttributes.ReparsePoint | FileAttributes.Hidden)) != 0)
                            {
                                continue;
                            }

                            directories.Push(child);
                        }
                    }
                }

                assetPaths = new List<string>(collected).AsReadOnly();
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                assetPaths = Array.Empty<string>();
                error = AssemblyDependencyAuditError.SourceUnavailable;
                errorMessage = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// asmrefだけをstreaming列挙し、directory・file entry・件数の上限内で収集します。
        /// </summary>
        private static bool TryCollectPhysicalAssemblyReferenceAssetPaths(
            IReadOnlyList<SearchRoot> searchRoots,
            out IReadOnlyList<string> assetPaths,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            var collected = new SortedSet<string>(StringComparer.Ordinal);
            var directoryCount = 0;
            var fileEntryCount = 0;
            error = AssemblyDependencyAuditError.None;
            try
            {
                for (var rootIndex = 0; rootIndex < searchRoots.Count; rootIndex++)
                {
                    directoryCount++;
                    if (directoryCount > MaximumAssemblyAssetDirectories)
                    {
                        return FailTraversalLimit(
                            "asmref探索directory数",
                            MaximumAssemblyAssetDirectories,
                            out assetPaths,
                            out error,
                            out errorMessage);
                    }

                    var root = searchRoots[rootIndex];
                    var directories = new Stack<string>();
                    directories.Push(root.PhysicalPath);
                    while (directories.Count > 0)
                    {
                        var current = directories.Pop();
                        foreach (var file in Directory.EnumerateFiles(current, "*", SearchOption.TopDirectoryOnly))
                        {
                            fileEntryCount++;
                            if (fileEntryCount > MaximumAssemblyAssetFileEntries)
                            {
                                return FailTraversalLimit(
                                    "asmref探索file entry数",
                                    MaximumAssemblyAssetFileEntries,
                                    out assetPaths,
                                    out error,
                                    out errorMessage);
                            }

                            if (AssemblyDefinitionSourcePathUtility.IsIgnoredFileName(Path.GetFileName(file)) ||
                                !string.Equals(Path.GetExtension(file), ".asmref", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                            {
                                assetPaths = Array.Empty<string>();
                                error = AssemblyDependencyAuditError.SourceUnavailable;
                                errorMessage = $"reparse pointのasmref fileは監査できません: {file}";
                                return false;
                            }

                            if (!AssemblyDefinitionSourcePathUtility.TryMapPhysicalAssemblyReferenceFileToAssetPath(
                                    root.AssetPath,
                                    root.PhysicalPath,
                                    file,
                                    out var assetPath))
                            {
                                assetPaths = Array.Empty<string>();
                                error = AssemblyDependencyAuditError.SourceUnavailable;
                                errorMessage = $"asmref の物理pathをasset pathへ変換できませんでした: {file}";
                                return false;
                            }

                            collected.Add(assetPath);
                            if (collected.Count > AssemblyReferenceAnalyzer.MaximumAssemblyReferences)
                            {
                                assetPaths = Array.Empty<string>();
                                error = AssemblyDependencyAuditError.TooManyAssemblyReferences;
                                errorMessage = $"物理asmref数が上限 {AssemblyReferenceAnalyzer.MaximumAssemblyReferences} 件を超えています。";
                                return false;
                            }
                        }

                        foreach (var child in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
                        {
                            directoryCount++;
                            if (directoryCount > MaximumAssemblyAssetDirectories)
                            {
                                return FailTraversalLimit(
                                    "asmref探索directory数",
                                    MaximumAssemblyAssetDirectories,
                                    out assetPaths,
                                    out error,
                                    out errorMessage);
                            }

                            var childAttributes = File.GetAttributes(child);
                            if (AssemblyDefinitionSourcePathUtility.IsIgnoredDirectoryName(Path.GetFileName(child)) ||
                                (childAttributes & (FileAttributes.ReparsePoint | FileAttributes.Hidden)) != 0)
                            {
                                continue;
                            }

                            directories.Push(child);
                        }
                    }
                }

                assetPaths = new List<string>(collected).AsReadOnly();
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                assetPaths = Array.Empty<string>();
                error = AssemblyDependencyAuditError.SourceUnavailable;
                errorMessage = exception.Message;
                return false;
            }
        }

        /// <summary>探索上限超過をtyped errorとして返します。</summary>
        private static bool FailTraversalLimit(
            string label,
            int limit,
            out IReadOnlyList<string> assetPaths,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            assetPaths = Array.Empty<string>();
            error = AssemblyDependencyAuditError.AssemblyAssetTraversalLimitExceeded;
            errorMessage = $"{label}が上限 {limit} を超えています。";
            return false;
        }

        /// <summary>物理pathの解決失敗を監査全体のtyped errorへ変換します。</summary>
        private static void SetPhysicalResolutionError(
            string assetPath,
            PhysicalFileResolutionFailure failure,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            if (failure == PhysicalFileResolutionFailure.UnsafeReparsePath)
            {
                error = AssemblyDependencyAuditError.UnsafeAssemblyAssetPath;
                errorMessage =
                    $"{assetPath} は承認済みroot配下のreparse pointを通るため安全に監査できません。" +
                    "結果の完全性を保つためRefreshを停止しました。";
                return;
            }

            error = AssemblyDependencyAuditError.SourceUnavailable;
            errorMessage = $"{assetPath} の物理fileを解決できませんでした。";
        }

        /// <summary>
        /// FileUtilを優先し、未import fileでは登録rootから物理pathを再構築します。
        /// </summary>
        private static bool TryResolvePhysicalFile(
            string assetPath,
            IReadOnlyList<SearchRoot> searchRoots,
            out string physicalPath,
            out PhysicalFileResolutionFailure failure)
        {
            physicalPath = string.Empty;
            failure = PhysicalFileResolutionFailure.Unavailable;
            var rejectedReparsePath = false;
            var resolved = FileUtil.GetPhysicalPath(assetPath);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                var fullResolved = Path.GetFullPath(resolved);
                if (File.Exists(fullResolved))
                {
                    if (IsSafePhysicalFile(
                            assetPath,
                            fullResolved,
                            searchRoots,
                            false,
                            out var isReparsePath))
                    {
                        physicalPath = fullResolved;
                        failure = PhysicalFileResolutionFailure.None;
                        return true;
                    }

                    rejectedReparsePath |= isReparsePath;
                }
            }

            for (var index = 0; index < searchRoots.Count; index++)
            {
                var root = searchRoots[index];
                if (AssemblyDefinitionSourcePathUtility.TryMapAssetPathToPhysicalFile(
                        root.AssetPath,
                        root.PhysicalPath,
                        assetPath,
                        out var candidate) &&
                    File.Exists(candidate))
                {
                    if (IsSafePhysicalFile(
                            assetPath,
                            candidate,
                            searchRoots,
                            false,
                            out var isReparsePath))
                    {
                        physicalPath = candidate;
                        failure = PhysicalFileResolutionFailure.None;
                        return true;
                    }

                    rejectedReparsePath |= isReparsePath;
                }
            }

            failure = rejectedReparsePath
                ? PhysicalFileResolutionFailure.UnsafeReparsePath
                : PhysicalFileResolutionFailure.Unavailable;
            return false;
        }

        /// <summary>
        /// FileUtilを優先し、未import asmrefでは登録rootから物理pathを再構築します。
        /// </summary>
        private static bool TryResolveAssemblyReferencePhysicalFile(
            string assetPath,
            IReadOnlyList<SearchRoot> searchRoots,
            out string physicalPath,
            out PhysicalFileResolutionFailure failure)
        {
            physicalPath = string.Empty;
            failure = PhysicalFileResolutionFailure.Unavailable;
            var rejectedReparsePath = false;
            var resolved = FileUtil.GetPhysicalPath(assetPath);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                var fullResolved = Path.GetFullPath(resolved);
                if (File.Exists(fullResolved))
                {
                    if (IsSafePhysicalFile(
                            assetPath,
                            fullResolved,
                            searchRoots,
                            true,
                            out var isReparsePath))
                    {
                        physicalPath = fullResolved;
                        failure = PhysicalFileResolutionFailure.None;
                        return true;
                    }

                    rejectedReparsePath |= isReparsePath;
                }
            }

            for (var index = 0; index < searchRoots.Count; index++)
            {
                var root = searchRoots[index];
                if (AssemblyDefinitionSourcePathUtility.TryMapAssemblyReferenceAssetPathToPhysicalFile(
                        root.AssetPath,
                        root.PhysicalPath,
                        assetPath,
                        out var candidate) &&
                    File.Exists(candidate))
                {
                    if (IsSafePhysicalFile(
                            assetPath,
                            candidate,
                            searchRoots,
                            true,
                            out var isReparsePath))
                    {
                        physicalPath = candidate;
                        failure = PhysicalFileResolutionFailure.None;
                        return true;
                    }

                    rejectedReparsePath |= isReparsePath;
                }
            }

            failure = rejectedReparsePath
                ? PhysicalFileResolutionFailure.UnsafeReparsePath
                : PhysicalFileResolutionFailure.Unavailable;
            return false;
        }

        /// <summary>
        /// fileが対応root内へround-tripし、root配下のreparse pointを通らないかを返します。
        /// </summary>
        private static bool IsSafePhysicalFile(
            string assetPath,
            string physicalPath,
            IReadOnlyList<SearchRoot> searchRoots,
            bool isAssemblyReference,
            out bool isReparsePath)
        {
            isReparsePath = false;
            try
            {
                if ((File.GetAttributes(physicalPath) & FileAttributes.ReparsePoint) != 0)
                {
                    isReparsePath = true;
                    return false;
                }

                var foundReparsePath = false;
                for (var index = 0; index < searchRoots.Count; index++)
                {
                    var root = searchRoots[index];
                    var mapped = isAssemblyReference
                        ? AssemblyDefinitionSourcePathUtility.TryMapPhysicalAssemblyReferenceFileToAssetPath(
                            root.AssetPath,
                            root.PhysicalPath,
                            physicalPath,
                            out var mappedAssetPath)
                        : AssemblyDefinitionSourcePathUtility.TryMapPhysicalFileToAssetPath(
                            root.AssetPath,
                            root.PhysicalPath,
                            physicalPath,
                            out mappedAssetPath);
                    if (!mapped || !string.Equals(mappedAssetPath, assetPath, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (HasReparseDirectoryBelowRoot(root.PhysicalPath, physicalPath))
                    {
                        foundReparsePath = true;
                        continue;
                    }

                    return true;
                }

                isReparsePath = foundReparsePath;
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>承認済みrootより下の祖先directoryにreparse pointがあるかを返します。</summary>
        private static bool HasReparseDirectoryBelowRoot(string rootPhysicalPath, string filePhysicalPath)
        {
            var fullRoot = Path.GetFullPath(rootPhysicalPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var current = Directory.GetParent(Path.GetFullPath(filePhysicalPath));
            while (current != null && !string.Equals(current.FullName, fullRoot, GetPhysicalPathComparison()))
            {
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }

                current = current.Parent;
            }

            return current == null;
        }

        /// <summary>必須assembly asset関連fileをbyte上限内で厳密UTF-8として読みます。</summary>
        private static bool TryReadBoundedRequiredUtf8(
            string physicalPath,
            ref long totalBytes,
            out string text,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            return TryReadBoundedUtf8(
                physicalPath,
                false,
                ref totalBytes,
                out _,
                out text,
                out error,
                out errorMessage);
        }

        /// <summary>任意metaをbyte上限内で厳密UTF-8として読みます。</summary>
        private static bool TryReadBoundedOptionalUtf8(
            string physicalPath,
            ref long totalBytes,
            out bool exists,
            out string text,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            return TryReadBoundedUtf8(
                physicalPath,
                true,
                ref totalBytes,
                out exists,
                out text,
                out error,
                out errorMessage);
        }

        /// <summary>
        /// stream.Lengthをallocation前に検証し、UTF-8 BOMだけを任意で除いてdecodeします。
        /// </summary>
        private static bool TryReadBoundedUtf8(
            string physicalPath,
            bool optional,
            ref long totalBytes,
            out bool exists,
            out string text,
            out AssemblyDependencyAuditError error,
            out string errorMessage)
        {
            exists = false;
            text = string.Empty;
            error = AssemblyDependencyAuditError.None;
            errorMessage = string.Empty;
            try
            {
                using (var stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    exists = true;
                    var length = stream.Length;
                    if (length > MaximumAssemblyAssetSourceBytes)
                    {
                        error = AssemblyDependencyAuditError.SourceTooLarge;
                        errorMessage = $"file sizeが上限 {MaximumAssemblyAssetSourceBytes} bytesを超えています。";
                        return false;
                    }

                    if (totalBytes > MaximumAssemblyAssetTotalBytes - length)
                    {
                        error = AssemblyDependencyAuditError.AssemblyAssetTotalBytesExceeded;
                        errorMessage = $"読取総量が上限 {MaximumAssemblyAssetTotalBytes} bytesを超えています。";
                        return false;
                    }

                    var bytes = new byte[(int)length];
                    var offset = 0;
                    while (offset < bytes.Length)
                    {
                        var read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read <= 0)
                        {
                            throw new EndOfStreamException("file lengthより前に読取が終了しました。");
                        }

                        offset += read;
                    }

                    var preambleLength = bytes.Length >= 3 &&
                        bytes[0] == 0xEF &&
                        bytes[1] == 0xBB &&
                        bytes[2] == 0xBF
                        ? 3
                        : 0;
                    text = StrictUtf8.GetString(bytes, preambleLength, bytes.Length - preambleLength);
                    totalBytes += length;
                    return true;
                }
            }
            catch (FileNotFoundException) when (optional)
            {
                return true;
            }
            catch (DirectoryNotFoundException) when (optional)
            {
                return true;
            }
            catch (Exception exception)
            {
                exists = false;
                text = string.Empty;
                error = AssemblyDependencyAuditError.SourceUnavailable;
                errorMessage = exception.Message;
                return false;
            }
        }

        /// <summary>packageをasset path順へ並べます。</summary>
        private static int ComparePackages(RegisteredPackageInfo left, RegisteredPackageInfo right)
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

            return string.Compare(left.assetPath, right.assetPath, StringComparison.Ordinal);
        }

        /// <summary>現在のOSに合わせた物理path比較規則を返します。</summary>
        private static StringComparison GetPhysicalPathComparison()
        {
            return Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        /// <summary>1つのUnity論理rootと対応する物理rootを保持します。</summary>
        private sealed class SearchRoot
        {
            /// <summary>rootの論理pathと物理pathを保持します。</summary>
            internal SearchRoot(string assetPath, string physicalPath)
            {
                AssetPath = assetPath;
                PhysicalPath = physicalPath;
            }

            /// <summary>AssetsまたはPackages/package-nameです。</summary>
            internal string AssetPath { get; }

            /// <summary>実fileを列挙する絶対directory pathです。</summary>
            internal string PhysicalPath { get; }
        }
    }
}
