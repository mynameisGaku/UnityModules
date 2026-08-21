using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ReferenceFinder
{
    /// <summary>
    /// Finds project assets that directly depend on a selected asset.
    /// </summary>
    public static class AssetReferenceFinder
    {
        private static readonly StringComparer PathComparer = StringComparer.Ordinal;

        /// <summary>
        /// Finds direct references to an asset below the Assets folder.
        /// </summary>
        /// <param name="target">A persistent project or package asset.</param>
        /// <returns>A complete, ordinally sorted search result.</returns>
        /// <exception cref="ArgumentException">Thrown when the target is not a persistent non-folder asset.</exception>
        public static AssetReferenceSearchResult FindDirectReferences(UnityEngine.Object target)
        {
            if (target == null)
            {
                throw new ArgumentException("A persistent asset is required.", nameof(target));
            }

            return FindReferences(
                AssetDatabase.GetAssetPath(target),
                AssetReferenceSearchMode.Direct,
                null);
        }

        /// <summary>
        /// Finds direct references to an asset inside specified Assets folders.
        /// </summary>
        /// <param name="targetAssetPath">A canonical AssetDatabase path for a non-folder asset.</param>
        /// <param name="searchFolders">Assets folders to scan. Null or empty scans the complete Assets folder.</param>
        /// <returns>A complete, ordinally sorted search result.</returns>
        /// <exception cref="ArgumentException">Thrown when the target or a search folder is invalid.</exception>
        public static AssetReferenceSearchResult FindDirectReferences(
            string targetAssetPath,
            IReadOnlyList<string> searchFolders = null)
        {
            return FindReferences(targetAssetPath, AssetReferenceSearchMode.Direct, searchFolders);
        }

        /// <summary>
        /// Finds direct or transitive references to an asset below the Assets folder.
        /// </summary>
        /// <param name="target">A persistent project or package asset.</param>
        /// <param name="searchMode">The dependency depth to match.</param>
        /// <param name="searchFolders">Assets folders to scan. Null or empty scans the complete Assets folder.</param>
        /// <returns>A complete, ordinally sorted search result.</returns>
        /// <exception cref="ArgumentException">Thrown when the target, mode, or a search folder is invalid.</exception>
        public static AssetReferenceSearchResult FindReferences(
            UnityEngine.Object target,
            AssetReferenceSearchMode searchMode,
            IReadOnlyList<string> searchFolders = null)
        {
            if (target == null)
            {
                throw new ArgumentException("A persistent asset is required.", nameof(target));
            }

            return FindReferences(AssetDatabase.GetAssetPath(target), searchMode, searchFolders);
        }

        /// <summary>
        /// Finds direct or transitive references to an asset inside specified Assets folders.
        /// </summary>
        /// <param name="targetAssetPath">A canonical AssetDatabase path for a non-folder asset.</param>
        /// <param name="searchMode">The dependency depth to match.</param>
        /// <param name="searchFolders">Assets folders to scan. Null or empty scans the complete Assets folder.</param>
        /// <returns>A complete, ordinally sorted search result.</returns>
        /// <exception cref="ArgumentException">Thrown when the target, mode, or a search folder is invalid.</exception>
        public static AssetReferenceSearchResult FindReferences(
            string targetAssetPath,
            AssetReferenceSearchMode searchMode,
            IReadOnlyList<string> searchFolders = null)
        {
            return FindReferencesInternal(targetAssetPath, searchFolders, searchMode, null);
        }

        internal static AssetReferenceSearchResult FindDirectReferencesInternal(
            string targetAssetPath,
            IReadOnlyList<string> searchFolders,
            Func<int, int, string, bool> continueSearch)
        {
            return FindReferencesInternal(
                targetAssetPath,
                searchFolders,
                AssetReferenceSearchMode.Direct,
                continueSearch);
        }

        internal static AssetReferenceSearchResult FindReferencesInternal(
            string targetAssetPath,
            IReadOnlyList<string> searchFolders,
            AssetReferenceSearchMode searchMode,
            Func<int, int, string, bool> continueSearch)
        {
            var canonicalTarget = ValidateTarget(targetAssetPath);
            ValidateSearchMode(searchMode);
            var folders = NormalizeSearchFolders(searchFolders);
            var candidates = FindCandidatePaths(folders, canonicalTarget);
            var references = new List<string>();
            var failures = new List<string>();
            var scanned = 0;
            var canceled = false;

            for (var index = 0; index < candidates.Length; index++)
            {
                var candidate = candidates[index];
                if (continueSearch != null && !continueSearch(index, candidates.Length, candidate))
                {
                    canceled = true;
                    break;
                }

                try
                {
                    var dependencies = AssetDatabase.GetDependencies(
                        candidate,
                        searchMode == AssetReferenceSearchMode.Recursive);
                    if (dependencies.Any(path => string.Equals(path, canonicalTarget, StringComparison.Ordinal)))
                    {
                        references.Add(candidate);
                    }
                }
                catch (Exception)
                {
                    failures.Add(candidate);
                }

                scanned++;
            }

            references.Sort(PathComparer);
            failures.Sort(PathComparer);
            return new AssetReferenceSearchResult(
                canonicalTarget,
                references.ToArray(),
                failures.ToArray(),
                scanned,
                candidates.Length,
                canceled,
                searchMode);
        }

        private static void ValidateSearchMode(AssetReferenceSearchMode searchMode)
        {
            if (searchMode != AssetReferenceSearchMode.Direct
                && searchMode != AssetReferenceSearchMode.Recursive)
            {
                throw new ArgumentException($"Unsupported search mode: {searchMode}", nameof(searchMode));
            }
        }

        internal static string[] NormalizeSearchFolders(IReadOnlyList<string> searchFolders)
        {
            if (searchFolders == null || searchFolders.Count == 0)
            {
                return new[] { "Assets" };
            }

            var normalized = new List<string>(searchFolders.Count);
            for (var index = 0; index < searchFolders.Count; index++)
            {
                var raw = searchFolders[index];
                if (string.IsNullOrWhiteSpace(raw))
                {
                    throw new ArgumentException("Search folders cannot contain empty paths.", nameof(searchFolders));
                }

                var path = raw.Replace('\\', '/').TrimEnd('/');
                if (!AssetDatabase.IsValidFolder(path)
                    || !(string.Equals(path, "Assets", StringComparison.Ordinal)
                        || path.StartsWith("Assets/", StringComparison.Ordinal)))
                {
                    throw new ArgumentException($"Search folder must be inside Assets: {raw}", nameof(searchFolders));
                }

                if (!normalized.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    normalized.Add(path);
                }
            }

            normalized.Sort(PathComparer);
            if (normalized.Any(path => string.Equals(path, "Assets", StringComparison.Ordinal)))
            {
                return new[] { "Assets" };
            }

            var compact = new List<string>(normalized.Count);
            foreach (var path in normalized)
            {
                if (!compact.Any(parent => path.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase)))
                {
                    compact.Add(path);
                }
            }

            return compact.ToArray();
        }

        private static string ValidateTarget(string targetAssetPath)
        {
            if (string.IsNullOrWhiteSpace(targetAssetPath))
            {
                throw new ArgumentException("A target asset path is required.", nameof(targetAssetPath));
            }

            var normalized = targetAssetPath.Replace('\\', '/');
            var guid = AssetDatabase.AssetPathToGUID(normalized);
            if (string.IsNullOrEmpty(guid) || AssetDatabase.IsValidFolder(normalized))
            {
                throw new ArgumentException($"Target must be a persistent non-folder asset: {targetAssetPath}", nameof(targetAssetPath));
            }

            var canonical = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(canonical))
            {
                throw new ArgumentException($"Target asset could not be resolved: {targetAssetPath}", nameof(targetAssetPath));
            }

            return canonical;
        }

        private static string[] FindCandidatePaths(string[] folders, string targetAssetPath)
        {
            return AssetDatabase.FindAssets(string.Empty, folders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path)
                    && !AssetDatabase.IsValidFolder(path)
                    && !string.Equals(path, targetAssetPath, StringComparison.Ordinal))
                .Distinct(PathComparer)
                .OrderBy(path => path, PathComparer)
                .ToArray();
        }
    }
}
