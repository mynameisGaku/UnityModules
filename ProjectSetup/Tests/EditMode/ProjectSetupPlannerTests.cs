// SPDX-License-Identifier: MIT

using System.Linq;
using NUnit.Framework;
using ProjectSetup.Editor;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Tests
{
    internal sealed class ProjectSetupPlannerTests
    {
        private ProjectSetupProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<ProjectSetupProfile>();
            _profile.SetRecommendedDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_profile);
        }

        [Test]
        public void Build_IncludesOnlyEnabledDifferencesInStableOrder()
        {
            _profile.ConfigureColorSpace = true;
            _profile.ColorSpace = ColorSpace.Linear;
            _profile.ConfigureRunInBackground = true;
            _profile.RunInBackground = true;
            var current = Snapshot(SerializationMode.Mixed, "Hidden Meta Files", ColorSpace.Gamma, false);

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Changes.Count, Is.EqualTo(4));
            Assert.That(plan.Changes[0].Key, Is.EqualTo(ProjectSetupSettingKey.AssetSerialization));
            Assert.That(plan.Changes[1].Key, Is.EqualTo(ProjectSetupSettingKey.VersionControl));
            Assert.That(plan.Changes[2].Key, Is.EqualTo(ProjectSetupSettingKey.ColorSpace));
            Assert.That(plan.Changes[3].Key, Is.EqualTo(ProjectSetupSettingKey.RunInBackground));
        }

        [Test]
        public void Build_DisabledSettingsDoNotChange()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            var current = Snapshot(SerializationMode.Mixed, "Hidden Meta Files", ColorSpace.Gamma, false);

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.HasChanges, Is.False);
        }

        [Test]
        public void Build_RejectsMissingProfile()
        {
            var plan = ProjectSetupPlanner.Build(null, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors[0], Does.Contain("Select"));
        }

        [Test]
        public void Build_RejectsBlankEnabledText()
        {
            _profile.ConfigureCompanyName = true;
            _profile.CompanyName = "   ";

            var plan = ProjectSetupPlanner.Build(_profile, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Company Name"));
        }

        [Test]
        public void Build_PlansApplicationIdentifierForActiveTarget()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureApplicationIdentifier = true;
            _profile.ApplicationIdentifier = "com.studiogaku.sample";
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0",
                hasApplicationIdentifierData: true,
                applicationIdentifierTargetId: "Standalone",
                applicationIdentifierTargetLabel: "Standalone",
                applicationIdentifier: "com.company.product");

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.True, string.Join("\n", plan.Errors));
            Assert.That(plan.Changes, Has.Exactly(1).Property("Key").EqualTo(ProjectSetupSettingKey.ApplicationIdentifier));
            Assert.That(plan.Changes.Single().DesiredValue, Is.EqualTo("com.studiogaku.sample"));
        }

        [Test]
        public void Build_PlansScriptingBackendForActiveTarget()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureScriptingBackend = true;
            _profile.ScriptingBackend = ScriptingImplementation.IL2CPP;
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Linear,
                false,
                "Company",
                "Product",
                "1.0.0",
                hasScriptingBackendData: true,
                scriptingBackendTargetId: "Standalone",
                scriptingBackendTargetLabel: "Windows",
                scriptingBackend: ScriptingImplementation.Mono2x);

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.Errors, Is.Empty);
            Assert.That(plan.Changes, Has.Exactly(1).Property("Key").EqualTo(ProjectSetupSettingKey.ScriptingBackend));
            Assert.That(plan.Changes[0].Label, Does.Contain("Windows"));
        }

        [Test]
        public void Build_RejectsUnsupportedScriptingBackend()
        {
            _profile.ConfigureScriptingBackend = true;
            _profile.ScriptingBackend = ScriptingImplementation.WinRTDotNET;
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Linear,
                false,
                "Company",
                "Product",
                "1.0.0",
                hasScriptingBackendData: true,
                scriptingBackendTargetId: "Standalone",
                scriptingBackendTargetLabel: "Windows",
                scriptingBackend: ScriptingImplementation.Mono2x);

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Mono or IL2CPP"));
        }

        [Test]
        public void Build_RejectsScriptingBackendWhenActiveTargetIsUnavailable()
        {
            _profile.ConfigureScriptingBackend = true;
            _profile.ScriptingBackend = ScriptingImplementation.IL2CPP;

            var plan = ProjectSetupPlanner.Build(_profile, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("unavailable"));
        }

        [Test]
        public void Build_PlansApiCompatibilityLevelForActiveTarget()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureApiCompatibilityLevel = true;
            _profile.ApiCompatibilityLevel = ApiCompatibilityLevel.NET_Unity_4_8;
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Linear,
                false,
                "Company",
                "Product",
                "1.0.0",
                hasApiCompatibilityLevelData: true,
                apiCompatibilityLevelTargetId: "Standalone",
                apiCompatibilityLevelTargetLabel: "Windows",
                apiCompatibilityLevel: ApiCompatibilityLevel.NET_Standard);

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.Errors, Is.Empty);
            Assert.That(plan.Changes, Has.Exactly(1).Property("Key").EqualTo(ProjectSetupSettingKey.ApiCompatibilityLevel));
            Assert.That(plan.Changes[0].Label, Does.Contain("Windows"));
        }

        [Test]
        public void Build_RejectsLegacyApiCompatibilityLevel()
        {
            _profile.ConfigureApiCompatibilityLevel = true;
            _profile.ApiCompatibilityLevel = ApiCompatibilityLevel.NET_2_0;
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Linear,
                false,
                "Company",
                "Product",
                "1.0.0",
                hasApiCompatibilityLevelData: true,
                apiCompatibilityLevelTargetId: "Standalone",
                apiCompatibilityLevelTargetLabel: "Windows",
                apiCompatibilityLevel: ApiCompatibilityLevel.NET_Standard);

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains(".NET Standard or .NET Framework"));
        }

        [Test]
        public void Build_RejectsApiCompatibilityLevelWhenActiveTargetIsUnavailable()
        {
            _profile.ConfigureApiCompatibilityLevel = true;
            _profile.ApiCompatibilityLevel = ApiCompatibilityLevel.NET_Unity_4_8;

            var plan = ProjectSetupPlanner.Build(_profile, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("API Compatibility Level is unavailable"));
        }

        [Test]
        public void Build_PlansManagedStrippingLevelForActiveTarget()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureManagedStrippingLevel = true;
            _profile.ManagedStrippingLevel = ManagedStrippingLevel.High;
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Linear,
                false,
                "Company",
                "Product",
                "1.0.0",
                hasManagedStrippingLevelData: true,
                managedStrippingLevelTargetId: "Standalone",
                managedStrippingLevelTargetLabel: "Windows",
                managedStrippingLevel: ManagedStrippingLevel.Minimal);

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.Errors, Is.Empty);
            Assert.That(plan.Changes, Has.Exactly(1).Property("Key").EqualTo(ProjectSetupSettingKey.ManagedStrippingLevel));
            Assert.That(plan.Changes[0].Label, Does.Contain("Windows"));
        }

        [Test]
        public void Build_RejectsUnsupportedManagedStrippingLevel()
        {
            _profile.ConfigureManagedStrippingLevel = true;
            _profile.ManagedStrippingLevel = (ManagedStrippingLevel)99;
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Linear,
                false,
                "Company",
                "Product",
                "1.0.0",
                hasManagedStrippingLevelData: true,
                managedStrippingLevelTargetId: "Standalone",
                managedStrippingLevelTargetLabel: "Windows",
                managedStrippingLevel: ManagedStrippingLevel.Minimal);

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Disabled, Minimal, Low, Medium, or High"));
        }

        [Test]
        public void Build_RejectsManagedStrippingLevelWhenActiveTargetIsUnavailable()
        {
            _profile.ConfigureManagedStrippingLevel = true;
            _profile.ManagedStrippingLevel = ManagedStrippingLevel.High;

            var plan = ProjectSetupPlanner.Build(_profile, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Managed Stripping Level is unavailable"));
        }

        [TestCase("game")]
        [TestCase("Com.company.game")]
        [TestCase("com.1company.game")]
        [TestCase("com.company.game-name")]
        [TestCase("com..game")]
        public void Build_RejectsInvalidApplicationIdentifier(string value)
        {
            _profile.ConfigureApplicationIdentifier = true;
            _profile.ApplicationIdentifier = value;
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0",
                hasApplicationIdentifierData: true,
                applicationIdentifierTargetId: "Standalone",
                applicationIdentifierTargetLabel: "Standalone",
                applicationIdentifier: "com.company.product");

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Application Identifier"));
        }

        [Test]
        public void Build_RejectsApplicationIdentifierWhenActiveTargetIsUnavailable()
        {
            _profile.ConfigureApplicationIdentifier = true;
            _profile.ApplicationIdentifier = "com.studiogaku.sample";

            var plan = ProjectSetupPlanner.Build(_profile, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("unavailable"));
        }

        [Test]
        public void Build_RejectsUnknownEnterPlayModeFlags()
        {
            _profile.ConfigureEnterPlayMode = true;
            _profile.EnterPlayModeOptionsEnabled = true;
            _profile.EnterPlayModeOptions = (EnterPlayModeOptions)(1 << 20);

            var plan = ProjectSetupPlanner.Build(_profile, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("unsupported flags"));
        }

        [Test]
        public void Build_RejectsEnabledEnterPlayModeWithoutDisabledReloadOption()
        {
            _profile.ConfigureEnterPlayMode = true;
            _profile.EnterPlayModeOptionsEnabled = true;
            _profile.EnterPlayModeOptions = EnterPlayModeOptions.None;

            var plan = ProjectSetupPlanner.Build(_profile, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("at least one"));
        }

        [Test]
        public void Build_DefaultReloadIgnoresStoredFlags()
        {
            _profile.ConfigureEnterPlayMode = true;
            _profile.EnterPlayModeOptionsEnabled = false;
            _profile.EnterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.DisableSceneReload,
                ColorSpace.Gamma,
                false,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0");

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.Changes, Has.None.Property("Key").EqualTo(ProjectSetupSettingKey.EnterPlayMode));
        }

        [Test]
        public void Build_AddsOnlyMissingTagManagerNamesInStableCategoryOrder()
        {
            _profile.ConfigureTags = true;
            _profile.Tags = new[] { "ExistingTag", "NewTag" };
            _profile.ConfigureLayers = true;
            _profile.Layers = new[] { "ExistingLayer", "NewLayer" };
            _profile.ConfigureSortingLayers = true;
            _profile.SortingLayers = new[] { "ExistingSorting", "NewSorting" };
            var current = SnapshotWithTagManager();

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Changes.Select(change => change.Key), Is.EqualTo(new[]
            {
                ProjectSetupSettingKey.Tags,
                ProjectSetupSettingKey.Layers,
                ProjectSetupSettingKey.SortingLayers
            }));
            Assert.That(plan.Changes[0].DesiredValue, Does.Contain("NewTag").And.Not.Contain("ExistingTag"));
            Assert.That(plan.Changes[1].DesiredValue, Does.Contain("NewLayer").And.Not.Contain("ExistingLayer"));
            Assert.That(plan.Changes[2].DesiredValue, Does.Contain("NewSorting").And.Not.Contain("ExistingSorting"));
        }

        [Test]
        public void Build_RejectsDuplicateOrUntrimmedTagManagerNames()
        {
            _profile.ConfigureTags = true;
            _profile.Tags = new[] { "Duplicate", "Duplicate" };
            _profile.ConfigureSortingLayers = true;
            _profile.SortingLayers = new[] { " Untrimmed" };

            var plan = ProjectSetupPlanner.Build(_profile, SnapshotWithTagManager());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("duplicate"));
            Assert.That(plan.Errors, Has.Some.Contains("trimmed"));
        }

        [Test]
        public void Build_RejectsLayersWhenFreeUserSlotsAreInsufficient()
        {
            _profile.ConfigureLayers = true;
            _profile.Layers = new[] { "OverflowLayer" };
            var current = SnapshotWithTagManager(fillUserLayers: true);

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("free user slots"));
        }

        [Test]
        public void Build_RejectsEmptyBuildSceneList()
        {
            _profile.ConfigureBuildScenes = true;

            var plan = ProjectSetupPlanner.Build(_profile, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("at least one Scene"));
        }

        [Test]
        public void Build_RejectsDisabledStartupScene()
        {
            _profile.ConfigureBuildScenes = true;
            _profile.BuildScenes = new[] { new ProjectSetupBuildScene(string.Empty, string.Empty, false) };

            var plan = ProjectSetupPlanner.Build(_profile, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("startup Scene"));
        }

        [Test]
        public void BuildSceneState_UsesGuidIdentityAfterSceneMove()
        {
            var beforeMove = new ProjectSetupBuildSceneState("scene-guid", "Assets/Old/Bootstrap.unity", true);
            var afterMove = new ProjectSetupBuildSceneState("scene-guid", "Assets/New/Bootstrap.unity", true);

            Assert.That(afterMove, Is.EqualTo(beforeMove));
            Assert.That(afterMove.GetHashCode(), Is.EqualTo(beforeMove.GetHashCode()));
        }

        [Test]
        public void Build_PlayModeStartSceneCanReturnToCurrentlyOpenScenes()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigurePlayModeStartScene = true;
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0",
                hasPlayModeStartSceneData: true,
                playModeStartSceneGuid: "bootstrap-guid",
                playModeStartScenePath: "Assets/Bootstrap.unity");

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Changes, Has.Exactly(1).Property("Key").EqualTo(ProjectSetupSettingKey.PlayModeStartScene));
            Assert.That(plan.Changes[0].DesiredValue, Is.EqualTo("Currently open Scenes"));
        }

        [Test]
        public void Build_RejectsMissingPlayModeStartScene()
        {
            _profile.ConfigurePlayModeStartScene = true;
            _profile.PlayModeStartScene = new ProjectSetupSceneReference("missing-guid", "Assets/Missing.unity");

            var plan = ProjectSetupPlanner.Build(_profile, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Play Mode Start Scene"));
        }

        [Test]
        public void Snapshot_PlayModeStartSceneUsesGuidIdentityAfterMove()
        {
            var beforeMove = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0",
                hasPlayModeStartSceneData: true,
                playModeStartSceneGuid: "scene-guid",
                playModeStartScenePath: "Assets/Old/Bootstrap.unity");
            var afterMove = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0",
                hasPlayModeStartSceneData: true,
                playModeStartSceneGuid: "scene-guid",
                playModeStartScenePath: "Assets/New/Bootstrap.unity");

            Assert.That(afterMove, Is.EqualTo(beforeMove));
            Assert.That(afterMove.GetHashCode(), Is.EqualTo(beforeMove.GetHashCode()));
        }

        [Test]
        public void Build_AddsOnlyMissingScriptingDefineSymbolsForActiveTarget()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureScriptingDefineSymbols = true;
            _profile.ScriptingDefineSymbols = new[] { "EXISTING_FEATURE", "NEW_FEATURE" };
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0",
                hasScriptingDefineData: true,
                scriptingDefineTargetId: "Standalone",
                scriptingDefineTargetLabel: "Standalone",
                scriptingDefineSymbols: new[] { "EXISTING_FEATURE", "USER_SYMBOL" });

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Changes, Has.Exactly(1).Property("Key").EqualTo(ProjectSetupSettingKey.ScriptingDefineSymbols));
            Assert.That(plan.Changes[0].CurrentValue, Is.EqualTo("EXISTING_FEATURE;USER_SYMBOL"));
            Assert.That(plan.Changes[0].DesiredValue, Is.EqualTo("EXISTING_FEATURE;USER_SYMBOL;NEW_FEATURE"));
        }

        [TestCase("1INVALID")]
        [TestCase("INVALID-SYMBOL")]
        [TestCase("NON_ASCII_\u30B7\u30F3\u30DC\u30EB")]
        public void Build_RejectsInvalidScriptingDefineSymbol(string symbol)
        {
            _profile.ConfigureScriptingDefineSymbols = true;
            _profile.ScriptingDefineSymbols = new[] { symbol };
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0",
                hasScriptingDefineData: true,
                scriptingDefineTargetId: "Standalone",
                scriptingDefineTargetLabel: "Standalone",
                scriptingDefineSymbols: new string[0]);

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Scripting Define Symbols"));
        }

        [Test]
        public void Build_PlansRootNamespaceAndNewScriptLineEndingsTogether()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureRootNamespace = true;
            _profile.RootNamespace = "Studio.Game";
            _profile.ConfigureNewScriptLineEndings = true;
            _profile.NewScriptLineEndings = LineEndingsMode.Unix;
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0",
                hasCodeGenerationData: true,
                rootNamespace: "",
                newScriptLineEndings: LineEndingsMode.Windows);

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Changes, Has.Some.Property("Key").EqualTo(ProjectSetupSettingKey.RootNamespace));
            Assert.That(plan.Changes, Has.Some.Property("Key").EqualTo(ProjectSetupSettingKey.NewScriptLineEndings));
        }

        [TestCase("1Studio")]
        [TestCase("Studio..Game")]
        [TestCase("class.Game")]
        [TestCase("Studio.Game-Tools")]
        [TestCase("Studio.\u30B2\u30FC\u30E0")]
        public void Build_RejectsInvalidRootNamespace(string value)
        {
            _profile.ConfigureRootNamespace = true;
            _profile.RootNamespace = value;
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0",
                hasCodeGenerationData: true);

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Root Namespace"));
        }

        [Test]
        public void Build_PlansDuplicateNamingSettingsTogether()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureNamingDefaults = true;
            _profile.GameObjectNamingScheme = EditorSettings.NamingScheme.Underscore;
            _profile.GameObjectNamingDigits = 3;
            _profile.AssetNamingUsesSpace = false;
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0",
                hasNamingData: true,
                gameObjectNamingScheme: EditorSettings.NamingScheme.SpaceParenthesis,
                gameObjectNamingDigits: 1,
                assetNamingUsesSpace: true);

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Changes, Has.Some.Property("Key").EqualTo(ProjectSetupSettingKey.GameObjectNamingScheme));
            Assert.That(plan.Changes, Has.Some.Property("Key").EqualTo(ProjectSetupSettingKey.GameObjectNamingDigits));
            Assert.That(plan.Changes, Has.Some.Property("Key").EqualTo(ProjectSetupSettingKey.AssetNamingUsesSpace));
        }

        [TestCase(0)]
        [TestCase(10)]
        public void Build_RejectsUnsupportedGameObjectNamingDigits(int digits)
        {
            _profile.ConfigureNamingDefaults = true;
            _profile.GameObjectNamingDigits = digits;
            var current = new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0",
                hasNamingData: true);

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Naming Digits"));
        }

        [Test]
        public void Build_ProjectFoldersPlansMissingParentsInDepthOrder()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureProjectFolders = true;
            _profile.ProjectFolders = new[] { "Assets/Game/Data" };
            var current = Snapshot().WithProjectFolderState(new[] { "Assets" }, new[] { "Assets" });

            var plan = ProjectSetupPlanner.Build(_profile, current);
            var missing = ProjectSetupPlanner.GetMissingProjectFolders(_profile, current);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Changes, Has.Exactly(1).Property("Key").EqualTo(ProjectSetupSettingKey.ProjectFolders));
            Assert.That(missing, Is.EqualTo(new[] { "Assets/Game", "Assets/Game/Data" }));
        }

        [TestCase("Assets/../Secrets")]
        [TestCase("Packages/Generated")]
        [TestCase("Assets/CON")]
        public void Build_ProjectFoldersRejectsUnsafePath(string path)
        {
            _profile.ConfigureProjectFolders = true;
            _profile.ProjectFolders = new[] { path };

            var plan = ProjectSetupPlanner.Build(_profile, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Project Folders"));
        }

        [Test]
        public void Build_ProjectFoldersRejectsNormalizedDuplicate()
        {
            _profile.ConfigureProjectFolders = true;
            _profile.ProjectFolders = new[] { "Assets/Game/Data", "Assets\\Game\\Data" };

            var plan = ProjectSetupPlanner.Build(_profile, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("duplicate"));
        }

        [Test]
        public void Build_ProjectFoldersRejectsAssetCollisionInParentPath()
        {
            _profile.ConfigureProjectFolders = true;
            _profile.ProjectFolders = new[] { "Assets/Game/Data" };
            var current = Snapshot().WithProjectFolderState(
                new[] { "Assets" },
                new[] { "Assets", "Assets/Game" });

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Asset already uses"));
        }

        [Test]
        public void Build_AssemblyDefinitionsPlansFilesAndRequiredFolders()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureAssemblyDefinitions = true;
            _profile.AssemblyName = "Studio.Game";
            _profile.RuntimeAssemblyFolder = "Assets/Game/Scripts";
            _profile.EditorAssemblyFolder = "Assets/Game/Scripts/Editor";
            var current = Snapshot().WithProjectFolderState(new[] { "Assets" }, new[] { "Assets" });

            var plan = ProjectSetupPlanner.Build(_profile, current);
            var folders = ProjectSetupPlanner.GetMissingProjectFolders(_profile, current);
            var definitions = ProjectSetupPlanner.GetMissingAssemblyDefinitions(_profile, current);

            Assert.That(plan.IsValid, Is.True, string.Join("\n", plan.Errors));
            Assert.That(plan.Changes, Has.Exactly(1).Property("Key").EqualTo(ProjectSetupSettingKey.AssemblyDefinitions));
            Assert.That(folders, Is.EqualTo(new[]
            {
                "Assets/Game",
                "Assets/Game/Scripts",
                "Assets/Game/Scripts/Editor"
            }));
            Assert.That(definitions.Select(definition => definition.Path), Is.EqualTo(new[]
            {
                "Assets/Game/Scripts/Studio.Game.asmdef",
                "Assets/Game/Scripts/Editor/Studio.Game.Editor.asmdef"
            }));
        }

        [Test]
        public void Build_AssemblyDefinitionsIncludesTestAssemblyFoldersAndFiles()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureAssemblyDefinitions = true;
            _profile.IncludeTestAssemblies = true;
            _profile.TestAssemblyRootFolder = "Assets/Tests";
            var current = Snapshot().WithProjectFolderState(new[] { "Assets" }, new[] { "Assets" });

            var plan = ProjectSetupPlanner.Build(_profile, current);
            var folders = ProjectSetupPlanner.GetMissingProjectFolders(_profile, current);
            var definitions = ProjectSetupPlanner.GetMissingAssemblyDefinitions(_profile, current);

            Assert.That(plan.IsValid, Is.True, string.Join("\n", plan.Errors));
            Assert.That(folders, Does.Contain("Assets/Tests/EditMode").And.Contain("Assets/Tests/PlayMode"));
            Assert.That(definitions.Select(definition => definition.Path), Does.Contain("Assets/Tests/EditMode/Game.Tests.asmdef"));
            Assert.That(definitions.Select(definition => definition.Path), Does.Contain("Assets/Tests/PlayMode/Game.PlayMode.Tests.asmdef"));
            Assert.That(plan.Changes.Single(change => change.Key == ProjectSetupSettingKey.AssemblyDefinitions).DesiredValue, Does.Contain("EditMode").And.Contain("PlayMode"));
        }

        [TestCase("Game-Invalid")]
        [TestCase("class")]
        [TestCase("Game..Runtime")]
        [TestCase("Game\u00E9")]
        public void Build_AssemblyDefinitionsRejectsInvalidAssemblyName(string assemblyName)
        {
            _profile.ConfigureAssemblyDefinitions = true;
            _profile.AssemblyName = assemblyName;

            var plan = ProjectSetupPlanner.Build(_profile, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Assembly Name"));
        }

        [Test]
        public void Build_AssemblyDefinitionsRejectsEditorFolderOutsideRuntimeFolder()
        {
            _profile.ConfigureAssemblyDefinitions = true;
            _profile.RuntimeAssemblyFolder = "Assets/Runtime";
            _profile.EditorAssemblyFolder = "Assets/Editor";

            var plan = ProjectSetupPlanner.Build(_profile, Snapshot());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("child"));
        }

        [Test]
        public void Build_AssemblyDefinitionsPreservesExistingTargets()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureAssemblyDefinitions = true;
            var current = Snapshot().WithProjectFolderState(
                new[] { "Assets", "Assets/Scripts", "Assets/Scripts/Editor" },
                new[]
                {
                    "Assets",
                    "Assets/Scripts",
                    "Assets/Scripts/Game.asmdef",
                    "Assets/Scripts/Editor",
                    "Assets/Scripts/Editor/Game.Editor.asmdef"
                });

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.True, string.Join("\n", plan.Errors));
            Assert.That(plan.HasChanges, Is.False);
        }

        [Test]
        public void Build_VersionControlFilesPlansOnlyMissingRootFile()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureVersionControlFiles = true;
            var current = Snapshot().WithProjectRootFileState(new[] { ".gitignore" });

            var plan = ProjectSetupPlanner.Build(_profile, current);
            var files = ProjectSetupPlanner.GetMissingVersionControlFiles(_profile, current);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Changes, Has.Exactly(1).Property("Key").EqualTo(ProjectSetupSettingKey.VersionControlFiles));
            Assert.That(files.Select(file => file.Path), Is.EqualTo(new[] { ".gitattributes" }));
        }

        [Test]
        public void Build_AssemblyDefinitionsRejectsDifferentDefinitionInTargetFolder()
        {
            _profile.ConfigureAssemblyDefinitions = true;
            var current = Snapshot().WithProjectFolderState(
                new[] { "Assets", "Assets/Scripts", "Assets/Scripts/Editor" },
                new[] { "Assets", "Assets/Scripts", "Assets/Scripts/Existing.asmdef" });

            var plan = ProjectSetupPlanner.Build(_profile, current);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("different Assembly Definition"));
        }

        [Test]
        public void FolderRestore_RemovesOnlyCreatedEmptyFolderTree()
        {
            var removable = ProjectSetupFolderUtility.GetRestorableFolders(
                new[] { "Assets/Generated", "Assets/Generated/Empty", "Assets/Generated/Used" },
                new[] { "Assets", "Assets/Generated", "Assets/Generated/Empty", "Assets/Generated/Used" },
                new[]
                {
                    "Assets",
                    "Assets/Generated",
                    "Assets/Generated/Empty",
                    "Assets/Generated/Used",
                    "Assets/Generated/Used/Keep.asset"
                });

            Assert.That(removable, Is.EqualTo(new[] { "Assets/Generated/Empty" }));
        }

        private static ProjectSetupSnapshot Snapshot(
            SerializationMode serializationMode = SerializationMode.ForceText,
            string versionControl = "Visible Meta Files",
            ColorSpace colorSpace = ColorSpace.Gamma,
            bool runInBackground = false)
        {
            return new ProjectSetupSnapshot(
                serializationMode,
                versionControl,
                false,
                EnterPlayModeOptions.None,
                colorSpace,
                runInBackground,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0");
        }

        private static ProjectSetupSnapshot SnapshotWithTagManager(bool fillUserLayers = false)
        {
            var layers = new string[32];
            layers[8] = "ExistingLayer";
            if (fillUserLayers)
            {
                for (var index = 8; index < layers.Length; index++)
                {
                    layers[index] = $"Layer{index}";
                }
            }

            return new ProjectSetupSnapshot(
                SerializationMode.ForceText,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0",
                true,
                new[] { "Untagged", "ExistingTag" },
                new[] { "ExistingTag" },
                layers,
                new[]
                {
                    new ProjectSetupSortingLayer("Default", 0, false),
                    new ProjectSetupSortingLayer("ExistingSorting", 10, false)
                });
        }
    }
}
