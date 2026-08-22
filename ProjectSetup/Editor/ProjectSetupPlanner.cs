// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Editor
{
    internal static class ProjectSetupPlanner
    {
        private const int MaximumTextLength = 128;
        private const int MaximumVersionLength = 64;
        private const int MaximumNameLength = 64;
        private const int MaximumRequestedNameCount = 64;
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
            AddNameListChange(profile.ConfigureTags, profile.Tags, current.Tags, ProjectSetupSettingKey.Tags, "Tags", changes, errors);
            AddLayerChange(profile, current, changes, errors);
            AddNameListChange(
                profile.ConfigureSortingLayers,
                profile.SortingLayers,
                current.SortingLayers.Select(layer => layer.Name),
                ProjectSetupSettingKey.SortingLayers,
                "Sorting Layers",
                changes,
                errors);
            return new ProjectSetupPlan(changes, errors);
        }

        private static void AddLayerChange(
            ProjectSetupProfile profile,
            ProjectSetupSnapshot current,
            ICollection<ProjectSetupChange> changes,
            ICollection<string> errors)
        {
            if (!profile.ConfigureLayers)
            {
                return;
            }

            if (!TryGetMissingNames(profile.Layers, current.Layers, "Layers", errors, out var missing))
            {
                return;
            }

            var freeSlotCount = current.Layers.Skip(8).Count(string.IsNullOrEmpty);
            if (missing.Count > freeSlotCount)
            {
                errors.Add($"Layers requires {missing.Count} free user slots, but only {freeSlotCount} are available.");
                return;
            }

            AddMissingNames(changes, ProjectSetupSettingKey.Layers, "Layers", missing);
        }

        private static void AddNameListChange(
            bool enabled,
            IReadOnlyList<string> requested,
            IEnumerable<string> current,
            ProjectSetupSettingKey key,
            string label,
            ICollection<ProjectSetupChange> changes,
            ICollection<string> errors)
        {
            if (!enabled)
            {
                return;
            }

            if (TryGetMissingNames(requested, current, label, errors, out var missing))
            {
                AddMissingNames(changes, key, label, missing);
            }
        }

        private static bool TryGetMissingNames(
            IReadOnlyList<string> requested,
            IEnumerable<string> current,
            string label,
            ICollection<string> errors,
            out List<string> missing)
        {
            missing = new List<string>();
            if (requested.Count > MaximumRequestedNameCount)
            {
                errors.Add($"{label} supports at most {MaximumRequestedNameCount} requested names.");
                return false;
            }

            var requestedSet = new HashSet<string>(StringComparer.Ordinal);
            var existing = new HashSet<string>(current ?? Array.Empty<string>(), StringComparer.Ordinal);
            for (var index = 0; index < requested.Count; index++)
            {
                var value = requested[index];
                if (!IsValidName(value))
                {
                    errors.Add($"{label} entry {index + 1} must be trimmed, contain 1 to {MaximumNameLength} characters, and contain no control characters.");
                    return false;
                }

                if (!requestedSet.Add(value))
                {
                    errors.Add($"{label} contains the duplicate name '{value}'.");
                    return false;
                }

                if (!existing.Contains(value))
                {
                    missing.Add(value);
                }
            }

            return true;
        }

        private static bool IsValidName(string value)
        {
            if (string.IsNullOrEmpty(value)
                || value.Length > MaximumNameLength
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddMissingNames(
            ICollection<ProjectSetupChange> changes,
            ProjectSetupSettingKey key,
            string label,
            IReadOnlyList<string> missing)
        {
            if (missing.Count > 0)
            {
                changes.Add(new ProjectSetupChange(key, label, "Already configured names are preserved", $"Add: {string.Join(", ", missing)}"));
            }
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
