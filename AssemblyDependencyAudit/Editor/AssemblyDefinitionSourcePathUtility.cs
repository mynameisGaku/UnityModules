using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyDependencyAudit.Editor
{
    /// <summary>
    /// asmdef の論理 path、物理 path、meta GUID を Unity API なしで正規化します。
    /// </summary>
    internal static class AssemblyDefinitionSourcePathUtility
    {
        /// <summary>Unity が assembly definition として扱う拡張子です。</summary>
        private const string AssemblyDefinitionExtension = ".asmdef";

        /// <summary>Unity が assembly definition reference として扱う拡張子です。</summary>
        private const string AssemblyReferenceExtension = ".asmref";

        /// <summary>
        /// AssetDatabase 結果と物理列挙結果を Ordinal 順の重複しない asset path へまとめます。
        /// </summary>
        internal static IReadOnlyList<string> MergeAssetPaths(
            IReadOnlyList<string> typedAssetPaths,
            IReadOnlyList<string> physicalAssetPaths)
        {
            return MergeAssetPaths(typedAssetPaths, physicalAssetPaths, AssemblyDefinitionExtension);
        }

        /// <summary>
        /// asmref の typed path と物理列挙結果を Ordinal 順の重複しない一覧へまとめます。
        /// </summary>
        internal static IReadOnlyList<string> MergeAssemblyReferenceAssetPaths(
            IReadOnlyList<string> typedAssetPaths,
            IReadOnlyList<string> physicalAssetPaths)
        {
            return MergeAssetPaths(typedAssetPaths, physicalAssetPaths, AssemblyReferenceExtension);
        }

        /// <summary>指定拡張子の対象pathだけを重複なしでまとめます。</summary>
        private static IReadOnlyList<string> MergeAssetPaths(
            IReadOnlyList<string> typedAssetPaths,
            IReadOnlyList<string> physicalAssetPaths,
            string extension)
        {
            var merged = new SortedSet<string>(StringComparer.Ordinal);
            AddIncludedPaths(merged, typedAssetPaths, extension);
            AddIncludedPaths(merged, physicalAssetPaths, extension);
            return new List<string>(merged).AsReadOnly();
        }

        /// <summary>
        /// Assets または Packages 配下で、Unity が無視する directory を通らない asmdef かを返します。
        /// </summary>
        internal static bool IsIncludedAssetPath(string assetPath)
        {
            return IsIncludedAssetPath(assetPath, AssemblyDefinitionExtension);
        }

        /// <summary>
        /// Assets または Packages 配下で、Unity が無視する directory を通らない asmref かを返します。
        /// </summary>
        internal static bool IsIncludedAssemblyReferenceAssetPath(string assetPath)
        {
            return IsIncludedAssetPath(assetPath, AssemblyReferenceExtension);
        }

        /// <summary>指定拡張子で監査対象に含まれる asset path かを返します。</summary>
        private static bool IsIncludedAssetPath(string assetPath, string extension)
        {
            var normalized = NormalizeAssetPath(assetPath);
            if (!normalized.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
                (!normalized.StartsWith("Assets/", StringComparison.Ordinal) &&
                    !normalized.StartsWith("Packages/", StringComparison.Ordinal)))
            {
                return false;
            }

            var segments = normalized.Split('/');
            if (segments.Length < 2)
            {
                return false;
            }

            for (var index = 0; index < segments.Length; index++)
            {
                if (string.IsNullOrEmpty(segments[index]))
                {
                    return false;
                }

                if (index > 0 &&
                    (index < segments.Length - 1
                        ? IsIgnoredDirectoryName(segments[index])
                        : IsIgnoredFileName(segments[index])))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 物理 root 配下の asmdef file を対応する Unity asset path へ変換します。
        /// root 外、無視対象 directory、asmdef 以外は false を返します。
        /// </summary>
        internal static bool TryMapPhysicalFileToAssetPath(
            string rootAssetPath,
            string rootPhysicalPath,
            string filePhysicalPath,
            out string assetPath)
        {
            return TryMapPhysicalFileToAssetPath(
                rootAssetPath,
                rootPhysicalPath,
                filePhysicalPath,
                AssemblyDefinitionExtension,
                out assetPath);
        }

        /// <summary>
        /// 物理 root 配下の asmref file を対応する Unity asset path へ変換します。
        /// </summary>
        internal static bool TryMapPhysicalAssemblyReferenceFileToAssetPath(
            string rootAssetPath,
            string rootPhysicalPath,
            string filePhysicalPath,
            out string assetPath)
        {
            return TryMapPhysicalFileToAssetPath(
                rootAssetPath,
                rootPhysicalPath,
                filePhysicalPath,
                AssemblyReferenceExtension,
                out assetPath);
        }

        /// <summary>指定拡張子の物理fileを安全なasset pathへ変換します。</summary>
        private static bool TryMapPhysicalFileToAssetPath(
            string rootAssetPath,
            string rootPhysicalPath,
            string filePhysicalPath,
            string extension,
            out string assetPath)
        {
            assetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(rootAssetPath) ||
                string.IsNullOrWhiteSpace(rootPhysicalPath) ||
                string.IsNullOrWhiteSpace(filePhysicalPath))
            {
                return false;
            }

            try
            {
                var fullRoot = Path.GetFullPath(rootPhysicalPath);
                var fullFile = Path.GetFullPath(filePhysicalPath);
                var relative = Path.GetRelativePath(fullRoot, fullFile);
                if (string.IsNullOrEmpty(relative) ||
                    string.Equals(relative, ".", StringComparison.Ordinal) ||
                    Path.IsPathRooted(relative) ||
                    string.Equals(relative, "..", StringComparison.Ordinal) ||
                    relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                    relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
                {
                    return false;
                }

                var normalizedRoot = NormalizeAssetPath(rootAssetPath).TrimEnd('/');
                var normalizedRelative = NormalizeAssetPath(relative).TrimStart('/');
                var candidate = normalizedRoot + "/" + normalizedRelative;
                if (!IsIncludedAssetPath(candidate, extension))
                {
                    return false;
                }

                assetPath = candidate;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// root asset path 配下の論理 pathを、対応する物理 root 配下へ安全に変換します。
        /// </summary>
        internal static bool TryMapAssetPathToPhysicalFile(
            string rootAssetPath,
            string rootPhysicalPath,
            string assetPath,
            out string physicalPath)
        {
            return TryMapAssetPathToPhysicalFile(
                rootAssetPath,
                rootPhysicalPath,
                assetPath,
                AssemblyDefinitionExtension,
                out physicalPath);
        }

        /// <summary>asmref asset pathを対応する物理root配下へ安全に変換します。</summary>
        internal static bool TryMapAssemblyReferenceAssetPathToPhysicalFile(
            string rootAssetPath,
            string rootPhysicalPath,
            string assetPath,
            out string physicalPath)
        {
            return TryMapAssetPathToPhysicalFile(
                rootAssetPath,
                rootPhysicalPath,
                assetPath,
                AssemblyReferenceExtension,
                out physicalPath);
        }

        /// <summary>指定拡張子のasset pathを対応する物理fileへ安全に変換します。</summary>
        private static bool TryMapAssetPathToPhysicalFile(
            string rootAssetPath,
            string rootPhysicalPath,
            string assetPath,
            string extension,
            out string physicalPath)
        {
            physicalPath = string.Empty;
            var normalizedRoot = NormalizeAssetPath(rootAssetPath).TrimEnd('/');
            var normalizedAssetPath = NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(normalizedRoot) ||
                string.IsNullOrWhiteSpace(rootPhysicalPath) ||
                !IsIncludedAssetPath(normalizedAssetPath, extension) ||
                !normalizedAssetPath.StartsWith(normalizedRoot + "/", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                var relative = normalizedAssetPath.Substring(normalizedRoot.Length + 1);
                var combined = Path.Combine(
                    Path.GetFullPath(rootPhysicalPath),
                    relative.Replace('/', Path.DirectorySeparatorChar));
                var fullPath = Path.GetFullPath(combined);
                if (!TryMapPhysicalFileToAssetPath(normalizedRoot, rootPhysicalPath, fullPath, extension, out var roundTrip) ||
                    !string.Equals(roundTrip, normalizedAssetPath, StringComparison.Ordinal))
                {
                    return false;
                }

                physicalPath = fullPath;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Unity meta YAML から32桁16進数の GUID を取り出します。
        /// GUID が無い、または形式が不正な場合は false を返します。
        /// </summary>
        internal static bool TryExtractGuidFromMeta(string metaText, out string guid)
        {
            guid = string.Empty;
            if (string.IsNullOrEmpty(metaText))
            {
                return false;
            }

            using (var reader = new StringReader(metaText))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.StartsWith("guid:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var candidate = line.Substring(5).Trim();
                    if (IsHexGuid(candidate))
                    {
                        guid = candidate;
                        return true;
                    }

                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Unity meta YAML に top-level guid field が exactly one あり、32桁16進数なら取得します。
        /// </summary>
        internal static bool TryExtractExactlyOneGuidFromMeta(string metaText, out string guid)
        {
            guid = string.Empty;
            if (string.IsNullOrEmpty(metaText))
            {
                return false;
            }

            var count = 0;
            using (var reader = new StringReader(metaText))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.StartsWith("guid:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    count++;
                    var candidate = line.Substring(5).Trim();
                    if (count != 1 || !IsHexGuid(candidate))
                    {
                        guid = string.Empty;
                        return false;
                    }

                    guid = candidate;
                }
            }

            if (count == 1)
            {
                return true;
            }

            guid = string.Empty;
            return false;
        }

        /// <summary>
        /// directory 名がUnityのimport対象外かを返します。
        /// </summary>
        internal static bool IsIgnoredDirectoryName(string directoryName)
        {
            return IsIgnoredAssetName(directoryName);
        }

        /// <summary>file 名がUnityのimport対象外かを返します。</summary>
        internal static bool IsIgnoredFileName(string fileName)
        {
            return IsIgnoredAssetName(fileName) ||
                string.Equals(Path.GetExtension(fileName), ".tmp", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>fileまたはdirectoryの予約名をUnityのimport規則に合わせて判定します。</summary>
        private static bool IsIgnoredAssetName(string name)
        {
            return !string.IsNullOrEmpty(name) &&
                (name.StartsWith(".", StringComparison.Ordinal) ||
                    name.EndsWith("~", StringComparison.Ordinal) ||
                    string.Equals(name, "cvs", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>path separator を Unity asset path 形式へそろえます。</summary>
        internal static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        /// <summary>候補一覧から監査対象だけを重複なしで追加します。</summary>
        private static void AddIncludedPaths(
            SortedSet<string> destination,
            IReadOnlyList<string> paths,
            string extension)
        {
            if (paths == null)
            {
                return;
            }

            for (var index = 0; index < paths.Count; index++)
            {
                var normalized = NormalizeAssetPath(paths[index]);
                if (IsIncludedAssetPath(normalized, extension))
                {
                    destination.Add(normalized);
                }
            }
        }

        /// <summary>32文字すべてが16進数かを返します。</summary>
        private static bool IsHexGuid(string value)
        {
            if (value == null || value.Length != 32)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if ((character < '0' || character > '9') &&
                    (character < 'a' || character > 'f') &&
                    (character < 'A' || character > 'F'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
