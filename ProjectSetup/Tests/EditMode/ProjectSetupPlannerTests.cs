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
