// SPDX-License-Identifier: MIT

using System;
using System.IO;
using NUnit.Framework;
using ProjectSetup.Editor;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Tests
{
    internal sealed class ProjectSetupServiceTests
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
            UnityEngine.Object.DestroyImmediate(_profile);
        }

        [Test]
        public void Apply_NoDifferencesDoesNotWriteBackup()
        {
            var environment = new FakeEnvironment(Snapshot());
            var backup = new FakeBackupStore();
            var service = new ProjectSetupService(environment, backup);

            var result = service.Apply(_profile);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(environment.ApplyProfileCount, Is.Zero);
            Assert.That(backup.SaveCount, Is.Zero);
        }

        [Test]
        public void Apply_SavesBeforeApplyingAndVerifiesResult()
        {
            var before = Snapshot(SerializationMode.Mixed, "Hidden Meta Files");
            var environment = new FakeEnvironment(before);
            var backup = new FakeBackupStore();
            var service = new ProjectSetupService(environment, backup);

            var result = service.Apply(_profile);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(backup.SaveCount, Is.EqualTo(1));
            Assert.That(backup.Snapshot, Is.EqualTo(before));
            Assert.That(environment.ApplyProfileCount, Is.EqualTo(1));
            Assert.That(environment.State.AssetSerialization, Is.EqualTo(SerializationMode.ForceText));
            Assert.That(environment.State.VersionControlMode, Is.EqualTo("Visible Meta Files"));
        }

        [Test]
        public void Apply_WhenEnvironmentThrowsAttemptsRollback()
        {
            var before = Snapshot(SerializationMode.Mixed, "Hidden Meta Files");
            var environment = new FakeEnvironment(before) { ThrowOnProfileApply = true };
            var service = new ProjectSetupService(environment, new FakeBackupStore());

            var result = service.Apply(_profile);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(environment.ApplySnapshotCount, Is.EqualTo(1));
            Assert.That(environment.State, Is.EqualTo(before));
        }

        [Test]
        public void Apply_WhenBackupFailsDoesNotWriteProjectSettings()
        {
            var before = Snapshot(SerializationMode.Mixed, "Hidden Meta Files");
            var environment = new FakeEnvironment(before);
            var backup = new FakeBackupStore { ThrowOnSave = true };
            var service = new ProjectSetupService(environment, backup);

            var result = service.Apply(_profile);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(environment.ApplyProfileCount, Is.Zero);
            Assert.That(environment.ApplySnapshotCount, Is.Zero);
            Assert.That(environment.State, Is.EqualTo(before));
        }

        [Test]
        public void RestoreLast_AppliesSavedSnapshot()
        {
            var desired = Snapshot(SerializationMode.Mixed, "Hidden Meta Files");
            var environment = new FakeEnvironment(Snapshot());
            var backup = new FakeBackupStore { Snapshot = desired, HasSnapshot = true };
            var service = new ProjectSetupService(environment, backup);

            var result = service.RestoreLast();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(environment.State, Is.EqualTo(desired));
        }

        [Test]
        public void RestoreLast_WhenVerificationFailsRestoresPreRestoreState()
        {
            var before = Snapshot();
            var desired = Snapshot(SerializationMode.Mixed, "Hidden Meta Files");
            var environment = new FakeEnvironment(before) { IgnoreFirstSnapshotApply = true };
            var backup = new FakeBackupStore { Snapshot = desired, HasSnapshot = true };
            var service = new ProjectSetupService(environment, backup);

            var result = service.RestoreLast();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(environment.ApplySnapshotCount, Is.EqualTo(2));
            Assert.That(environment.State, Is.EqualTo(before));
        }

        [Test]
        public void Preview_WhenUnavailableReturnsErrorWithoutCapture()
        {
            var environment = new FakeEnvironment(Snapshot()) { IsAvailable = false };
            var service = new ProjectSetupService(environment, new FakeBackupStore());

            var plan = service.Preview(_profile);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(environment.CaptureCount, Is.Zero);
        }

        [Test]
        public void PreviewRestore_WhenBackupContainsUnsupportedValueDoesNotApply()
        {
            var invalid = new ProjectSetupSnapshot(
                (SerializationMode)999,
                "Visible Meta Files",
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "Company",
                "Product",
                "1.0.0");
            var environment = new FakeEnvironment(Snapshot());
            var backup = new FakeBackupStore { Snapshot = invalid, HasSnapshot = true };
            var service = new ProjectSetupService(environment, backup);

            var plan = service.PreviewRestore(out _, out _);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(environment.ApplySnapshotCount, Is.Zero);
        }

        [Test]
        public void RestoreLast_RestoresTagManagerCollectionsExactly()
        {
            var before = SnapshotWithTagManager("CurrentTag", "CurrentLayer", "CurrentSorting", 20);
            var desired = SnapshotWithTagManager("BackupTag", "BackupLayer", "BackupSorting", 30);
            var environment = new FakeEnvironment(before);
            var backup = new FakeBackupStore { Snapshot = desired, HasSnapshot = true };
            var service = new ProjectSetupService(environment, backup);

            var preview = service.PreviewRestore(out _, out var error);
            var result = service.RestoreLast();

            Assert.That(preview.IsValid, Is.True, error);
            Assert.That(preview.Changes, Has.Some.Property("Key").EqualTo(ProjectSetupSettingKey.Tags));
            Assert.That(preview.Changes, Has.Some.Property("Key").EqualTo(ProjectSetupSettingKey.Layers));
            Assert.That(preview.Changes, Has.Some.Property("Key").EqualTo(ProjectSetupSettingKey.SortingLayers));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(environment.State, Is.EqualTo(desired));
        }

        [Test]
        public void PreviewRestore_WhenBuildSceneTargetChangedReturnsError()
        {
            var current = SnapshotWithBuildScenes("profile:new", "Assets/New.unity");
            var desired = SnapshotWithBuildScenes("profile:backup", "Assets/Backup.unity");
            var environment = new FakeEnvironment(current);
            var backup = new FakeBackupStore { Snapshot = desired, HasSnapshot = true };
            var service = new ProjectSetupService(environment, backup);

            var plan = service.PreviewRestore(out _, out _);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Build Scene target"));
            Assert.That(environment.ApplySnapshotCount, Is.Zero);
        }

        [Test]
        public void PreviewRestore_WhenScriptingDefineTargetChangedReturnsError()
        {
            var current = SnapshotWithScriptingDefines("Android", "CURRENT_SYMBOL");
            var desired = SnapshotWithScriptingDefines("Standalone", "BACKUP_SYMBOL");
            var environment = new FakeEnvironment(current);
            var backup = new FakeBackupStore { Snapshot = desired, HasSnapshot = true };
            var service = new ProjectSetupService(environment, backup);

            var plan = service.PreviewRestore(out _, out _);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("scripting define target"));
            Assert.That(environment.ApplySnapshotCount, Is.Zero);
        }

        [Test]
        public void PreviewRestore_WhenScriptingDefinesDifferShowsExactBackupList()
        {
            var current = SnapshotWithScriptingDefines("Standalone", "CURRENT_SYMBOL", "AFTER_APPLY_SYMBOL");
            var desired = SnapshotWithScriptingDefines("Standalone", "CURRENT_SYMBOL", "BACKUP_SYMBOL");
            var environment = new FakeEnvironment(current);
            var backup = new FakeBackupStore { Snapshot = desired, HasSnapshot = true };
            var service = new ProjectSetupService(environment, backup);

            var plan = service.PreviewRestore(out _, out var error);

            Assert.That(plan.IsValid, Is.True, error);
            Assert.That(plan.Changes, Has.Exactly(1).Property("Key").EqualTo(ProjectSetupSettingKey.ScriptingDefineSymbols));
            Assert.That(plan.Changes[0].CurrentValue, Is.EqualTo("CURRENT_SYMBOL;AFTER_APPLY_SYMBOL"));
            Assert.That(plan.Changes[0].DesiredValue, Is.EqualTo("CURRENT_SYMBOL;BACKUP_SYMBOL"));
        }

        private static ProjectSetupSnapshot Snapshot(SerializationMode serializationMode = SerializationMode.ForceText, string versionControl = "Visible Meta Files")
        {
            return new ProjectSetupSnapshot(
                serializationMode,
                versionControl,
                false,
                EnterPlayModeOptions.None,
                ColorSpace.Gamma,
                false,
                "DefaultCompany",
                "New Unity Project",
                "1.0.0");
        }

        private static ProjectSetupSnapshot SnapshotWithTagManager(string tag, string layer, string sortingLayer, int sortingLayerId)
        {
            var layers = new string[32];
            layers[8] = layer;
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
                new[] { "Untagged", tag },
                new[] { tag },
                layers,
                new[]
                {
                    new ProjectSetupSortingLayer("Default", 0, false),
                    new ProjectSetupSortingLayer(sortingLayer, sortingLayerId, false)
                });
        }

        private static ProjectSetupSnapshot SnapshotWithBuildScenes(string targetId, string path)
        {
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
                false,
                null,
                null,
                null,
                null,
                null,
                true,
                targetId,
                targetId,
                new[] { new ProjectSetupBuildSceneState(string.Empty, path, true) });
        }

        private static ProjectSetupSnapshot SnapshotWithScriptingDefines(string targetId, params string[] symbols)
        {
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
                hasScriptingDefineData: true,
                scriptingDefineTargetId: targetId,
                scriptingDefineTargetLabel: targetId,
                scriptingDefineSymbols: symbols);
        }

        private sealed class FakeEnvironment : IProjectSetupEnvironment
        {
            internal FakeEnvironment(ProjectSetupSnapshot state)
            {
                State = state;
            }

            public bool IsAvailable { get; set; } = true;
            internal ProjectSetupSnapshot State { get; private set; }
            internal int CaptureCount { get; private set; }
            internal int ApplyProfileCount { get; private set; }
            internal int ApplySnapshotCount { get; private set; }
            internal bool ThrowOnProfileApply { get; set; }
            internal bool IgnoreFirstSnapshotApply { get; set; }

            public ProjectSetupSnapshot Capture()
            {
                CaptureCount++;
                return State;
            }

            public void Apply(ProjectSetupProfile profile)
            {
                ApplyProfileCount++;
                if (ThrowOnProfileApply)
                {
                    throw new InvalidOperationException("Expected failure");
                }

                State = new ProjectSetupSnapshot(
                    profile.ConfigureAssetSerialization ? profile.AssetSerialization : State.AssetSerialization,
                    profile.ConfigureVersionControl ? profile.VersionControlMode : State.VersionControlMode,
                    profile.ConfigureEnterPlayMode ? profile.EnterPlayModeOptionsEnabled : State.EnterPlayModeOptionsEnabled,
                    profile.ConfigureEnterPlayMode ? profile.EnterPlayModeOptions : State.EnterPlayModeOptions,
                    profile.ConfigureColorSpace ? profile.ColorSpace : State.ColorSpace,
                    profile.ConfigureRunInBackground ? profile.RunInBackground : State.RunInBackground,
                    profile.ConfigureCompanyName ? profile.CompanyName : State.CompanyName,
                    profile.ConfigureProductName ? profile.ProductName : State.ProductName,
                    profile.ConfigureBundleVersion ? profile.BundleVersion : State.BundleVersion);
            }

            public void Apply(ProjectSetupSnapshot snapshot)
            {
                ApplySnapshotCount++;
                if (IgnoreFirstSnapshotApply && ApplySnapshotCount == 1)
                {
                    return;
                }

                State = snapshot;
            }
        }

        private sealed class FakeBackupStore : IProjectSetupBackupStore
        {
            public bool Exists => HasSnapshot;
            internal bool HasSnapshot { get; set; }
            internal int SaveCount { get; private set; }
            internal ProjectSetupSnapshot Snapshot { get; set; }
            internal bool ThrowOnSave { get; set; }

            public void Save(ProjectSetupSnapshot snapshot)
            {
                SaveCount++;
                if (ThrowOnSave)
                {
                    throw new IOException("Expected backup failure");
                }

                Snapshot = snapshot;
                HasSnapshot = true;
            }

            public bool TryLoad(out ProjectSetupSnapshot snapshot, out string error)
            {
                snapshot = Snapshot;
                error = HasSnapshot ? string.Empty : "No backup";
                return HasSnapshot;
            }
        }
    }
}
