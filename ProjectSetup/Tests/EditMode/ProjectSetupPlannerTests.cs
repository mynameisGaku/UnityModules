// SPDX-License-Identifier: MIT

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
    }
}
