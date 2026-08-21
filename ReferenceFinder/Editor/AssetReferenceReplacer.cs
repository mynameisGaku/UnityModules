using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ReferenceFinder
{
    /// <summary>
    /// Previews and replaces exact serialized asset references with Undo support.
    /// </summary>
    public static class AssetReferenceReplacer
    {
        private static readonly StringComparer PathComparer = StringComparer.Ordinal;

        /// <summary>
        /// Creates a non-mutating replacement preview for direct serialized references below Assets.
        /// </summary>
        /// <param name="target">The persistent asset currently referenced.</param>
        /// <param name="replacement">A different persistent asset with the same concrete type.</param>
        /// <param name="searchFolders">Assets folders to inspect. Null or empty scans Assets.</param>
        /// <returns>An immutable plan containing supported, unsupported, and failed paths.</returns>
        /// <exception cref="ArgumentException">Thrown when either asset or a search folder is invalid.</exception>
        public static AssetReferenceReplacementPlan Preview(
            UnityEngine.Object target,
            UnityEngine.Object replacement,
            IReadOnlyList<string> searchFolders = null)
        {
            var targetInfo = ValidateAsset(target, nameof(target));
            var replacementInfo = ValidateAsset(replacement, nameof(replacement));
            if (targetInfo.Identity.Equals(replacementInfo.Identity))
            {
                throw new ArgumentException("Target and replacement assets must be different.", nameof(replacement));
            }

            if (targetInfo.Asset.GetType() != replacementInfo.Asset.GetType())
            {
                throw new ArgumentException("Target and replacement assets must have the same concrete type.", nameof(replacement));
            }

            if (targetInfo.Asset is MonoScript)
            {
                throw new ArgumentException("Script references cannot be replaced by this tool.", nameof(target));
            }

            var search = AssetReferenceFinder.FindDirectReferences(targetInfo.Path, searchFolders);
            var occurrences = new List<AssetReferenceOccurrence>();
            var unsupported = new List<string>();
            var failed = new List<string>(search.FailedAssetPaths);

            foreach (var assetPath in search.ReferenceAssetPaths)
            {
                if (string.Equals(Path.GetExtension(assetPath), ".unity", StringComparison.OrdinalIgnoreCase))
                {
                    unsupported.Add(assetPath);
                    continue;
                }

                try
                {
                    var found = FindOccurrences(assetPath, targetInfo.Identity);
                    if (found.Count == 0)
                    {
                        unsupported.Add(assetPath);
                    }
                    else
                    {
                        occurrences.AddRange(found);
                    }
                }
                catch (Exception)
                {
                    failed.Add(assetPath);
                }
            }

            occurrences.Sort(CompareOccurrences);
            unsupported.Sort(PathComparer);
            failed = failed.Distinct(PathComparer).OrderBy(path => path, PathComparer).ToList();
            return new AssetReferenceReplacementPlan(
                targetInfo.Path,
                replacementInfo.Path,
                targetInfo.Identity,
                replacementInfo.Identity,
                targetInfo.Asset.GetType(),
                occurrences.ToArray(),
                unsupported.ToArray(),
                failed.ToArray());
        }

        /// <summary>
        /// Applies a preview after verifying that every serialized property is unchanged.
        /// </summary>
        /// <param name="plan">A plan returned by <see cref="Preview"/>.</param>
        /// <returns>The number of replacements and changed asset paths.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the plan is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown before mutation when the plan is stale or incomplete.</exception>
        public static AssetReferenceReplacementResult Apply(AssetReferenceReplacementPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (plan.FailedAssetPaths.Count > 0)
            {
                throw new InvalidOperationException("The preview contains assets that could not be inspected.");
            }

            var target = ResolveAsset(plan.TargetIdentity, plan.TargetAssetPath);
            var replacement = ResolveAsset(plan.ReplacementIdentity, plan.ReplacementAssetPath);
            if (target == null || replacement == null || target.GetType() != plan.TargetType || replacement.GetType() != plan.TargetType)
            {
                throw new InvalidOperationException("The target or replacement asset changed after preview.");
            }

            var resolved = ResolveOccurrences(plan, target);
            if (resolved.Count == 0)
            {
                return new AssetReferenceReplacementResult(0, Array.Empty<string>());
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace Asset References");
            try
            {
                var owners = resolved.Select(item => item.Owner).Distinct().ToArray();
                Undo.RecordObjects(owners, "Replace Asset References");
                foreach (var item in resolved)
                {
                    item.Property.objectReferenceValue = replacement;
                }

                foreach (var serializedObject in resolved.Select(item => item.SerializedObject).Distinct())
                {
                    if (!serializedObject.ApplyModifiedProperties())
                    {
                        throw new InvalidOperationException("A serialized reference could not be updated.");
                    }
                }

                foreach (var owner in owners)
                {
                    EditorUtility.SetDirty(owner);
                }

                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                AssetDatabase.SaveAssets();
                throw;
            }

            var changedPaths = resolved
                .Select(item => item.Occurrence.AssetPath)
                .Distinct(PathComparer)
                .OrderBy(path => path, PathComparer)
                .ToArray();
            return new AssetReferenceReplacementResult(resolved.Count, changedPaths);
        }

        private static List<AssetReferenceOccurrence> FindOccurrences(
            string assetPath,
            AssetReferenceIdentity targetIdentity)
        {
            var occurrences = new List<AssetReferenceOccurrence>();
            foreach (var owner in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (owner == null
                    || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(owner, out _, out long ownerLocalFileId))
                {
                    continue;
                }

                using var serializedObject = new SerializedObject(owner);
                var property = serializedObject.GetIterator();
                var enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyType != SerializedPropertyType.ObjectReference
                        || !AssetReferenceIdentity.TryCreate(property.objectReferenceValue, out var identity)
                        || !identity.Equals(targetIdentity))
                    {
                        continue;
                    }

                    occurrences.Add(new AssetReferenceOccurrence(
                        assetPath,
                        ownerLocalFileId,
                        owner.name,
                        owner.GetType().FullName ?? owner.GetType().Name,
                        property.propertyPath));
                }
            }

            return occurrences;
        }

        private static List<ResolvedOccurrence> ResolveOccurrences(
            AssetReferenceReplacementPlan plan,
            UnityEngine.Object target)
        {
            var resolved = new List<ResolvedOccurrence>(plan.Occurrences.Count);
            var serializedObjects = new Dictionary<string, SerializedObject>(StringComparer.Ordinal);
            foreach (var occurrence in plan.Occurrences)
            {
                var key = occurrence.AssetPath + "\u001f" + occurrence.OwnerLocalFileId;
                if (!serializedObjects.TryGetValue(key, out var serializedObject))
                {
                    var owner = ResolveAsset(
                        new AssetReferenceIdentity(
                            AssetDatabase.AssetPathToGUID(occurrence.AssetPath),
                            occurrence.OwnerLocalFileId),
                        occurrence.AssetPath);
                    if (owner == null)
                    {
                        throw new InvalidOperationException($"A previewed owner no longer exists: {occurrence.AssetPath}");
                    }

                    serializedObject = new SerializedObject(owner);
                    serializedObjects.Add(key, serializedObject);
                }

                var property = serializedObject.FindProperty(occurrence.PropertyPath);
                if (property == null
                    || property.propertyType != SerializedPropertyType.ObjectReference
                    || property.objectReferenceValue != target)
                {
                    throw new InvalidOperationException(
                        $"A previewed reference changed: {occurrence.AssetPath} ({occurrence.PropertyPath})");
                }

                resolved.Add(new ResolvedOccurrence(
                    occurrence,
                    serializedObject.targetObject,
                    serializedObject,
                    property));
            }

            return resolved;
        }

        private static AssetInfo ValidateAsset(UnityEngine.Object asset, string parameterName)
        {
            if (asset == null
                || !EditorUtility.IsPersistent(asset)
                || !AssetReferenceIdentity.TryCreate(asset, out var identity))
            {
                throw new ArgumentException("A persistent non-folder asset is required.", parameterName);
            }

            var path = AssetDatabase.GUIDToAssetPath(identity.Guid);
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            {
                throw new ArgumentException("A persistent non-folder asset is required.", parameterName);
            }

            return new AssetInfo(asset, path, identity);
        }

        private static UnityEngine.Object ResolveAsset(AssetReferenceIdentity identity, string assetPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (AssetReferenceIdentity.TryCreate(asset, out var candidate) && candidate.Equals(identity))
                {
                    return asset;
                }
            }

            return null;
        }

        private static int CompareOccurrences(AssetReferenceOccurrence left, AssetReferenceOccurrence right)
        {
            var pathComparison = PathComparer.Compare(left.AssetPath, right.AssetPath);
            if (pathComparison != 0)
            {
                return pathComparison;
            }

            var ownerComparison = left.OwnerLocalFileId.CompareTo(right.OwnerLocalFileId);
            return ownerComparison != 0
                ? ownerComparison
                : PathComparer.Compare(left.PropertyPath, right.PropertyPath);
        }

        private readonly struct AssetInfo
        {
            internal AssetInfo(UnityEngine.Object asset, string path, AssetReferenceIdentity identity)
            {
                Asset = asset;
                Path = path;
                Identity = identity;
            }

            internal UnityEngine.Object Asset { get; }

            internal string Path { get; }

            internal AssetReferenceIdentity Identity { get; }
        }

        private sealed class ResolvedOccurrence
        {
            internal ResolvedOccurrence(
                AssetReferenceOccurrence occurrence,
                UnityEngine.Object owner,
                SerializedObject serializedObject,
                SerializedProperty property)
            {
                Occurrence = occurrence;
                Owner = owner;
                SerializedObject = serializedObject;
                Property = property;
            }

            internal AssetReferenceOccurrence Occurrence { get; }

            internal UnityEngine.Object Owner { get; }

            internal SerializedObject SerializedObject { get; }

            internal SerializedProperty Property { get; }
        }
    }

    internal readonly struct AssetReferenceIdentity : IEquatable<AssetReferenceIdentity>
    {
        internal AssetReferenceIdentity(string guid, long localFileId)
        {
            Guid = guid;
            LocalFileId = localFileId;
        }

        internal string Guid { get; }

        internal long LocalFileId { get; }

        internal static bool TryCreate(UnityEngine.Object asset, out AssetReferenceIdentity identity)
        {
            identity = default;
            if (asset == null
                || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localFileId)
                || string.IsNullOrEmpty(guid))
            {
                return false;
            }

            identity = new AssetReferenceIdentity(guid, localFileId);
            return true;
        }

        public bool Equals(AssetReferenceIdentity other)
        {
            return string.Equals(Guid, other.Guid, StringComparison.Ordinal)
                && LocalFileId == other.LocalFileId;
        }

        public override bool Equals(object obj)
        {
            return obj is AssetReferenceIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Guid != null ? StringComparer.Ordinal.GetHashCode(Guid) : 0) * 397) ^ LocalFileId.GetHashCode();
            }
        }
    }
}
