// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
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
        public void Apply_WhenEnvironmentThrowsDoesNotClaimUnconfirmedRootFiles()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureVersionControlFiles = true;
            var before = Snapshot().WithProjectRootFileState(Array.Empty<string>());
            var environment = new FakeEnvironment(before) { ThrowOnProfileApply = true };
            var service = new ProjectSetupService(environment, new FakeBackupStore());

            var result = service.Apply(_profile);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(environment.LastAppliedSnapshot.CreatedProjectRootFiles, Is.Empty);
            Assert.That(environment.State.ProjectRootFilePaths, Is.Empty);
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
        public void Apply_ProjectFoldersRecordsOnlyMissingFoldersInBackup()
        {
            _profile.ConfigureProjectFolders = true;
            _profile.ProjectFolders = new[] { "Assets/Game/Data" };
            var before = Snapshot().WithProjectFolderState(new[] { "Assets" }, new[] { "Assets" });
            var environment = new FakeEnvironment(before);
            var backup = new FakeBackupStore();
            var service = new ProjectSetupService(environment, backup);

            var result = service.Apply(_profile);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(backup.Snapshot.CreatedProjectFolders, Is.EqualTo(new[] { "Assets/Game", "Assets/Game/Data" }));
            Assert.That(environment.State.ProjectFolders, Does.Contain("Assets/Game").And.Contain("Assets/Game/Data"));
        }

        [Test]
        public void Apply_AssemblyDefinitionsRecordsOnlyCreatedFilesInBackup()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureAssemblyDefinitions = true;
            var before = Snapshot().WithProjectFolderState(new[] { "Assets" }, new[] { "Assets" });
            var environment = new FakeEnvironment(before);
            var backup = new FakeBackupStore();
            var service = new ProjectSetupService(environment, backup);

            var result = service.Apply(_profile);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(backup.Snapshot.CreatedProjectFolders, Is.EqualTo(new[] { "Assets/Scripts", "Assets/Scripts/Editor" }));
            Assert.That(backup.Snapshot.CreatedProjectAssets.Select(asset => asset.Path), Is.EqualTo(new[]
            {
                "Assets/Scripts/Game.asmdef",
                "Assets/Scripts/Editor/Game.Editor.asmdef"
            }));
            Assert.That(environment.State.ProjectAssetPaths, Does.Contain("Assets/Scripts/Game.asmdef"));
            Assert.That(environment.State.ProjectAssetPaths, Does.Contain("Assets/Scripts/Editor/Game.Editor.asmdef"));
        }

        [Test]
        public void Apply_VersionControlFilesRecordsOnlyCreatedRootFilesInBackup()
        {
            _profile.ConfigureAssetSerialization = false;
            _profile.ConfigureVersionControl = false;
            _profile.ConfigureVersionControlFiles = true;
            var before = Snapshot().WithProjectRootFileState(new[] { ".gitignore" });
            var environment = new FakeEnvironment(before);
            var backup = new FakeBackupStore();
            var service = new ProjectSetupService(environment, backup);

            var result = service.Apply(_profile);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(
                backup.Snapshot.CreatedProjectRootFiles.Select(file => file.Path),
                Is.EqualTo(new[] { ".gitattributes" }));
            Assert.That(environment.State.ProjectRootFilePaths, Is.EqualTo(new[] { ".gitignore", ".gitattributes" }));
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

        [Test]
        public void PreviewRestore_WhenApplicationIdentifierTargetChangedReturnsError()
        {
            var current = SnapshotWithApplicationIdentifier("Android", "com.studiogaku.current");
            var desired = SnapshotWithApplicationIdentifier("Standalone", "com.studiogaku.backup");
            var environment = new FakeEnvironment(current);
            var backup = new FakeBackupStore { Snapshot = desired, HasSnapshot = true };
            var service = new ProjectSetupService(environment, backup);

            var plan = service.PreviewRestore(out _, out _);
            var result = service.RestoreLast();

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Application Identifier target"));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("Application Identifier target"));
            Assert.That(environment.ApplySnapshotCount, Is.Zero);
        }

        [Test]
        public void PreviewRestore_WhenApplicationIdentifierDiffersShowsExactBackupValue()
        {
            var current = SnapshotWithApplicationIdentifier("Standalone", "com.studiogaku.current");
            var desired = SnapshotWithApplicationIdentifier("Standalone", "com.studiogaku.backup");
            var environment = new FakeEnvironment(current);
            var backup = new FakeBackupStore { Snapshot = desired, HasSnapshot = true };
            var service = new ProjectSetupService(environment, backup);

            var plan = service.PreviewRestore(out _, out var error);

            Assert.That(plan.IsValid, Is.True, error);
            Assert.That(plan.Changes, Has.Exactly(1).Property("Key").EqualTo(ProjectSetupSettingKey.ApplicationIdentifier));
            Assert.That(plan.Changes[0].CurrentValue, Is.EqualTo("com.studiogaku.current"));
            Assert.That(plan.Changes[0].DesiredValue, Is.EqualTo("com.studiogaku.backup"));
        }

        [Test]
        public void PreviewRestore_WhenScriptingBackendTargetChangedReturnsError()
        {
            var current = SnapshotWithScriptingBackend("Android", ScriptingImplementation.IL2CPP);
            var desired = SnapshotWithScriptingBackend("Standalone", ScriptingImplementation.Mono2x);
            var environment = new FakeEnvironment(current);
            var backup = new FakeBackupStore { Snapshot = desired, HasSnapshot = true };
            var service = new ProjectSetupService(environment, backup);

            var plan = service.PreviewRestore(out _, out _);
            var result = service.RestoreLast();

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("Scripting Backend target"));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("Scripting Backend target"));
            Assert.That(environment.ApplySnapshotCount, Is.Zero);
        }

        [Test]
        public void PreviewRestore_WhenScriptingBackendDiffersShowsExactBackupValue()
        {
            var current = SnapshotWithScriptingBackend("Standalone", ScriptingImplementation.IL2CPP);
            var desired = SnapshotWithScriptingBackend("Standalone", ScriptingImplementation.Mono2x);
            var environment = new FakeEnvironment(current);
            var backup = new FakeBackupStore { Snapshot = desired, HasSnapshot = true };
            var service = new ProjectSetupService(environment, backup);

            var plan = service.PreviewRestore(out _, out var error);

            Assert.That(plan.IsValid, Is.True, error);
            Assert.That(plan.Changes, Has.Exactly(1).Property("Key").EqualTo(ProjectSetupSettingKey.ScriptingBackend));
            Assert.That(plan.Changes[0].CurrentValue, Is.EqualTo("IL2CPP"));
            Assert.That(plan.Changes[0].DesiredValue, Is.EqualTo("Mono2x"));
        }

        [Test]
        public void PreviewRestore_WhenApiCompatibilityTargetChangedReturnsError()
        {
            var current = SnapshotWithApiCompatibilityLevel("Android", ApiCompatibilityLevel.NET_Standard);
            var desired = SnapshotWithApiCompatibilityLevel("Standalone", ApiCompatibilityLevel.NET_Unity_4_8);
            var environment = new FakeEnvironment(current);
            var backup = new FakeBackupStore { Snapshot = desired, HasSnapshot = true };
            var service = new ProjectSetupService(environment, backup);

            var plan = service.PreviewRestore(out _, out _);
            var result = service.RestoreLast();

            Assert.That(plan.IsValid, Is.False);
            Assert.That(plan.Errors, Has.Some.Contains("API Compatibility Level target"));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(environment.ApplySnapshotCount, Is.Zero);
        }

        [Test]
        public void PreviewRestore_WhenApiCompatibilityLevelDiffersShowsExactBackupValue()
        {
            var current = SnapshotWithApiCompatibilityLevel("Standalone", ApiCompatibilityLevel.NET_Standard);
            var desired = SnapshotWithApiCompatibilityLevel("Standalone", ApiCompatibilityLevel.NET_Unity_4_8);
            var environment = new FakeEnvironment(current);
            var backup = new FakeBackupStore { Snapshot = desired, HasSnapshot = true };
            var service = new ProjectSetupService(environment, backup);

            var plan = service.PreviewRestore(out _, out var error);

            Assert.That(plan.IsValid, Is.True, error);
            Assert.That(plan.Changes, Has.Exactly(1).Property("Key").EqualTo(ProjectSetupSettingKey.ApiCompatibilityLevel));
            Assert.That(plan.Changes[0].CurrentValue, Is.EqualTo(".NET Standard"));
            Assert.That(plan.Changes[0].DesiredValue, Is.EqualTo(".NET Framework"));
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

        private static ProjectSetupSnapshot SnapshotWithApplicationIdentifier(string targetId, string applicationIdentifier)
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
                hasApplicationIdentifierData: true,
                applicationIdentifierTargetId: targetId,
                applicationIdentifierTargetLabel: targetId,
                applicationIdentifier: applicationIdentifier);
        }

        private static ProjectSetupSnapshot SnapshotWithScriptingBackend(string targetId, ScriptingImplementation scriptingBackend)
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
                hasScriptingBackendData: true,
                scriptingBackendTargetId: targetId,
                scriptingBackendTargetLabel: targetId,
                scriptingBackend: scriptingBackend);
        }

        private static ProjectSetupSnapshot SnapshotWithApiCompatibilityLevel(string targetId, ApiCompatibilityLevel apiCompatibilityLevel)
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
                hasApiCompatibilityLevelData: true,
                apiCompatibilityLevelTargetId: targetId,
                apiCompatibilityLevelTargetLabel: targetId,
                apiCompatibilityLevel: apiCompatibilityLevel);
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
            internal ProjectSetupSnapshot LastAppliedSnapshot { get; private set; }
            internal bool ThrowOnProfileApply { get; set; }
            internal bool IgnoreFirstSnapshotApply { get; set; }

            public ProjectSetupSnapshot Capture()
            {
                CaptureCount++;
                return State;
            }

            public ProjectSetupEnvironmentApplyResult Apply(ProjectSetupProfile profile)
            {
                ApplyProfileCount++;
                if (ThrowOnProfileApply)
                {
                    throw new InvalidOperationException("Expected failure");
                }

                var updated = new ProjectSetupSnapshot(
                    profile.ConfigureAssetSerialization ? profile.AssetSerialization : State.AssetSerialization,
                    profile.ConfigureVersionControl ? profile.VersionControlMode : State.VersionControlMode,
                    profile.ConfigureEnterPlayMode ? profile.EnterPlayModeOptionsEnabled : State.EnterPlayModeOptionsEnabled,
                    profile.ConfigureEnterPlayMode ? profile.EnterPlayModeOptions : State.EnterPlayModeOptions,
                    profile.ConfigureColorSpace ? profile.ColorSpace : State.ColorSpace,
                    profile.ConfigureRunInBackground ? profile.RunInBackground : State.RunInBackground,
                    profile.ConfigureCompanyName ? profile.CompanyName : State.CompanyName,
                    profile.ConfigureProductName ? profile.ProductName : State.ProductName,
                    profile.ConfigureBundleVersion ? profile.BundleVersion : State.BundleVersion);
                var folders = State.ProjectFolders;
                var assets = State.ProjectAssetPaths;
                var missingDefinitions = ProjectSetupPlanner.GetMissingAssemblyDefinitions(profile, State);
                var missing = Array.Empty<string>();
                if (profile.ConfigureProjectFolders || profile.ConfigureAssemblyDefinitions)
                {
                    missing = ProjectSetupPlanner.GetMissingProjectFolders(profile, State);
                    folders = folders.Concat(missing).ToArray();
                    assets = assets.Concat(missing).ToArray();
                }

                var createdAssets = missingDefinitions.Select(definition => definition.ToCreatedAsset()).ToArray();
                assets = assets.Concat(createdAssets.Select(asset => asset.Path)).ToArray();
                var missingRootFiles = ProjectSetupPlanner.GetMissingVersionControlFiles(profile, State);
                var createdRootFiles = missingRootFiles.Select(file => file.ToCreatedRootFile()).ToArray();
                var rootFilePaths = State.ProjectRootFilePaths
                    .Concat(createdRootFiles.Select(file => file.Path))
                    .ToArray();

                State = updated
                    .WithProjectFolderState(folders, assets)
                    .WithProjectRootFileState(rootFilePaths);
                return new ProjectSetupEnvironmentApplyResult(missing, createdAssets, createdRootFiles);
            }

            public void Apply(ProjectSetupSnapshot snapshot)
            {
                ApplySnapshotCount++;
                LastAppliedSnapshot = snapshot;
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
