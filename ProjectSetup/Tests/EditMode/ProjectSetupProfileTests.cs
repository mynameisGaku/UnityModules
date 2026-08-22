// SPDX-License-Identifier: MIT

using System.Linq;
using NUnit.Framework;
using ProjectSetup.Editor;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Tests
{
    internal sealed class ProjectSetupProfileTests
    {
        [Test]
        public void RecommendedDefaults_OwnOnlyVersionControlSafetySettings()
        {
            var profile = ScriptableObject.CreateInstance<ProjectSetupProfile>();
            try
            {
                profile.SetRecommendedDefaults();

                Assert.That(profile.ConfigureAssetSerialization, Is.True);
                Assert.That(profile.AssetSerialization, Is.EqualTo(SerializationMode.ForceText));
                Assert.That(profile.ConfigureVersionControl, Is.True);
                Assert.That(profile.VersionControlMode, Is.EqualTo("Visible Meta Files"));
                Assert.That(profile.ConfigureEnterPlayMode, Is.False);
                Assert.That(profile.ConfigurePlayModeStartScene, Is.False);
                Assert.That(profile.PlayModeStartScene.IsEmpty, Is.True);
                Assert.That(profile.ConfigureColorSpace, Is.False);
                Assert.That(profile.ConfigureRunInBackground, Is.False);
                Assert.That(profile.ConfigureCompanyName, Is.False);
                Assert.That(profile.ConfigureProductName, Is.False);
                Assert.That(profile.ConfigureBundleVersion, Is.False);
                Assert.That(profile.ConfigureApplicationIdentifier, Is.False);
                Assert.That(profile.ConfigureScriptingBackend, Is.False);
                Assert.That(profile.ConfigureApiCompatibilityLevel, Is.False);
                Assert.That(profile.ConfigureManagedStrippingLevel, Is.False);
                Assert.That(profile.ConfigureBuildScenes, Is.False);
                Assert.That(profile.BuildScenes, Is.Empty);
                Assert.That(profile.ConfigureTags, Is.False);
                Assert.That(profile.Tags, Is.Empty);
                Assert.That(profile.ConfigureLayers, Is.False);
                Assert.That(profile.Layers, Is.Empty);
                Assert.That(profile.ConfigureSortingLayers, Is.False);
                Assert.That(profile.SortingLayers, Is.Empty);
                Assert.That(profile.ConfigureScriptingDefineSymbols, Is.False);
                Assert.That(profile.ScriptingDefineSymbols, Is.Empty);
                Assert.That(profile.ConfigureRootNamespace, Is.False);
                Assert.That(profile.RootNamespace, Is.Empty);
                Assert.That(profile.ConfigureNewScriptLineEndings, Is.False);
                Assert.That(profile.NewScriptLineEndings, Is.EqualTo(LineEndingsMode.OSNative));
                Assert.That(profile.ConfigureNamingDefaults, Is.False);
                Assert.That(profile.GameObjectNamingScheme, Is.EqualTo(EditorSettings.NamingScheme.SpaceParenthesis));
                Assert.That(profile.GameObjectNamingDigits, Is.EqualTo(1));
                Assert.That(profile.AssetNamingUsesSpace, Is.True);
                Assert.That(profile.ConfigureProjectFolders, Is.False);
                Assert.That(profile.ProjectFolders, Is.EqualTo(new[]
                {
                    "Assets/Art",
                    "Assets/Audio",
                    "Assets/Prefabs",
                    "Assets/Scenes",
                    "Assets/Scripts",
                    "Assets/Settings"
                }));
                Assert.That(profile.ConfigureAssemblyDefinitions, Is.False);
                Assert.That(profile.AssemblyName, Is.EqualTo("Game"));
                Assert.That(profile.RuntimeAssemblyFolder, Is.EqualTo("Assets/Scripts"));
                Assert.That(profile.EditorAssemblyFolder, Is.EqualTo("Assets/Scripts/Editor"));
                Assert.That(profile.IncludeTestAssemblies, Is.False);
                Assert.That(profile.TestAssemblyRootFolder, Is.EqualTo("Assets/Tests"));
                Assert.That(profile.ConfigureVersionControlFiles, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Capture_EnablesAndCopiesEverySupportedSetting()
        {
            var profile = ScriptableObject.CreateInstance<ProjectSetupProfile>();
            try
            {
                var layers = new string[32];
                layers[8] = "Gameplay";
                layers[12] = "Interaction";
                var snapshot = new ProjectSetupSnapshot(
                    SerializationMode.Mixed,
                    "Hidden Meta Files",
                    true,
                    EnterPlayModeOptions.DisableSceneReload,
                    ColorSpace.Linear,
                    true,
                    "Company",
                    "Product",
                    "3.0.0",
                    true,
                    new[] { "Untagged", "Player", "Collectible" },
                    new[] { "Collectible" },
                    layers,
                    new[]
                    {
                        new ProjectSetupSortingLayer("Default", 0, false),
                        new ProjectSetupSortingLayer("Foreground", 15, false)
                    },
                    string.Empty,
                    true,
                    "global",
                    "Global Build Scenes",
                    new[]
                    {
                        new ProjectSetupBuildSceneState("guid-a", "Assets/Bootstrap.unity", true),
                        new ProjectSetupBuildSceneState("guid-b", "Assets/Gameplay.unity", false)
                    },
                    true,
                    "guid-a",
                    "Assets/Bootstrap.unity",
                    true,
                    "Standalone",
                    "Standalone",
                    new[] { "PROJECT_FEATURE", "DEBUG_MENU" },
                    true,
                    "Studio.Game",
                    LineEndingsMode.Unix,
                    true,
                    EditorSettings.NamingScheme.Underscore,
                    3,
                    false,
                    hasApplicationIdentifierData: true,
                    applicationIdentifierTargetId: "Standalone",
                    applicationIdentifierTargetLabel: "Windows",
                    applicationIdentifier: "com.studiogaku.game",
                    hasScriptingBackendData: true,
                    scriptingBackendTargetId: "Standalone",
                    scriptingBackendTargetLabel: "Windows",
                    scriptingBackend: ScriptingImplementation.IL2CPP,
                    hasApiCompatibilityLevelData: true,
                    apiCompatibilityLevelTargetId: "Standalone",
                    apiCompatibilityLevelTargetLabel: "Windows",
                    apiCompatibilityLevel: ApiCompatibilityLevel.NET_Unity_4_8,
                    hasManagedStrippingLevelData: true,
                    managedStrippingLevelTargetId: "Standalone",
                    managedStrippingLevelTargetLabel: "Windows",
                    managedStrippingLevel: ManagedStrippingLevel.High);

                profile.Capture(snapshot);

                Assert.That(profile.ConfigureAssetSerialization, Is.True);
                Assert.That(profile.AssetSerialization, Is.EqualTo(SerializationMode.Mixed));
                Assert.That(profile.ConfigureVersionControl, Is.True);
                Assert.That(profile.VersionControlMode, Is.EqualTo("Hidden Meta Files"));
                Assert.That(profile.ConfigureEnterPlayMode, Is.True);
                Assert.That(profile.EnterPlayModeOptions, Is.EqualTo(EnterPlayModeOptions.DisableSceneReload));
                Assert.That(profile.ConfigurePlayModeStartScene, Is.True);
                Assert.That(profile.PlayModeStartScene.SceneGuid, Is.EqualTo("guid-a"));
                Assert.That(profile.PlayModeStartScene.FallbackPath, Is.EqualTo("Assets/Bootstrap.unity"));
                Assert.That(profile.ConfigureColorSpace, Is.True);
                Assert.That(profile.ColorSpace, Is.EqualTo(ColorSpace.Linear));
                Assert.That(profile.ConfigureRunInBackground, Is.True);
                Assert.That(profile.RunInBackground, Is.True);
                Assert.That(profile.ConfigureCompanyName, Is.True);
                Assert.That(profile.CompanyName, Is.EqualTo("Company"));
                Assert.That(profile.ConfigureProductName, Is.True);
                Assert.That(profile.ProductName, Is.EqualTo("Product"));
                Assert.That(profile.ConfigureBundleVersion, Is.True);
                Assert.That(profile.BundleVersion, Is.EqualTo("3.0.0"));
                Assert.That(profile.ConfigureApplicationIdentifier, Is.True);
                Assert.That(profile.ApplicationIdentifier, Is.EqualTo("com.studiogaku.game"));
                Assert.That(profile.ConfigureScriptingBackend, Is.True);
                Assert.That(profile.ScriptingBackend, Is.EqualTo(ScriptingImplementation.IL2CPP));
                Assert.That(profile.ConfigureApiCompatibilityLevel, Is.True);
                Assert.That(profile.ApiCompatibilityLevel, Is.EqualTo(ApiCompatibilityLevel.NET_Unity_4_8));
                Assert.That(profile.ConfigureManagedStrippingLevel, Is.True);
                Assert.That(profile.ManagedStrippingLevel, Is.EqualTo(ManagedStrippingLevel.High));
                Assert.That(profile.ConfigureBuildScenes, Is.True);
                Assert.That(profile.BuildScenes.Select(scene => scene.SceneGuid), Is.EqualTo(new[] { "guid-a", "guid-b" }));
                Assert.That(profile.BuildScenes.Select(scene => scene.Enabled), Is.EqualTo(new[] { true, false }));
                Assert.That(profile.ConfigureTags, Is.True);
                Assert.That(profile.Tags, Is.EqualTo(new[] { "Collectible" }));
                Assert.That(profile.ConfigureLayers, Is.True);
                Assert.That(profile.Layers, Is.EqualTo(new[] { "Gameplay", "Interaction" }));
                Assert.That(profile.ConfigureSortingLayers, Is.True);
                Assert.That(profile.SortingLayers, Is.EqualTo(new[] { "Foreground" }));
                Assert.That(profile.ConfigureScriptingDefineSymbols, Is.True);
                Assert.That(profile.ScriptingDefineSymbols, Is.EqualTo(new[] { "PROJECT_FEATURE", "DEBUG_MENU" }));
                Assert.That(profile.ConfigureRootNamespace, Is.True);
                Assert.That(profile.RootNamespace, Is.EqualTo("Studio.Game"));
                Assert.That(profile.ConfigureNewScriptLineEndings, Is.True);
                Assert.That(profile.NewScriptLineEndings, Is.EqualTo(LineEndingsMode.Unix));
                Assert.That(profile.ConfigureNamingDefaults, Is.True);
                Assert.That(profile.GameObjectNamingScheme, Is.EqualTo(EditorSettings.NamingScheme.Underscore));
                Assert.That(profile.GameObjectNamingDigits, Is.EqualTo(3));
                Assert.That(profile.AssetNamingUsesSpace, Is.False);
                Assert.That(profile.ConfigureProjectFolders, Is.False);
                Assert.That(profile.ProjectFolders, Contains.Item("Assets/Scripts"));
                Assert.That(profile.ConfigureAssemblyDefinitions, Is.False);
                Assert.That(profile.AssemblyName, Is.EqualTo("Game"));
                Assert.That(profile.RuntimeAssemblyFolder, Is.EqualTo("Assets/Scripts"));
                Assert.That(profile.EditorAssemblyFolder, Is.EqualTo("Assets/Scripts/Editor"));
                Assert.That(profile.IncludeTestAssemblies, Is.False);
                Assert.That(profile.TestAssemblyRootFolder, Is.EqualTo("Assets/Tests"));
                Assert.That(profile.ConfigureVersionControlFiles, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
