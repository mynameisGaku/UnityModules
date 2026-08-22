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
        private const int MaximumBuildSceneCount = 64;
        private const int MaximumScriptingDefineLength = 64;
        private const int MaximumRootNamespaceLength = 128;
        private const int MinimumGameObjectNamingDigits = 1;
        private const int MaximumGameObjectNamingDigits = 9;
        private const EnterPlayModeOptions KnownEnterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload
            | EnterPlayModeOptions.DisableSceneReload;
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum",
            "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto",
            "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
            "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly",
            "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct",
            "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while"
        };

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
            AddPlayModeStartSceneChange(profile, current, changes, errors);
            AddBuildSceneChange(profile, current, changes, errors);
            AddScriptingDefineChange(profile, current, changes, errors);
            AddCodeGenerationChange(profile, current, changes, errors);
            AddNamingDefaultsChange(profile, current, changes, errors);
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

        private static void AddNamingDefaultsChange(
            ProjectSetupProfile profile,
            ProjectSetupSnapshot current,
            ICollection<ProjectSetupChange> changes,
            ICollection<string> errors)
        {
            if (!profile.ConfigureNamingDefaults)
            {
                return;
            }

            if (!current.HasNamingData)
            {
                errors.Add("Duplicate Naming settings are unavailable in this Unity version.");
                return;
            }

            if (!Enum.IsDefined(typeof(EditorSettings.NamingScheme), profile.GameObjectNamingScheme))
            {
                errors.Add("GameObject Naming Scheme contains an unsupported value.");
            }
            else if (current.GameObjectNamingScheme != profile.GameObjectNamingScheme)
            {
                Add(
                    changes,
                    ProjectSetupSettingKey.GameObjectNamingScheme,
                    "GameObject Naming Scheme",
                    current.GameObjectNamingScheme,
                    profile.GameObjectNamingScheme);
            }

            if (profile.GameObjectNamingDigits < MinimumGameObjectNamingDigits
                || profile.GameObjectNamingDigits > MaximumGameObjectNamingDigits)
            {
                errors.Add($"GameObject Naming Digits must be between {MinimumGameObjectNamingDigits} and {MaximumGameObjectNamingDigits}.");
            }
            else if (current.GameObjectNamingDigits != profile.GameObjectNamingDigits)
            {
                Add(
                    changes,
                    ProjectSetupSettingKey.GameObjectNamingDigits,
                    "GameObject Naming Digits",
                    current.GameObjectNamingDigits,
                    profile.GameObjectNamingDigits);
            }

            if (current.AssetNamingUsesSpace != profile.AssetNamingUsesSpace)
            {
                Add(
                    changes,
                    ProjectSetupSettingKey.AssetNamingUsesSpace,
                    "Asset Copy Number Spacing",
                    current.AssetNamingUsesSpace ? "Use a space" : "No space",
                    profile.AssetNamingUsesSpace ? "Use a space" : "No space");
            }
        }

        private static void AddCodeGenerationChange(
            ProjectSetupProfile profile,
            ProjectSetupSnapshot current,
            ICollection<ProjectSetupChange> changes,
            ICollection<string> errors)
        {
            if (!profile.ConfigureRootNamespace && !profile.ConfigureNewScriptLineEndings)
            {
                return;
            }

            if (!current.HasCodeGenerationData)
            {
                errors.Add("Code Generation settings are unavailable in this Unity version.");
                return;
            }

            if (profile.ConfigureRootNamespace)
            {
                if (!IsValidRootNamespace(profile.RootNamespace))
                {
                    errors.Add($"Root Namespace must be empty or a valid C# namespace up to {MaximumRootNamespaceLength} characters.");
                }
                else if (!string.Equals(current.RootNamespace, profile.RootNamespace, StringComparison.Ordinal))
                {
                    Add(
                        changes,
                        ProjectSetupSettingKey.RootNamespace,
                        "Root Namespace",
                        FormatRootNamespace(current.RootNamespace),
                        FormatRootNamespace(profile.RootNamespace));
                }
            }

            if (profile.ConfigureNewScriptLineEndings)
            {
                if (!Enum.IsDefined(typeof(LineEndingsMode), profile.NewScriptLineEndings))
                {
                    errors.Add("New Script Line Endings contains an unsupported value.");
                }
                else if (current.NewScriptLineEndings != profile.NewScriptLineEndings)
                {
                    Add(
                        changes,
                        ProjectSetupSettingKey.NewScriptLineEndings,
                        "New Script Line Endings",
                        current.NewScriptLineEndings,
                        profile.NewScriptLineEndings);
                }
            }
        }

        private static void AddScriptingDefineChange(
            ProjectSetupProfile profile,
            ProjectSetupSnapshot current,
            ICollection<ProjectSetupChange> changes,
            ICollection<string> errors)
        {
            if (!profile.ConfigureScriptingDefineSymbols)
            {
                return;
            }

            if (!current.HasScriptingDefineData)
            {
                errors.Add("Scripting Define Symbols are unavailable for the active build target.");
                return;
            }

            var requested = profile.ScriptingDefineSymbols;
            if (requested.Length > MaximumRequestedNameCount)
            {
                errors.Add($"Scripting Define Symbols supports at most {MaximumRequestedNameCount} requested symbols.");
                return;
            }

            var requestedSet = new HashSet<string>(StringComparer.Ordinal);
            var desired = new List<string>(current.ScriptingDefineSymbols);
            var existing = new HashSet<string>(current.ScriptingDefineSymbols, StringComparer.Ordinal);
            for (var index = 0; index < requested.Length; index++)
            {
                var symbol = requested[index];
                if (!IsValidScriptingDefine(symbol))
                {
                    errors.Add($"Scripting Define Symbols entry {index + 1} must use 1 to {MaximumScriptingDefineLength} ASCII letters, digits, or underscores and cannot start with a digit.");
                    return;
                }

                if (!requestedSet.Add(symbol))
                {
                    errors.Add($"Scripting Define Symbols contains the duplicate symbol '{symbol}'.");
                    return;
                }

                if (existing.Add(symbol))
                {
                    desired.Add(symbol);
                }
            }

            if (desired.Count != current.ScriptingDefineSymbols.Length)
            {
                changes.Add(new ProjectSetupChange(
                    ProjectSetupSettingKey.ScriptingDefineSymbols,
                    $"Scripting Define Symbols ({current.ScriptingDefineTargetLabel})",
                    FormatScriptingDefines(current.ScriptingDefineSymbols),
                    FormatScriptingDefines(desired)));
            }
        }

        private static void AddPlayModeStartSceneChange(
            ProjectSetupProfile profile,
            ProjectSetupSnapshot current,
            ICollection<ProjectSetupChange> changes,
            ICollection<string> errors)
        {
            if (!profile.ConfigurePlayModeStartScene)
            {
                return;
            }

            var desiredReference = profile.PlayModeStartScene;
            var desiredGuid = string.Empty;
            var desiredPath = string.Empty;
            if (!desiredReference.IsEmpty)
            {
                if (!desiredReference.TryResolve(out desiredPath))
                {
                    errors.Add("Play Mode Start Scene must be empty or reference an existing Scene Asset.");
                    return;
                }

                desiredGuid = AssetDatabase.AssetPathToGUID(desiredPath);
            }

            if (!ProjectSetupSceneReference.SameIdentity(
                    current.PlayModeStartSceneGuid,
                    current.PlayModeStartScenePath,
                    desiredGuid,
                    desiredPath))
            {
                changes.Add(new ProjectSetupChange(
                    ProjectSetupSettingKey.PlayModeStartScene,
                    "Play Mode Start Scene",
                    FormatPlayModeStartScene(current.PlayModeStartScenePath),
                    FormatPlayModeStartScene(desiredPath)));
            }
        }

        private static void AddBuildSceneChange(
            ProjectSetupProfile profile,
            ProjectSetupSnapshot current,
            ICollection<ProjectSetupChange> changes,
            ICollection<string> errors)
        {
            if (!profile.ConfigureBuildScenes)
            {
                return;
            }

            var requested = profile.BuildScenes;
            if (requested.Length == 0)
            {
                errors.Add("Build Scenes requires at least one Scene.");
                return;
            }

            if (requested.Length > MaximumBuildSceneCount)
            {
                errors.Add($"Build Scenes supports at most {MaximumBuildSceneCount} Scenes.");
                return;
            }

            if (requested[0] == null || !requested[0].Enabled)
            {
                errors.Add("The first Build Scene must be enabled because it is the Player startup Scene.");
                return;
            }

            var desired = new ProjectSetupBuildSceneState[requested.Length];
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < requested.Length; index++)
            {
                var entry = requested[index];
                if (entry == null || !entry.TryResolve(out var path))
                {
                    errors.Add($"Build Scene {index + 1} must reference an existing Scene Asset.");
                    return;
                }

                if (!paths.Add(path))
                {
                    errors.Add($"Build Scenes contains the duplicate Scene '{path}'.");
                    return;
                }

                desired[index] = new ProjectSetupBuildSceneState(AssetDatabase.AssetPathToGUID(path), path, entry.Enabled);
            }

            if (!SequenceEqual(current.BuildScenes, desired))
            {
                changes.Add(new ProjectSetupChange(
                    ProjectSetupSettingKey.BuildScenes,
                    "Build Scenes",
                    FormatBuildScenes(current.BuildScenes),
                    FormatBuildScenes(desired)));
            }
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

        private static bool IsValidScriptingDefine(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaximumScriptingDefineLength)
            {
                return false;
            }

            if (!IsAsciiLetter(value[0]) && value[0] != '_')
            {
                return false;
            }

            for (var index = 1; index < value.Length; index++)
            {
                var character = value[index];
                if (!IsAsciiLetter(character) && !IsAsciiDigit(character) && character != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAsciiLetter(char value)
        {
            return (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z');
        }

        private static bool IsAsciiDigit(char value)
        {
            return value >= '0' && value <= '9';
        }

        private static bool IsValidRootNamespace(string value)
        {
            if (value == null || value.Length > MaximumRootNamespaceLength)
            {
                return false;
            }

            if (value.Length == 0)
            {
                return true;
            }

            var segments = value.Split('.');
            foreach (var segment in segments)
            {
                if (segment.Length == 0 || CSharpKeywords.Contains(segment))
                {
                    return false;
                }

                if (!IsAsciiLetter(segment[0]) && segment[0] != '_')
                {
                    return false;
                }

                for (var index = 1; index < segment.Length; index++)
                {
                    var character = segment[index];
                    if (!IsAsciiLetter(character) && !IsAsciiDigit(character) && character != '_')
                    {
                        return false;
                    }
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

        private static string FormatRootNamespace(string value)
        {
            return string.IsNullOrEmpty(value) ? "No root namespace" : value;
        }

        private static void Add<T>(ICollection<ProjectSetupChange> changes, ProjectSetupSettingKey key, string label, T current, T desired)
        {
            changes.Add(new ProjectSetupChange(key, label, current?.ToString() ?? string.Empty, desired?.ToString() ?? string.Empty));
        }

        private static bool SequenceEqual(IReadOnlyList<ProjectSetupBuildSceneState> left, IReadOnlyList<ProjectSetupBuildSceneState> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                if (!left[index].Equals(right[index]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static string FormatBuildScenes(IReadOnlyList<ProjectSetupBuildSceneState> scenes)
        {
            if (scenes == null || scenes.Count == 0)
            {
                return "No Scenes";
            }

            return string.Join(", ", scenes.Select((scene, index) =>
                $"{index + 1}. {System.IO.Path.GetFileNameWithoutExtension(scene.Path)} ({(scene.Enabled ? "Enabled" : "Disabled")})"));
        }

        internal static string FormatScriptingDefines(IReadOnlyList<string> symbols)
        {
            return symbols == null || symbols.Count == 0 ? "No custom symbols" : string.Join(";", symbols);
        }

        private static string FormatPlayModeStartScene(string path)
        {
            return string.IsNullOrEmpty(path) ? "Currently open Scenes" : path;
        }
    }
}
