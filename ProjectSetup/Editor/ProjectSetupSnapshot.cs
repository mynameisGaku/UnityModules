// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Editor
{
    [Serializable]
    internal readonly struct ProjectSetupSnapshot : IEquatable<ProjectSetupSnapshot>
    {
        internal ProjectSetupSnapshot(
            SerializationMode assetSerialization,
            string versionControlMode,
            bool enterPlayModeOptionsEnabled,
            EnterPlayModeOptions enterPlayModeOptions,
            ColorSpace colorSpace,
            bool runInBackground,
            string companyName,
            string productName,
            string bundleVersion,
            bool hasTagManagerData = false,
            string[] tags = null,
            string[] customTags = null,
            string[] layers = null,
            ProjectSetupSortingLayer[] sortingLayers = null,
            string tagManagerFileText = null)
        {
            AssetSerialization = assetSerialization;
            VersionControlMode = versionControlMode ?? string.Empty;
            EnterPlayModeOptionsEnabled = enterPlayModeOptionsEnabled;
            EnterPlayModeOptions = enterPlayModeOptions;
            ColorSpace = colorSpace;
            RunInBackground = runInBackground;
            CompanyName = companyName ?? string.Empty;
            ProductName = productName ?? string.Empty;
            BundleVersion = bundleVersion ?? string.Empty;
            HasTagManagerData = hasTagManagerData;
            Tags = Clone(tags);
            CustomTags = Clone(customTags);
            Layers = Clone(layers);
            SortingLayers = Clone(sortingLayers);
            TagManagerFileText = tagManagerFileText ?? string.Empty;
        }

        internal SerializationMode AssetSerialization { get; }
        internal string VersionControlMode { get; }
        internal bool EnterPlayModeOptionsEnabled { get; }
        internal EnterPlayModeOptions EnterPlayModeOptions { get; }
        internal ColorSpace ColorSpace { get; }
        internal bool RunInBackground { get; }
        internal string CompanyName { get; }
        internal string ProductName { get; }
        internal string BundleVersion { get; }
        internal bool HasTagManagerData { get; }
        internal string[] Tags { get; }
        internal string[] CustomTags { get; }
        internal string[] Layers { get; }
        internal ProjectSetupSortingLayer[] SortingLayers { get; }
        internal string TagManagerFileText { get; }

        public bool Equals(ProjectSetupSnapshot other)
        {
            return ScalarEquals(other)
                && HasTagManagerData == other.HasTagManagerData
                && SequenceEqual(Tags, other.Tags)
                && SequenceEqual(CustomTags, other.CustomTags)
                && SequenceEqual(Layers, other.Layers)
                && SequenceEqual(SortingLayers, other.SortingLayers)
                && string.Equals(TagManagerFileText, other.TagManagerFileText, StringComparison.Ordinal);
        }

        internal bool Matches(ProjectSetupSnapshot actual)
        {
            return ScalarEquals(actual)
                && (!HasTagManagerData
                    || (actual.HasTagManagerData
                        && SequenceEqual(Tags, actual.Tags)
                        && SequenceEqual(CustomTags, actual.CustomTags)
                        && SequenceEqual(Layers, actual.Layers)
                        && SequenceEqual(SortingLayers, actual.SortingLayers)
                        && string.Equals(TagManagerFileText, actual.TagManagerFileText, StringComparison.Ordinal)));
        }

        private bool ScalarEquals(ProjectSetupSnapshot other)
        {
            return AssetSerialization == other.AssetSerialization
                && string.Equals(VersionControlMode, other.VersionControlMode, StringComparison.Ordinal)
                && EnterPlayModeOptionsEnabled == other.EnterPlayModeOptionsEnabled
                && EnterPlayModeOptions == other.EnterPlayModeOptions
                && ColorSpace == other.ColorSpace
                && RunInBackground == other.RunInBackground
                && string.Equals(CompanyName, other.CompanyName, StringComparison.Ordinal)
                && string.Equals(ProductName, other.ProductName, StringComparison.Ordinal)
                && string.Equals(BundleVersion, other.BundleVersion, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ProjectSetupSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)AssetSerialization;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(VersionControlMode);
                hash = (hash * 397) ^ EnterPlayModeOptionsEnabled.GetHashCode();
                hash = (hash * 397) ^ (int)EnterPlayModeOptions;
                hash = (hash * 397) ^ (int)ColorSpace;
                hash = (hash * 397) ^ RunInBackground.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(CompanyName);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ProductName);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(BundleVersion);
                hash = (hash * 397) ^ HasTagManagerData.GetHashCode();
                hash = AddHash(hash, Tags);
                hash = AddHash(hash, CustomTags);
                hash = AddHash(hash, Layers);
                hash = AddHash(hash, SortingLayers);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(TagManagerFileText ?? string.Empty);
                return hash;
            }
        }

        private static string[] Clone(string[] values)
        {
            return values == null ? Array.Empty<string>() : (string[])values.Clone();
        }

        private static ProjectSetupSortingLayer[] Clone(ProjectSetupSortingLayer[] values)
        {
            return values == null ? Array.Empty<ProjectSetupSortingLayer>() : (ProjectSetupSortingLayer[])values.Clone();
        }

        private static bool SequenceEqual<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            var comparer = EqualityComparer<T>.Default;
            for (var index = 0; index < left.Count; index++)
            {
                if (!comparer.Equals(left[index], right[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static int AddHash<T>(int hash, IReadOnlyList<T> values)
        {
            unchecked
            {
                if (values == null)
                {
                    return hash * 397;
                }

                for (var index = 0; index < values.Count; index++)
                {
                    var value = values[index];
                    hash = (hash * 397) ^ ((object)value == null ? 0 : EqualityComparer<T>.Default.GetHashCode(value));
                }

                return hash;
            }
        }
    }
}
