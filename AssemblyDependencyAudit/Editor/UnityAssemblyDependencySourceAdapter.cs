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
    /// AssetDatabase と物理 file を使って現 project の asmdef を取得します。
    /// </summary>
    internal sealed class UnityAssemblyDependencySourceAdapter : IAssemblyDependencySourceAdapter
    {
        /// <summary>BOMの有無を許可し、不正byteを拒否するUTF-8 reader設定です。</summary>
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// AssetDatabase と Assets・登録済みPackagesの物理列挙をunionし、全asmdefを読み取ります。
        /// 1件でも列挙または読取に失敗した場合は部分的な一覧を返しません。
        /// </summary>
        public bool TryReadAll(out IReadOnlyList<AssemblyDefinitionSource> sources, out string errorMessage)
        {
            sources = Array.Empty<AssemblyDefinitionSource>();
            errorMessage = string.Empty;

            try
            {
                if (!TryCollectTypedAssetPaths(out var typedAssetPaths, out errorMessage) ||
                    !TryCollectSearchRoots(out var searchRoots, out errorMessage) ||
                    !TryCollectPhysicalAssetPaths(searchRoots, out var physicalAssetPaths, out errorMessage))
                {
                    return false;
                }

                var assetPaths = AssemblyDefinitionSourcePathUtility.MergeAssetPaths(typedAssetPaths, physicalAssetPaths);
                var collected = new List<AssemblyDefinitionSource>(assetPaths.Count);
                for (var index = 0; index < assetPaths.Count; index++)
                {
                    var assetPath = assetPaths[index];
                    if (!TryResolvePhysicalFile(assetPath, searchRoots, out var physicalPath))
                    {
                        errorMessage = $"{assetPath} の物理fileを解決できませんでした。";
                        return false;
                    }

                    if (!TryReadRequiredUtf8(physicalPath, out var json, out var readError))
                    {
                        errorMessage = $"{assetPath} を読み取れませんでした: {readError}";
                        return false;
                    }

                    var guid = AssetDatabase.AssetPathToGUID(assetPath) ?? string.Empty;
                    if (!TryReadOptionalUtf8(physicalPath + ".meta", out var metaExists, out var metaText, out var metaError))
                    {
                        errorMessage = $"{assetPath}.meta を読み取れませんでした: {metaError}";
                        return false;
                    }

                    if (metaExists && AssemblyDefinitionSourcePathUtility.TryExtractGuidFromMeta(metaText, out var rawGuid))
                    {
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
        private static bool TryCollectTypedAssetPaths(out IReadOnlyList<string> assetPaths, out string errorMessage)
        {
            var collected = new List<string>();
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            if (guids == null)
            {
                assetPaths = Array.Empty<string>();
                errorMessage = "AssetDatabase から asmdef 一覧を取得できませんでした。";
                return false;
            }

            for (var index = 0; index < guids.Length; index++)
            {
                var guid = guids[index] ?? string.Empty;
                var assetPath = AssemblyDefinitionSourcePathUtility.NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(assetPath))
                {
                    assetPaths = Array.Empty<string>();
                    errorMessage = "AssetDatabase の asmdef GUID から asset path を取得できませんでした。";
                    return false;
                }

                collected.Add(assetPath);
            }

            assetPaths = collected.AsReadOnly();
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
            out IReadOnlyList<string> assetPaths,
            out string errorMessage)
        {
            var collected = new List<string>();
            try
            {
                for (var rootIndex = 0; rootIndex < searchRoots.Count; rootIndex++)
                {
                    var root = searchRoots[rootIndex];
                    var directories = new Stack<string>();
                    directories.Push(root.PhysicalPath);
                    while (directories.Count > 0)
                    {
                        var current = directories.Pop();
                        var files = Directory.GetFiles(current, "*", SearchOption.TopDirectoryOnly);
                        Array.Sort(files, StringComparer.Ordinal);
                        for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
                        {
                            if (!string.Equals(Path.GetExtension(files[fileIndex]), ".asmdef", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (!AssemblyDefinitionSourcePathUtility.TryMapPhysicalFileToAssetPath(
                                    root.AssetPath,
                                    root.PhysicalPath,
                                    files[fileIndex],
                                    out var assetPath))
                            {
                                assetPaths = Array.Empty<string>();
                                errorMessage = $"asmdef の物理pathをasset pathへ変換できませんでした: {files[fileIndex]}";
                                return false;
                            }

                            collected.Add(assetPath);
                        }

                        var childDirectories = Directory.GetDirectories(current, "*", SearchOption.TopDirectoryOnly);
                        Array.Sort(childDirectories, StringComparer.Ordinal);
                        for (var directoryIndex = childDirectories.Length - 1; directoryIndex >= 0; directoryIndex--)
                        {
                            var child = childDirectories[directoryIndex];
                            if (AssemblyDefinitionSourcePathUtility.IsIgnoredDirectoryName(Path.GetFileName(child)) ||
                                (File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                            {
                                continue;
                            }

                            directories.Push(child);
                        }
                    }
                }

                assetPaths = collected.AsReadOnly();
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                assetPaths = Array.Empty<string>();
                errorMessage = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// FileUtilを優先し、未import fileでは登録rootから物理pathを再構築します。
        /// </summary>
        private static bool TryResolvePhysicalFile(
            string assetPath,
            IReadOnlyList<SearchRoot> searchRoots,
            out string physicalPath)
        {
            physicalPath = string.Empty;
            var resolved = FileUtil.GetPhysicalPath(assetPath);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                var fullResolved = Path.GetFullPath(resolved);
                if (File.Exists(fullResolved))
                {
                    physicalPath = fullResolved;
                    return true;
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
                    physicalPath = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>必須fileを厳密UTF-8で読み取ります。</summary>
        private static bool TryReadRequiredUtf8(string physicalPath, out string text, out string errorMessage)
        {
            try
            {
                text = File.ReadAllText(physicalPath, StrictUtf8);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                text = string.Empty;
                errorMessage = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 任意fileが無ければ成功として空を返し、存在するfileを読めなければ失敗します。
        /// </summary>
        private static bool TryReadOptionalUtf8(
            string physicalPath,
            out bool exists,
            out string text,
            out string errorMessage)
        {
            try
            {
                File.GetAttributes(physicalPath);
                exists = true;
                text = File.ReadAllText(physicalPath, StrictUtf8);
                errorMessage = string.Empty;
                return true;
            }
            catch (FileNotFoundException)
            {
                exists = false;
                text = string.Empty;
                errorMessage = string.Empty;
                return true;
            }
            catch (DirectoryNotFoundException)
            {
                exists = false;
                text = string.Empty;
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                exists = false;
                text = string.Empty;
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
