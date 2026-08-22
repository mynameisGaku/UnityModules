// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Editor
{
    internal static class ProjectSetupPlanner
    {
        private const int MaximumTextLength = 128;
        private const int MaximumVersionLength = 64;
        private const EnterPlayModeOptions KnownEnterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload
            | EnterPlayModeOptions.DisableSceneReload;

        internal static ProjectSetupPlan Build(ProjectSetupProfile profile, ProjectSetupSnapshot current)
        {
            var changes = new List<ProjectSetupChange>();
            var errors = new List<string>();
            if (profile == null)
            {
                errors.Add("Select a Project Setup profile.");
                return new ProjectSetupPlan(changes, errors);
            }

            if (profile.ConfigureAssetSerialization)
            {
                if (!Enum.IsDefined(typeof(SerializationMode), profile.AssetSerialization))
                {
                    errors.Add("Asset Serialization contains an unsupported value.");
                }
                else if (current.AssetSerialization != profile.AssetSerialization)
                {
                    Add(changes, ProjectSetupSettingKey.AssetSerialization, "Asset Serialization", current.AssetSerialization, profile.AssetSerialization);
                }
            }

            if (profile.ConfigureVersionControl)
            {
                if (!IsValidRequiredText(profile.VersionControlMode, MaximumTextLength))
                {
                    errors.Add("Version Control must contain 1 to 128 characters.");
                }
                else if (!string.Equals(current.VersionControlMode, profile.VersionControlMode, StringComparison.Ordinal))
                {
                    Add(changes, ProjectSetupSettingKey.VersionControl, "Version Control", current.VersionControlMode, profile.VersionControlMode);
                }
            }

            if (profile.ConfigureEnterPlayMode)
            {
                if ((profile.EnterPlayModeOptions & ~KnownEnterPlayModeOptions) != 0)
                {
                    errors.Add("Enter Play Mode Options contains unsupported flags.");
                }
                else if (profile.EnterPlayModeOptionsEnabled && profile.EnterPlayModeOptions == EnterPlayModeOptions.None)
                {
                    errors.Add("Select at least one disabled reload option when custom Enter Play Mode Options are enabled.");
                }
                else
                {
                    var currentText = FormatEnterPlayMode(current.EnterPlayModeOptionsEnabled, current.EnterPlayModeOptions);
                    var desiredText = FormatEnterPlayMode(profile.EnterPlayModeOptionsEnabled, profile.EnterPlayModeOptions);
                    if (!string.Equals(currentText, desiredText, StringComparison.Ordinal))
                    {
                        Add(changes, ProjectSetupSettingKey.EnterPlayMode, "Enter Play Mode", currentText, desiredText);
                    }
                }
            }

            if (profile.ConfigureColorSpace)
            {
                if (profile.ColorSpace != ColorSpace.Gamma && profile.ColorSpace != ColorSpace.Linear)
                {
                    errors.Add("Color Space must be Gamma or Linear.");
                }
                else if (current.ColorSpace != profile.ColorSpace)
                {
                    Add(changes, ProjectSetupSettingKey.ColorSpace, "Color Space", current.ColorSpace, profile.ColorSpace);
                }
            }

            if (profile.ConfigureRunInBackground && current.RunInBackground != profile.RunInBackground)
            {
                Add(changes, ProjectSetupSettingKey.RunInBackground, "Run In Background", current.RunInBackground, profile.RunInBackground);
            }

            AddTextChange(profile.ConfigureCompanyName, profile.CompanyName, current.CompanyName, ProjectSetupSettingKey.CompanyName, "Company Name", MaximumTextLength, changes, errors);
            AddTextChange(profile.ConfigureProductName, profile.ProductName, current.ProductName, ProjectSetupSettingKey.ProductName, "Product Name", MaximumTextLength, changes, errors);
            AddTextChange(profile.ConfigureBundleVersion, profile.BundleVersion, current.BundleVersion, ProjectSetupSettingKey.BundleVersion, "Bundle Version", MaximumVersionLength, changes, errors);
            return new ProjectSetupPlan(changes, errors);
        }

        private static void AddTextChange(
            bool enabled,
            string desired,
            string current,
            ProjectSetupSettingKey key,
            string label,
            int maximumLength,
            ICollection<ProjectSetupChange> changes,
            ICollection<string> errors)
        {
            if (!enabled)
            {
                return;
            }

            if (!IsValidRequiredText(desired, maximumLength))
            {
                errors.Add($"{label} must contain 1 to {maximumLength} characters.");
                return;
            }

            if (!string.Equals(current, desired, StringComparison.Ordinal))
            {
                Add(changes, key, label, current, desired);
            }
        }

        private static bool IsValidRequiredText(string value, int maximumLength)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;
        }

        private static string FormatEnterPlayMode(bool enabled, EnterPlayModeOptions options)
        {
            return enabled ? options.ToString() : "Default reloads";
        }

        private static void Add<T>(ICollection<ProjectSetupChange> changes, ProjectSetupSettingKey key, string label, T current, T desired)
        {
            changes.Add(new ProjectSetupChange(key, label, current?.ToString() ?? string.Empty, desired?.ToString() ?? string.Empty));
        }
    }
}
