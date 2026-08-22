// SPDX-License-Identifier: MIT

using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Editor
{
    internal sealed class ProjectSetupProfile : ScriptableObject
    {
        [SerializeField] private bool configureAssetSerialization = true;
        [SerializeField] private SerializationMode assetSerialization = SerializationMode.ForceText;
        [SerializeField] private bool configureVersionControl = true;
        [SerializeField] private string versionControlMode = "Visible Meta Files";
        [SerializeField] private bool configureEnterPlayMode;
        [SerializeField] private bool enterPlayModeOptionsEnabled;
        [SerializeField] private EnterPlayModeOptions enterPlayModeOptions = EnterPlayModeOptions.None;
        [SerializeField] private bool configureColorSpace;
        [SerializeField] private ColorSpace colorSpace = ColorSpace.Linear;
        [SerializeField] private bool configureRunInBackground;
        [SerializeField] private bool runInBackground;
        [SerializeField] private bool configureCompanyName;
        [SerializeField] private string companyName = "DefaultCompany";
        [SerializeField] private bool configureProductName;
        [SerializeField] private string productName = "New Unity Project";
        [SerializeField] private bool configureBundleVersion;
        [SerializeField] private string bundleVersion = "1.0.0";

        internal bool ConfigureAssetSerialization { get => configureAssetSerialization; set => configureAssetSerialization = value; }
        internal SerializationMode AssetSerialization { get => assetSerialization; set => assetSerialization = value; }
        internal bool ConfigureVersionControl { get => configureVersionControl; set => configureVersionControl = value; }
        internal string VersionControlMode { get => versionControlMode; set => versionControlMode = value; }
        internal bool ConfigureEnterPlayMode { get => configureEnterPlayMode; set => configureEnterPlayMode = value; }
        internal bool EnterPlayModeOptionsEnabled { get => enterPlayModeOptionsEnabled; set => enterPlayModeOptionsEnabled = value; }
        internal EnterPlayModeOptions EnterPlayModeOptions { get => enterPlayModeOptions; set => enterPlayModeOptions = value; }
        internal bool ConfigureColorSpace { get => configureColorSpace; set => configureColorSpace = value; }
        internal ColorSpace ColorSpace { get => colorSpace; set => colorSpace = value; }
        internal bool ConfigureRunInBackground { get => configureRunInBackground; set => configureRunInBackground = value; }
        internal bool RunInBackground { get => runInBackground; set => runInBackground = value; }
        internal bool ConfigureCompanyName { get => configureCompanyName; set => configureCompanyName = value; }
        internal string CompanyName { get => companyName; set => companyName = value; }
        internal bool ConfigureProductName { get => configureProductName; set => configureProductName = value; }
        internal string ProductName { get => productName; set => productName = value; }
        internal bool ConfigureBundleVersion { get => configureBundleVersion; set => configureBundleVersion = value; }
        internal string BundleVersion { get => bundleVersion; set => bundleVersion = value; }

        internal void SetRecommendedDefaults()
        {
            configureAssetSerialization = true;
            assetSerialization = SerializationMode.ForceText;
            configureVersionControl = true;
            versionControlMode = "Visible Meta Files";
            configureEnterPlayMode = false;
            enterPlayModeOptionsEnabled = false;
            enterPlayModeOptions = EnterPlayModeOptions.None;
            configureColorSpace = false;
            colorSpace = ColorSpace.Linear;
            configureRunInBackground = false;
            runInBackground = false;
            configureCompanyName = false;
            companyName = "DefaultCompany";
            configureProductName = false;
            productName = "New Unity Project";
            configureBundleVersion = false;
            bundleVersion = "1.0.0";
        }

        internal void Capture(ProjectSetupSnapshot snapshot)
        {
            configureAssetSerialization = true;
            assetSerialization = snapshot.AssetSerialization;
            configureVersionControl = true;
            versionControlMode = snapshot.VersionControlMode;
            configureEnterPlayMode = true;
            enterPlayModeOptionsEnabled = snapshot.EnterPlayModeOptionsEnabled;
            enterPlayModeOptions = snapshot.EnterPlayModeOptions;
            configureColorSpace = true;
            colorSpace = snapshot.ColorSpace;
            configureRunInBackground = true;
            runInBackground = snapshot.RunInBackground;
            configureCompanyName = true;
            companyName = snapshot.CompanyName;
            configureProductName = true;
            productName = snapshot.ProductName;
            configureBundleVersion = true;
            bundleVersion = snapshot.BundleVersion;
        }
    }
}
