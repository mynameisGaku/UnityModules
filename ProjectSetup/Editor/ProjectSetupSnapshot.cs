// SPDX-License-Identifier: MIT

using System;
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
            string bundleVersion)
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

        public bool Equals(ProjectSetupSnapshot other)
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
                return hash;
            }
        }
    }
}
