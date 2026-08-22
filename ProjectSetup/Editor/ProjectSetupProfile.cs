// SPDX-License-Identifier: MIT

using System;
using System.Linq;
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
        [SerializeField] private bool configureBuildScenes;
        [SerializeField] private ProjectSetupBuildScene[] buildScenes = Array.Empty<ProjectSetupBuildScene>();
        [SerializeField] private bool configureTags;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private bool configureLayers;
        [SerializeField] private string[] layers = Array.Empty<string>();
        [SerializeField] private bool configureSortingLayers;
        [SerializeField] private string[] sortingLayers = Array.Empty<string>();

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
        internal bool ConfigureBuildScenes { get => configureBuildScenes; set => configureBuildScenes = value; }
        internal ProjectSetupBuildScene[] BuildScenes
        {
            get => buildScenes ?? Array.Empty<ProjectSetupBuildScene>();
            set => buildScenes = CloneBuildScenes(value);
        }
        internal bool ConfigureTags { get => configureTags; set => configureTags = value; }
        internal string[] Tags { get => tags ?? Array.Empty<string>(); set => tags = value ?? Array.Empty<string>(); }
        internal bool ConfigureLayers { get => configureLayers; set => configureLayers = value; }
        internal string[] Layers { get => layers ?? Array.Empty<string>(); set => layers = value ?? Array.Empty<string>(); }
        internal bool ConfigureSortingLayers { get => configureSortingLayers; set => configureSortingLayers = value; }
        internal string[] SortingLayers { get => sortingLayers ?? Array.Empty<string>(); set => sortingLayers = value ?? Array.Empty<string>(); }

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
            configureBuildScenes = false;
            buildScenes = Array.Empty<ProjectSetupBuildScene>();
            configureTags = false;
            tags = Array.Empty<string>();
            configureLayers = false;
            layers = Array.Empty<string>();
            configureSortingLayers = false;
            sortingLayers = Array.Empty<string>();
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
            configureBuildScenes = snapshot.HasBuildSceneData;
            buildScenes = snapshot.BuildScenes
                .Select(scene => new ProjectSetupBuildScene(scene.SceneGuid, scene.Path, scene.Enabled))
                .ToArray();
            configureTags = snapshot.HasTagManagerData;
            tags = snapshot.CustomTags.ToArray();
            configureLayers = snapshot.HasTagManagerData;
            layers = snapshot.Layers.Skip(8).Where(value => !string.IsNullOrEmpty(value)).ToArray();
            configureSortingLayers = snapshot.HasTagManagerData;
            sortingLayers = snapshot.SortingLayers.Where(layer => layer.UniqueId != 0).Select(layer => layer.Name).ToArray();
        }

        private static ProjectSetupBuildScene[] CloneBuildScenes(ProjectSetupBuildScene[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<ProjectSetupBuildScene>();
            }

            return values.Select(value => value?.Clone() ?? new ProjectSetupBuildScene()).ToArray();
        }
    }
}
