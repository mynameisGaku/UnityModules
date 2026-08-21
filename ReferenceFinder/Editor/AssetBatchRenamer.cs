using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ReferenceFinder
{
    /// <summary>
    /// Previews and applies GUID-preserving renames for selected main assets below Assets.
    /// </summary>
    public static class AssetBatchRenamer
    {
        private static readonly StringComparer PathComparer = StringComparer.Ordinal;

        /// <summary>
        /// Creates a non-mutating rename preview from selected main assets.
        /// </summary>
        /// <param name="assets">Persistent main assets below the Assets folder.</param>
        /// <param name="findText">Optional ordinal text to replace in each file name.</param>
        /// <param name="replacementText">Text used for every find-text match.</param>
        /// <param name="prefix">Text inserted before each transformed file name.</param>
        /// <param name="suffix">Text inserted after each transformed file name.</param>
        /// <returns>A validated immutable plan that excludes unchanged names.</returns>
        /// <exception cref="ArgumentException">Thrown when selection, naming input, or destinations are invalid.</exception>
        public static AssetRenamePlan Preview(
            IReadOnlyList<UnityEngine.Object> assets,
            string findText,
            string replacementText,
            string prefix,
            string suffix)
        {
            if (assets == null || assets.Count == 0)
            {
                throw new ArgumentException("At least one main asset must be selected.", nameof(assets));
            }

            findText ??= string.Empty;
            replacementText ??= string.Empty;
            prefix ??= string.Empty;
            suffix ??= string.Empty;
            var sourcePaths = new SortedSet<string>(PathComparer);
            for (var index = 0; index < assets.Count; index++)
            {
                var asset = assets[index];
                var path = AssetDatabase.GetAssetPath(asset).Replace('\\', '/');
                if (asset == null
                    || string.IsNullOrEmpty(path)
                    || !path.StartsWith("Assets/", StringComparison.Ordinal)
                    || AssetDatabase.IsValidFolder(path)
                    || asset is MonoScript
                    || AssetDatabase.LoadMainAssetAtPath(path) != asset)
                {
                    throw new ArgumentException("Selection must contain only non-script persistent main assets below Assets.", nameof(assets));
                }

                sourcePaths.Add(path);
            }

            var entries = new List<AssetRenameEntry>(sourcePaths.Count);
            var destinationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sourcePath in sourcePaths)
            {
                var originalName = Path.GetFileNameWithoutExtension(sourcePath);
                var transformedName = string.IsNullOrEmpty(findText)
                    ? originalName
                    : originalName.Replace(findText, replacementText);
                var newName = prefix + transformedName + suffix;
                ValidateFileName(newName);
                var directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
                var extension = Path.GetExtension(sourcePath);
                var destinationPath = $"{directory}/{newName}{extension}";
                if (string.Equals(sourcePath, destinationPath, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("Case-only renames are not supported.", nameof(assets));
                }

                if (!destinationPaths.Add(destinationPath))
                {
                    throw new ArgumentException($"Multiple assets would use the same destination: {destinationPath}", nameof(assets));
                }

                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(destinationPath)))
                {
                    throw new ArgumentException($"A destination asset already exists: {destinationPath}", nameof(assets));
                }

                var guid = AssetDatabase.AssetPathToGUID(sourcePath);
                if (string.IsNullOrEmpty(guid))
                {
                    throw new ArgumentException($"The selected asset GUID could not be resolved: {sourcePath}", nameof(assets));
                }

                entries.Add(new AssetRenameEntry(guid, sourcePath, destinationPath));
            }

            return new AssetRenamePlan(entries.ToArray());
        }

        /// <summary>
        /// Applies a preview after verifying every GUID, source path, and destination again.
        /// </summary>
        /// <param name="plan">A plan returned by <see cref="Preview"/>.</param>
        /// <returns>The final renamed paths.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the plan is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the plan is stale or a rename fails.</exception>
        public static AssetRenameResult Apply(AssetRenamePlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            ValidatePlanState(plan);
            var applied = new List<AssetRenameEntry>(plan.Entries.Count);
            foreach (var entry in plan.Entries)
            {
                var newName = Path.GetFileNameWithoutExtension(entry.NewPath);
                var error = AssetDatabase.RenameAsset(entry.OriginalPath, newName);
                if (!string.IsNullOrEmpty(error))
                {
                    var rollbackErrors = Rollback(applied);
                    var suffix = rollbackErrors.Count == 0
                        ? string.Empty
                        : $" Rollback failures: {string.Join(" | ", rollbackErrors)}";
                    throw new InvalidOperationException($"Asset rename failed: {entry.OriginalPath}. {error}{suffix}");
                }

                applied.Add(entry);
            }

            AssetDatabase.SaveAssets();
            return new AssetRenameResult(
                applied.Select(entry => entry.NewPath).OrderBy(path => path, PathComparer).ToArray());
        }

        private static void ValidatePlanState(AssetRenamePlan plan)
        {
            var destinationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in plan.Entries)
            {
                if (!string.Equals(AssetDatabase.GUIDToAssetPath(entry.Guid), entry.OriginalPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"A previewed asset moved or was renamed: {entry.OriginalPath}");
                }

                if (!destinationPaths.Add(entry.NewPath)
                    || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(entry.NewPath)))
                {
                    throw new InvalidOperationException($"A previewed destination is no longer available: {entry.NewPath}");
                }
            }
        }

        private static List<string> Rollback(IReadOnlyList<AssetRenameEntry> applied)
        {
            var errors = new List<string>();
            for (var index = applied.Count - 1; index >= 0; index--)
            {
                var entry = applied[index];
                var currentPath = AssetDatabase.GUIDToAssetPath(entry.Guid);
                var originalName = Path.GetFileNameWithoutExtension(entry.OriginalPath);
                var error = string.IsNullOrEmpty(currentPath)
                    ? "The renamed asset could not be resolved by GUID."
                    : AssetDatabase.RenameAsset(currentPath, originalName);
                if (!string.IsNullOrEmpty(error))
                {
                    errors.Add($"{entry.NewPath}: {error}");
                }
            }

            AssetDatabase.SaveAssets();
            return errors;
        }

        private static void ValidateFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)
                || !string.Equals(fileName, fileName.Trim(), StringComparison.Ordinal)
                || string.Equals(fileName, ".", StringComparison.Ordinal)
                || string.Equals(fileName, "..", StringComparison.Ordinal)
                || fileName.EndsWith(".", StringComparison.Ordinal)
                || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || fileName.Contains('/')
                || fileName.Contains('\\'))
            {
                throw new ArgumentException($"The generated asset name is invalid: {fileName}", nameof(fileName));
            }
        }
    }
}
