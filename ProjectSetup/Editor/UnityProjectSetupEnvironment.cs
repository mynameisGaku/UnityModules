// SPDX-License-Identifier: MIT

using UnityEditor;

namespace ProjectSetup.Editor
{
    internal sealed class UnityProjectSetupEnvironment : IProjectSetupEnvironment
    {
        public bool IsAvailable => !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling
            && !EditorApplication.isUpdating;

        public ProjectSetupSnapshot Capture()
        {
            return new ProjectSetupSnapshot(
                EditorSettings.serializationMode,
                VersionControlSettings.mode,
                EditorSettings.enterPlayModeOptionsEnabled,
                EditorSettings.enterPlayModeOptions,
                PlayerSettings.colorSpace,
                PlayerSettings.runInBackground,
                PlayerSettings.companyName,
                PlayerSettings.productName,
                PlayerSettings.bundleVersion);
        }

        public void Apply(ProjectSetupProfile profile)
        {
            if (profile.ConfigureAssetSerialization)
            {
                EditorSettings.serializationMode = profile.AssetSerialization;
            }

            if (profile.ConfigureVersionControl)
            {
                VersionControlSettings.mode = profile.VersionControlMode;
            }

            if (profile.ConfigureEnterPlayMode)
            {
                EditorSettings.enterPlayModeOptionsEnabled = profile.EnterPlayModeOptionsEnabled;
                EditorSettings.enterPlayModeOptions = profile.EnterPlayModeOptions;
            }

            if (profile.ConfigureColorSpace)
            {
                PlayerSettings.colorSpace = profile.ColorSpace;
            }

            if (profile.ConfigureRunInBackground)
            {
                PlayerSettings.runInBackground = profile.RunInBackground;
            }

            if (profile.ConfigureCompanyName)
            {
                PlayerSettings.companyName = profile.CompanyName;
            }

            if (profile.ConfigureProductName)
            {
                PlayerSettings.productName = profile.ProductName;
            }

            if (profile.ConfigureBundleVersion)
            {
                PlayerSettings.bundleVersion = profile.BundleVersion;
            }

            AssetDatabase.SaveAssets();
        }

        public void Apply(ProjectSetupSnapshot snapshot)
        {
            EditorSettings.serializationMode = snapshot.AssetSerialization;
            VersionControlSettings.mode = snapshot.VersionControlMode;
            EditorSettings.enterPlayModeOptionsEnabled = snapshot.EnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = snapshot.EnterPlayModeOptions;
            PlayerSettings.colorSpace = snapshot.ColorSpace;
            PlayerSettings.runInBackground = snapshot.RunInBackground;
            PlayerSettings.companyName = snapshot.CompanyName;
            PlayerSettings.productName = snapshot.ProductName;
            PlayerSettings.bundleVersion = snapshot.BundleVersion;
            AssetDatabase.SaveAssets();
        }
    }
}
