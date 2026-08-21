using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ReferenceFinder
{
    /// <summary>
    /// Contains an immutable preview of serialized references that can be replaced.
    /// </summary>
    public sealed class AssetReferenceReplacementPlan
    {
        private readonly ReadOnlyCollection<AssetReferenceOccurrence> _occurrences;
        private readonly ReadOnlyCollection<string> _unsupportedAssetPaths;
        private readonly ReadOnlyCollection<string> _failedAssetPaths;

        internal AssetReferenceReplacementPlan(
            string targetAssetPath,
            string replacementAssetPath,
            AssetReferenceIdentity targetIdentity,
            AssetReferenceIdentity replacementIdentity,
            Type targetType,
            AssetReferenceOccurrence[] occurrences,
            string[] unsupportedAssetPaths,
            string[] failedAssetPaths)
        {
            TargetAssetPath = targetAssetPath;
            ReplacementAssetPath = replacementAssetPath;
            TargetIdentity = targetIdentity;
            ReplacementIdentity = replacementIdentity;
            TargetType = targetType;
            _occurrences = Array.AsReadOnly(occurrences);
            _unsupportedAssetPaths = Array.AsReadOnly(unsupportedAssetPaths);
            _failedAssetPaths = Array.AsReadOnly(failedAssetPaths);
        }

        /// <summary>Gets the canonical path of the asset being replaced.</summary>
        public string TargetAssetPath { get; }

        /// <summary>Gets the canonical path of the replacement asset.</summary>
        public string ReplacementAssetPath { get; }

        /// <summary>Gets exact serialized references that passed the preview checks.</summary>
        public IReadOnlyList<AssetReferenceOccurrence> Occurrences => _occurrences;

        /// <summary>Gets direct-reference assets whose exact property could not be edited safely.</summary>
        public IReadOnlyList<string> UnsupportedAssetPaths => _unsupportedAssetPaths;

        /// <summary>Gets asset paths that could not be inspected.</summary>
        public IReadOnlyList<string> FailedAssetPaths => _failedAssetPaths;

        internal AssetReferenceIdentity TargetIdentity { get; }

        internal AssetReferenceIdentity ReplacementIdentity { get; }

        internal Type TargetType { get; }
    }
}
