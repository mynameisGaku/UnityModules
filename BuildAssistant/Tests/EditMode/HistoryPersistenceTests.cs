using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using BuildAssistant.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace BuildAssistant.Tests
{
    public sealed class HistoryPersistenceTests
    {
        [Test]
        public void Load_UsesValidBackupWhenPrimaryHasMalformedNestedData()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry("backup-entry") });
            store.Save(new[] { BuildAssistantTestData.Entry("primary-entry") });
            fake.SetFile(HistoryPath, "{\"schemaVersion\":1,\"entries\":[null]}");

            var history = store.Load();

            Assert.That(history.RecoveredFromBackup, Is.True);
            Assert.That(history.Entries.Select(entry => entry.RunId), Is.EqualTo(new[] { "backup-entry" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Load_UsesValidBackupWhenPrimaryExistenceProbeIsRefused(bool useUnsupportedOperation)
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry("known-good-backup") });
            store.Save(new[] { BuildAssistantTestData.Entry("unreadable-primary") });
            fake.FileExistsExceptionPath = HistoryPath;
            fake.FileExistsException = useUnsupportedOperation ? new NotSupportedException("Injected unsupported probe.") : new SecurityException("Injected access denial.");

            var history = store.Load();

            Assert.That(history.RecoveredFromBackup, Is.True);
            Assert.That(history.Entries.Single().RunId, Is.EqualTo("known-good-backup"));
        }

        [Test]
        public void Load_UsesValidBackupWhenPrimaryBoundedReadFails()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry("known-good-backup") });
            store.Save(new[] { BuildAssistantTestData.Entry("unreadable-primary") });
            fake.ReadAllTextBoundedExceptionPath = HistoryPath;
            fake.ReadAllTextBoundedException = new IOException("Injected stream read failure.");

            var history = store.Load();

            Assert.That(history.RecoveredFromBackup, Is.True);
            Assert.That(history.Entries.Single().RunId, Is.EqualTo("known-good-backup"));
        }

        [Test]
        public void FileExistenceProbeFailure_IsContainedAsABoundedHistoryFailure()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            fake.FileExistsException = new SecurityException("Injected access denial.");

            var history = store.Load();
            var recovery = BuildAssistantService.RecoverPreviousRunState(store, DateTime.UtcNow, "probe-failure");

            Assert.That(history.Entries, Is.Empty);
            Assert.That(history.Message, Does.Contain("有効な履歴ファイルを読み込めませんでした"));
            Assert.That(recovery.Error, Is.EqualTo(BuildAssistantError.HistoryWriteFailed));
        }

        [Test]
        public void Load_UsesValidBackupWithoutParsingAnOversizedPrimaryDocument()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry("backup-entry") });
            store.Save(new[] { BuildAssistantTestData.Entry("primary-entry") });
            fake.SetFile(HistoryPath, new string('x', HistoryStore.MaximumDocumentBytes + 1));

            var history = store.Load();

            Assert.That(history.RecoveredFromBackup, Is.True);
            Assert.That(history.Entries.Select(entry => entry.RunId), Is.EqualTo(new[] { "backup-entry" }));
        }

        [Test]
        public void NestedHistoryLimits_RejectOnlyCountsBeyondThePublishedBounds()
        {
            Assert.That(HistoryStore.AreNestedCountsSupported(HistoryStore.MaximumDefineCount, HistoryStore.MaximumSceneCount, HistoryStore.MaximumAssetCount, HistoryStore.MaximumTypeCount), Is.True);
            Assert.That(HistoryStore.AreNestedCountsSupported(HistoryStore.MaximumDefineCount + 1, 0, 0, 0), Is.False);
            Assert.That(HistoryStore.AreNestedCountsSupported(0, HistoryStore.MaximumSceneCount + 1, 0, 0), Is.False);
            Assert.That(HistoryStore.AreNestedCountsSupported(0, 0, HistoryStore.MaximumAssetCount + 1, 0), Is.False);
            Assert.That(HistoryStore.AreNestedCountsSupported(0, 0, 0, HistoryStore.MaximumTypeCount + 1), Is.False);
        }

        [Test]
        public void JsonStructure_StopsOversizedArraysDuringParsingWithoutRejectingTwoMaximumAssetArrays()
        {
            var maximumAssets = string.Join(",", Enumerable.Repeat("{}", HistoryStore.MaximumAssetCount));
            var twoMaximumArrays = "[{\"assets\":[" + maximumAssets + "]},{\"assets\":[" + maximumAssets + "]}]";
            var oneOversizedArray = "{\"assets\":[" + string.Join(",", Enumerable.Repeat("null", HistoryStore.MaximumAssetCount + 1)) + "]}";

            Assert.That(JsonDocumentShape.TryParse(twoMaximumArrays, out _), Is.True);
            Assert.That(JsonDocumentShape.TryParse(oneOversizedArray, out _), Is.False);
        }

        [Test]
        public void SaveAfterBackupRecovery_PreservesTheKnownGoodBackupForASecondRecovery()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry("known-good-backup") });
            store.Save(new[] { BuildAssistantTestData.Entry("first-primary") });
            fake.SetFile(HistoryPath, "{\"schemaVersion\":1,\"entries\":[null]}");
            Assert.That(store.Load().RecoveredFromBackup, Is.True);

            store.Save(new[] { BuildAssistantTestData.Entry("new-primary") });
            fake.SetFile(HistoryPath, "{\"schemaVersion\":1,\"entries\":[null]}");
            var recoveredAgain = store.Load();

            Assert.That(recoveredAgain.RecoveredFromBackup, Is.True);
            Assert.That(recoveredAgain.Entries.Select(entry => entry.RunId), Is.EqualTo(new[] { "known-good-backup" }));
            Assert.That(fake.TemporaryFileCount, Is.Zero);
        }

        [Test]
        public void Save_LeavesOriginalHistoryAndRemovesTemporaryFileWhenAtomicReplaceFails()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry("original") });
            var originalJson = fake.GetFile(HistoryPath);
            fake.ThrowOnReplace = true;

            Assert.Throws<IOException>(() => store.Save(new[] { BuildAssistantTestData.Entry("replacement") }));

            Assert.That(fake.GetFile(HistoryPath), Is.EqualTo(originalJson));
            Assert.That(fake.TemporaryFileCount, Is.Zero);
        }

        [Test]
        public void Save_RefusesToReplaceAnUnreadableHistoryPair()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var backupPath = HistoryPath + ".bak";
            fake.SetFile(HistoryPath, "broken primary");
            fake.SetFile(backupPath, "broken backup");

            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry() }));

            Assert.That(fake.GetFile(HistoryPath), Is.EqualTo("broken primary"));
            Assert.That(fake.GetFile(backupPath), Is.EqualTo("broken backup"));
            Assert.That(fake.TemporaryFileCount, Is.Zero);
        }

        [Test]
        public void CorruptHistoryPair_IsQuarantinedWithoutDeletionAndStopsOnlyTheCurrentAttempt()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var backupPath = HistoryPath + ".bak";
            fake.SetFile(HistoryPath, "broken primary");
            fake.SetFile(backupPath, "broken backup");

            var failure = BuildAssistantService.RecoverInvalidHistory(store, "1234abcd");

            Assert.That(failure.Error, Is.EqualTo(BuildAssistantError.HistoryWriteFailed));
            Assert.That(failure.Message, Does.Contain("削除せず別名へ隔離"));
            Assert.That(fake.FileExists(HistoryPath), Is.False);
            Assert.That(fake.FileExists(backupPath), Is.False);
            Assert.That(fake.GetFile(HistoryPath + ".invalid-1234abcd"), Is.EqualTo("broken primary"));
            Assert.That(fake.GetFile(backupPath + ".invalid-1234abcd"), Is.EqualTo("broken backup"));
            Assert.That(BuildAssistantService.RecoverInvalidHistory(store, "next"), Is.Null);
        }

        [Test]
        public void HistoryQuarantine_RefusesToMoveAValidBackup()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry("valid-backup") });
            store.Save(new[] { BuildAssistantTestData.Entry("valid-primary") });
            fake.SetFile(HistoryPath, "broken primary");

            Assert.Throws<InvalidOperationException>(() => store.QuarantineInvalidHistory("1234abcd"));
            Assert.That(store.Load().RecoveredFromBackup, Is.True);
            Assert.That(store.Load().Entries[0].RunId, Is.EqualTo("valid-backup"));
        }

        [Test]
        public void RecoverInterrupted_RecordsOneInterruptedEntryWithoutRestartingTheBuild()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var started = new DateTime(2026, 8, 23, 1, 3, 0, DateTimeKind.Utc);
            var completed = started.AddMinutes(2);
            store.SaveRunState(RunState.CreateRunning(BuildAssistantTestData.Plan(), started));

            var history = store.RecoverInterrupted(completed);

            Assert.That(history.Entries.Count, Is.EqualTo(1));
            Assert.That(history.Entries[0].Status, Is.EqualTo(BuildAssistantHistoryStatus.Interrupted));
            Assert.That(history.Entries[0].CompletedAtUtc, Is.EqualTo(completed));
            Assert.That(history.Entries[0].Message, Does.Contain("自動では再実行していません"));
            Assert.That(store.HasRunState, Is.False);
        }

        [Test]
        public void TerminalTimes_ClampClockRollbackToTheRecordedStart()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var plan = BuildAssistantTestData.Plan();
            var started = new DateTime(2026, 8, 23, 1, 3, 0, DateTimeKind.Utc);
            store.SaveRunState(RunState.CreateRunning(plan, started));

            var recovered = store.RecoverInterrupted(started.AddMinutes(-5));
            var reduced = BuildReportReducer.CreateFailure(plan, started, started.AddMinutes(-5), BuildAssistantError.BuildInvocationFailed, "Expected failure.");

            Assert.That(recovered.Entries[0].CompletedAtUtc, Is.EqualTo(started));
            Assert.That(reduced.Entry.CompletedAtUtc, Is.EqualTo(started));
        }

        [Test]
        public void RunningState_ClampsAClockRollbackToThePlanCreationTime()
        {
            var created = new DateTime(2026, 8, 23, 1, 3, 0, DateTimeKind.Utc);
            var plan = BuildAssistantTestData.Plan(createdAtUtc: created);

            var state = RunState.CreateRunning(plan, created.AddMinutes(-5));

            Assert.That(state.Entry.CreatedAtUtc, Is.EqualTo(created));
            Assert.That(state.Entry.StartedAtUtc, Is.EqualTo(created));
            Assert.That(state.Entry.CompletedAtUtc, Is.EqualTo(created));
        }

        [Test]
        public void Load_NormalizesAOnePointZeroHistoryEntryWrittenDuringClockRollback()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var entry = BuildAssistantTestData.Entry();
            store.Save(new[] { entry });
            var legacyCreatedAtUtc = entry.StartedAtUtc.AddSeconds(30);
            var json = fake.GetFile(HistoryPath);
            var currentField = "\"createdAtUtc\": \"" + entry.CreatedAtUtc.ToString("O") + "\"";
            var legacyField = "\"createdAtUtc\": \"" + legacyCreatedAtUtc.ToString("O") + "\"";
            Assert.That(json, Does.Contain(currentField));
            fake.SetFile(HistoryPath, json.Replace(currentField, legacyField));

            var loaded = store.Load().Entries.Single();

            Assert.That(loaded.CreatedAtUtc, Is.EqualTo(legacyCreatedAtUtc));
            Assert.That(loaded.StartedAtUtc, Is.EqualTo(legacyCreatedAtUtc));
            Assert.That(loaded.CompletedAtUtc, Is.EqualTo(entry.CompletedAtUtc));
        }

        [Test]
        public void LoadRunState_NormalizesAOnePointZeroRunningEntryWrittenDuringClockRollback()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var plan = BuildAssistantTestData.Plan();
            var running = RunState.CreateRunning(plan, plan.CreatedAtUtc);
            store.SaveRunState(running);
            var legacyTime = plan.CreatedAtUtc.AddMinutes(-5);
            var json = fake.GetFile(RunStatePath);
            var currentTime = plan.CreatedAtUtc.ToString("O");
            var legacyTimeText = legacyTime.ToString("O");
            json = json.Replace("\"startedAtUtc\": \"" + currentTime + "\"", "\"startedAtUtc\": \"" + legacyTimeText + "\"");
            json = json.Replace("\"completedAtUtc\": \"" + currentTime + "\"", "\"completedAtUtc\": \"" + legacyTimeText + "\"");
            fake.SetFile(RunStatePath, json);

            var recovered = store.RecoverInterrupted(plan.CreatedAtUtc.AddMinutes(1));

            Assert.That(recovered.Entries.Single().CreatedAtUtc, Is.EqualTo(plan.CreatedAtUtc));
            Assert.That(recovered.Entries.Single().StartedAtUtc, Is.EqualTo(plan.CreatedAtUtc));
            Assert.That(recovered.Entries.Single().CompletedAtUtc, Is.EqualTo(plan.CreatedAtUtc.AddMinutes(1)));
        }

        [Test]
        public void Load_RejectsACompletedTimeBeforeTheRecordedStart()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var entry = BuildAssistantTestData.Entry();
            store.Save(new[] { entry });
            var json = fake.GetFile(HistoryPath);
            var currentField = "\"completedAtUtc\": \"" + entry.CompletedAtUtc.ToString("O") + "\"";
            var invalidField = "\"completedAtUtc\": \"" + entry.CreatedAtUtc.ToString("O") + "\"";
            Assert.That(json, Does.Contain(currentField));
            fake.SetFile(HistoryPath, json.Replace(currentField, invalidField));

            Assert.That(store.Load().Entries, Is.Empty);
        }

        [Test]
        public void Save_RejectsUndefinedOrUnsupportedBuildSettingValues()
        {
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, CreateFileSystem());

            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(status: (BuildAssistantHistoryStatus)999999) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(error: (BuildAssistantError)999999) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(profileKind: (BuildAssistantProfileKind)999999) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(target: (BuildTarget)999999) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(targetGroup: (BuildTargetGroup)999999) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(subtarget: 999999) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(backend: (ScriptingImplementation)999999) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(options: unchecked((BuildOptions)int.MinValue)) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(target: BuildTarget.Android) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(target: BuildTarget.StandaloneWindows) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(targetGroup: BuildTargetGroup.Android) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(namedBuildTarget: "Android") }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(options: BuildOptions.Development) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(options: BuildOptions.DetailedBuildReport | BuildOptions.CompressWithLz4 | BuildOptions.CompressWithLz4HC) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(backend: ScriptingImplementation.IL2CPP, options: BuildOptions.DetailedBuildReport | BuildOptions.Development | BuildOptions.EnableCodeCoverage) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(options: BuildOptions.DetailedBuildReport | BuildOptions.AllowDebugging) }));
        }

        [Test]
        public void Persistence_RejectsNonSequentialSceneOrders()
        {
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, CreateFileSystem());
            var skippedFirst = BuildAssistantTestData.Entry(scenes: new[] { new BuildAssistantScene(1, "scene-a", "Assets/A.unity", true, "hash-a") });
            var duplicate = BuildAssistantTestData.Entry(scenes: new[] { new BuildAssistantScene(0, "scene-a", "Assets/A.unity", true, "hash-a"), new BuildAssistantScene(0, "scene-b", "Assets/B.unity", true, "hash-b") });

            foreach (var entry in new[] { skippedFirst, duplicate })
            {
                Assert.Throws<InvalidDataException>(() => store.Save(new[] { entry }));
                Assert.Throws<InvalidDataException>(() => store.SaveRunState(new RunState(true, entry)));
                Assert.Throws<InvalidDataException>(() => HistoryStore.SerializeExport(entry));
            }
        }

        [Test]
        public void Load_RejectsANonSequentialSceneOrder()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry() });
            var json = fake.GetFile(HistoryPath);
            Assert.That(json, Does.Contain("\"order\": 0"));
            fake.SetFile(HistoryPath, json.Replace("\"order\": 0", "\"order\": 1"));

            Assert.That(store.Load().Entries, Is.Empty);
        }

        [Test]
        public void Persistence_RejectsOversizedProfileAndPreviousRunText()
        {
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, CreateFileSystem());
            var oversizedShortText = new string('x', HistoryStore.MaximumShortTextLength + 1);
            var oversizedPath = new string('x', HistoryStore.MaximumPathLength + 1);
            var invalidEntries = new[]
            {
                BuildAssistantTestData.Entry(profileGuid: oversizedShortText),
                BuildAssistantTestData.Entry(profileName: oversizedShortText),
                BuildAssistantTestData.Entry(profilePath: oversizedPath),
                BuildAssistantTestData.Entry(profileDependencyHash: oversizedShortText),
                BuildAssistantTestData.Entry(profileStableId: oversizedShortText),
                BuildAssistantTestData.Entry(previousRunId: oversizedShortText),
                BuildAssistantTestData.Entry(profileDependencyHash: string.Empty),
                BuildAssistantTestData.Entry(profileStableId: string.Empty)
            };

            foreach (var entry in invalidEntries)
            {
                Assert.Throws<InvalidDataException>(() => store.Save(new[] { entry }));
                Assert.Throws<InvalidDataException>(() => store.SaveRunState(new RunState(true, entry)));
                Assert.Throws<InvalidDataException>(() => HistoryStore.SerializeExport(entry));
            }
        }

        [Test]
        public void Load_RejectsOversizedProfileText()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var entry = BuildAssistantTestData.Entry();
            store.Save(new[] { entry });
            var json = fake.GetFile(HistoryPath);
            var validField = "\"profileName\": \"" + entry.ProfileName + "\"";
            var oversizedField = "\"profileName\": \"" + new string('x', HistoryStore.MaximumShortTextLength + 1) + "\"";
            Assert.That(json, Does.Contain(validField));
            fake.SetFile(HistoryPath, json.Replace(validField, oversizedField));

            Assert.That(store.Load().Entries, Is.Empty);
        }

        [TestCase(BuildTarget.StandaloneWindows64, (int)StandaloneBuildSubtarget.Default, ScriptingImplementation.Mono2x, BuildOptions.DetailedBuildReport)]
        [TestCase(BuildTarget.StandaloneWindows64, (int)StandaloneBuildSubtarget.Player, ScriptingImplementation.IL2CPP, BuildOptions.DetailedBuildReport | BuildOptions.CompressWithLz4)]
        [TestCase(BuildTarget.StandaloneOSX, (int)StandaloneBuildSubtarget.Player, ScriptingImplementation.Mono2x, BuildOptions.DetailedBuildReport | BuildOptions.Development | BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging | BuildOptions.WaitForPlayerConnection | BuildOptions.EnableCodeCoverage | BuildOptions.EnableDeepProfilingSupport)]
        [TestCase(BuildTarget.StandaloneLinux64, (int)StandaloneBuildSubtarget.Player, ScriptingImplementation.IL2CPP, BuildOptions.DetailedBuildReport | BuildOptions.Development | BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging | BuildOptions.WaitForPlayerConnection | BuildOptions.EnableDeepProfilingSupport | BuildOptions.CompressWithLz4HC | BuildOptions.SymlinkSources)]
        public void Load_AcceptsRepresentativeOnePointZeroGeneratedBuildSettings(BuildTarget target, int subtarget, ScriptingImplementation backend, BuildOptions options)
        {
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, CreateFileSystem());
            var entry = BuildAssistantTestData.Entry(target: target, subtarget: subtarget, backend: backend, options: options);

            store.Save(new[] { entry });
            var loaded = store.Load().Entries.Single();

            Assert.That(loaded.Target, Is.EqualTo(target));
            Assert.That(loaded.Subtarget, Is.EqualTo(subtarget));
            Assert.That(loaded.ScriptingBackend, Is.EqualTo(backend));
            Assert.That(loaded.Options, Is.EqualTo(options));
        }

        [Test]
        public void Load_AcceptsARepresentativeOnePointZeroCustomProfileEntry()
        {
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, CreateFileSystem());
            var entry = BuildAssistantTestData.Entry(profileKind: BuildAssistantProfileKind.Custom, profileGuid: "0123456789abcdef0123456789abcdef", profileName: "独自ビルド設定", profilePath: "Assets/Settings/CustomProfile.asset", profileDependencyHash: "custom-profile-hash", profileStableId: "custom:0123456789abcdef0123456789abcdef");

            store.Save(new[] { entry });
            var loaded = store.Load().Entries.Single();

            Assert.That(loaded.ProfileKind, Is.EqualTo(BuildAssistantProfileKind.Custom));
            Assert.That(loaded.ProfileGuid, Is.EqualTo(entry.ProfileGuid));
            Assert.That(loaded.ProfilePath, Is.EqualTo(entry.ProfilePath));
            Assert.That(loaded.ProfileStableId, Is.EqualTo(entry.ProfileStableId));
        }

        [Test]
        public void Load_AcceptsAndResavesAOnePointZeroCustomProfileFixtureWithAnEmptyName()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            fake.SetFile(HistoryPath, CreateOnePointZeroCustomProfileHistoryJson());

            var loaded = store.Load();

            Assert.That(loaded.Entries.Count, Is.EqualTo(1));
            Assert.That(loaded.Entries[0].ProfileKind, Is.EqualTo(BuildAssistantProfileKind.Custom));
            Assert.That(loaded.Entries[0].ProfileName, Is.Empty);
            store.Save(loaded.Entries);
            Assert.That(store.Load().Entries.Single().ProfileName, Is.Empty);
        }

        [Test]
        public void Save_RejectsStatusAndErrorCombinationsThatCannotBeGenerated()
        {
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, CreateFileSystem());

            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(status: BuildAssistantHistoryStatus.Succeeded, error: BuildAssistantError.StalePlan) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(status: BuildAssistantHistoryStatus.Failed, error: BuildAssistantError.None) }));
            Assert.Throws<InvalidDataException>(() => store.Save(new[] { BuildAssistantTestData.Entry(status: BuildAssistantHistoryStatus.Interrupted, error: BuildAssistantError.OutputReservationFailed) }));
        }

        [TestCase("\"status\": 0", "\"status\": 999999")]
        [TestCase("\"error\": 0", "\"error\": 999999")]
        [TestCase("\"profileKind\": 0", "\"profileKind\": 999999")]
        public void Load_RejectsUndefinedHistoryEnums(string validField, string invalidField)
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry() });
            var json = fake.GetFile(HistoryPath);
            Assert.That(json, Does.Contain(validField));
            fake.SetFile(HistoryPath, json.Replace(validField, invalidField));

            var history = store.Load();

            Assert.That(history.Entries, Is.Empty);
            Assert.That(history.Message, Does.Contain("有効な履歴ファイルを読み込めませんでした"));
        }

        [TestCase("status")]
        [TestCase("error")]
        [TestCase("profileKind")]
        [TestCase("target")]
        [TestCase("targetGroup")]
        [TestCase("subtarget")]
        [TestCase("scriptingBackend")]
        [TestCase("options")]
        [TestCase("totalErrors")]
        [TestCase("totalWarnings")]
        public void Load_RejectsAMissingRequiredEntryValue(string memberName)
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry() });
            var json = RemoveFirstMember(fake.GetFile(HistoryPath), memberName);
            Assert.That(JsonDocumentShape.TryParse(json, out _), Is.True);
            fake.SetFile(HistoryPath, json);

            Assert.That(store.Load().Entries, Is.Empty);
        }

        [TestCase("order")]
        [TestCase("enabled")]
        [TestCase("occurrenceCount")]
        [TestCase("assetCount")]
        public void Load_RejectsAMissingRequiredNestedValue(string memberName)
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry() });
            var json = RemoveFirstMember(fake.GetFile(HistoryPath), memberName);
            Assert.That(JsonDocumentShape.TryParse(json, out _), Is.True);
            fake.SetFile(HistoryPath, json);

            Assert.That(store.Load().Entries, Is.Empty);
        }

        [TestCase("\"status\": 0", "\"status\": null")]
        [TestCase("\"status\": 0", "\"status\": 0.0")]
        [TestCase("\"status\": 0", "\"status\": 0e0")]
        [TestCase("\"status\": 0", "\"status\": 2147483648")]
        [TestCase("\"status\": 0", "\"status\": \"0\"")]
        [TestCase("\"message\": \"\"", "\"message\": 0")]
        [TestCase("\"enabled\": true", "\"enabled\": 1")]
        [TestCase("\"GLOBAL\"", "0")]
        public void Load_RejectsARequiredValueWithTheWrongJsonType(string validValue, string invalidValue)
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry() });
            var json = fake.GetFile(HistoryPath);
            Assert.That(json, Does.Contain(validValue));
            fake.SetFile(HistoryPath, json.Replace(validValue, invalidValue));

            Assert.That(store.Load().Entries, Is.Empty);
        }

        [Test]
        public void Load_DoesNotTreatMemberNamesInsideAStringAsEntryMembers()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry() });
            var json = RemoveFirstMember(fake.GetFile(HistoryPath), "status");
            json = RemoveFirstMember(json, "error");
            json = json.Replace("\"message\": \"\"", "\"message\": \"\\\"status\\\": 0, \\\"error\\\": 0\"");
            fake.SetFile(HistoryPath, json);

            Assert.That(store.Load().Entries, Is.Empty);
        }

        [Test]
        public void Load_RejectsAnEscapedSpellingOfARequiredMemberName()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry() });
            var json = fake.GetFile(HistoryPath);
            Assert.That(json, Does.Contain("\"status\": 0"));
            fake.SetFile(HistoryPath, json.Replace("\"status\": 0", "\"st\\u0061tus\": 0"));

            Assert.That(store.Load().Entries, Is.Empty);
        }

        [Test]
        public void Load_RejectsAMissingMemberEvenWhenAnotherEntryDuplicatesIt()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry("first"), BuildAssistantTestData.Entry("second") });
            var lines = fake.GetFile(HistoryPath).Replace("\r\n", "\n").Split('\n').ToList();
            var marker = "\"status\":";
            var statusLines = lines.Select((line, index) => new { line, index }).Where(item => item.line.TrimStart().StartsWith(marker, StringComparison.Ordinal)).Select(item => item.index).ToArray();
            Assert.That(statusLines.Length, Is.EqualTo(2));
            lines.RemoveAt(statusLines[0]);
            var remaining = lines.FindIndex(line => line.TrimStart().StartsWith(marker, StringComparison.Ordinal));
            lines.Insert(remaining + 1, lines[remaining]);
            fake.SetFile(HistoryPath, string.Join("\n", lines));

            Assert.That(store.Load().Entries, Is.Empty);
        }

        [Test]
        public void MissingRequiredMemberPrimary_UsesAndPreservesTheValidBackup()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry("known-good-backup") });
            store.Save(new[] { BuildAssistantTestData.Entry("invalid-primary") });
            fake.SetFile(HistoryPath, RemoveFirstMember(fake.GetFile(HistoryPath), "status"));

            Assert.That(store.Load().RecoveredFromBackup, Is.True);
            Assert.That(store.Load().Entries.Single().RunId, Is.EqualTo("known-good-backup"));
            store.Save(new[] { BuildAssistantTestData.Entry("new-primary") });
            fake.SetFile(HistoryPath, "broken again");

            Assert.That(store.Load().RecoveredFromBackup, Is.True);
            Assert.That(store.Load().Entries.Single().RunId, Is.EqualTo("known-good-backup"));
        }

        [Test]
        public void Load_RejectsADefinedButImpossibleStatusAndErrorCombination()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.Save(new[] { BuildAssistantTestData.Entry() });
            var json = fake.GetFile(HistoryPath);
            var validField = "\"error\": " + ((int)BuildAssistantError.None).ToString();
            var invalidField = "\"error\": " + ((int)BuildAssistantError.StalePlan).ToString();
            Assert.That(json, Does.Contain(validField));
            fake.SetFile(HistoryPath, json.Replace(validField, invalidField));

            Assert.That(store.Load().Entries, Is.Empty);
        }

        [Test]
        public void SaveRunState_RejectsAnUnfinishedFlagWithATerminalEntry()
        {
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, CreateFileSystem());

            Assert.Throws<InvalidDataException>(() => store.SaveRunState(new RunState(false, BuildAssistantTestData.Entry())));
        }

        [Test]
        public void LoadRunState_RejectsAnUnfinishedFlagWithATerminalEntry()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            store.SaveRunState(new RunState(true, BuildAssistantTestData.Entry()));
            var json = fake.GetFile(RunStatePath);
            Assert.That(json, Does.Contain("\"completed\": true"));
            fake.SetFile(RunStatePath, json.Replace("\"completed\": true", "\"completed\": false"));

            Assert.Throws<InvalidDataException>(() => store.RecoverInterrupted(new DateTime(2026, 8, 23, 1, 5, 0, DateTimeKind.Utc)));
            Assert.That(store.HasRunState, Is.True);
        }

        [Test]
        public void LoadRunState_RejectsAMissingCompletedMemberEvenForARunningEntry()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var started = new DateTime(2026, 8, 23, 1, 3, 0, DateTimeKind.Utc);
            store.SaveRunState(RunState.CreateRunning(BuildAssistantTestData.Plan(), started));
            var json = RemoveFirstMember(fake.GetFile(RunStatePath), "completed");
            Assert.That(JsonDocumentShape.TryParse(json, out _), Is.True);
            fake.SetFile(RunStatePath, json);

            Assert.Throws<InvalidDataException>(() => store.RecoverInterrupted(started.AddMinutes(1)));
            Assert.That(store.HasRunState, Is.True);
        }

        [Test]
        public void LoadRunState_RejectsANonBooleanCompletedMember()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var started = new DateTime(2026, 8, 23, 1, 3, 0, DateTimeKind.Utc);
            store.SaveRunState(RunState.CreateRunning(BuildAssistantTestData.Plan(), started));
            var json = fake.GetFile(RunStatePath);
            Assert.That(json, Does.Contain("\"completed\": false"));
            fake.SetFile(RunStatePath, json.Replace("\"completed\": false", "\"completed\": 0"));

            Assert.Throws<InvalidDataException>(() => store.RecoverInterrupted(started.AddMinutes(1)));
            Assert.That(store.HasRunState, Is.True);
        }

        [Test]
        public void RecoverInterrupted_UsesValidBackupWhenPrimaryBoundedReadFails()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var backupPlan = BuildAssistantTestData.Plan(entropy: "ba0c0001");
            var primaryPlan = BuildAssistantTestData.Plan(entropy: "f1a20001");
            var started = backupPlan.CreatedAtUtc.AddSeconds(1);
            store.SaveRunState(RunState.CreateRunning(backupPlan, started));
            store.SaveRunState(RunState.CreateRunning(primaryPlan, started.AddSeconds(1)));
            fake.ReadAllTextBoundedExceptionPath = RunStatePath;
            fake.ReadAllTextBoundedException = new IOException("Injected stream read failure.");

            var recovered = store.RecoverInterrupted(started.AddMinutes(1));

            Assert.That(recovered.Entries.Single().RunId, Is.EqualTo(backupPlan.RunId));
            Assert.That(recovered.Entries.Single().Status, Is.EqualTo(BuildAssistantHistoryStatus.Interrupted));
            Assert.That(store.HasRunState, Is.False);
        }

        [Test]
        public void SaveRunState_AcceptsAnInterruptedTerminalEntry()
        {
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, CreateFileSystem());
            var started = new DateTime(2026, 8, 23, 1, 3, 0, DateTimeKind.Utc);
            var interrupted = RunState.CreateRunning(BuildAssistantTestData.Plan(), started).AsInterrupted(started.AddMinutes(1));

            Assert.DoesNotThrow(() => store.SaveRunState(interrupted));
            var history = store.RecoverInterrupted(started.AddMinutes(2));
            Assert.That(history.Entries[0].Status, Is.EqualTo(BuildAssistantHistoryStatus.Interrupted));
        }

        [Test]
        public void SaveRunState_RefusesToOverwriteAPairWithoutAValidOriginal()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var backupPath = RunStatePath + ".bak";
            fake.SetFile(RunStatePath, "broken primary");
            fake.SetFile(backupPath, "broken backup");

            Assert.Throws<InvalidDataException>(() => store.SaveRunState(RunState.CreateRunning(BuildAssistantTestData.Plan(), DateTime.UtcNow)));
            Assert.That(fake.GetFile(RunStatePath), Is.EqualTo("broken primary"));
            Assert.That(fake.GetFile(backupPath), Is.EqualTo("broken backup"));
            Assert.That(fake.TemporaryFileCount, Is.Zero);
        }

        [Test]
        public void SummaryMetricFailure_PreservesAnAlreadyReadUnitySuccess()
        {
            var started = new DateTime(2026, 8, 23, 1, 3, 0, DateTimeKind.Utc);

            var reduced = BuildReportReducer.Reduce(new ThrowingSummaryMetricReportView(), BuildAssistantTestData.Plan(), started, started.AddMinutes(1));

            Assert.That(reduced.BuildSucceeded, Is.True);
            Assert.That(reduced.Error, Is.EqualTo(BuildAssistantError.ReportReadFailed));
            Assert.That(reduced.Entry.Status, Is.EqualTo(BuildAssistantHistoryStatus.Succeeded));
            Assert.That(reduced.Entry.StartedAtUtc, Is.EqualTo(started));
            Assert.That(reduced.Entry.TotalOutputBytes, Is.Zero);
            Assert.That(reduced.Message, Does.Contain("概要の集計情報"));
        }

        [Test]
        public void Reduce_UsesRecordedUtcBoundsInsteadOfUnityTimesWithAmbiguousKinds()
        {
            var started = new DateTime(2026, 8, 31, 11, 29, 38, DateTimeKind.Utc);
            var completed = started.AddSeconds(25);
            var misleadingStarted = DateTime.SpecifyKind(started.AddHours(-9), DateTimeKind.Local);
            var misleadingCompleted = DateTime.SpecifyKind(completed.AddHours(-9), DateTimeKind.Unspecified);

            var reduced = BuildReportReducer.Reduce(new SuccessfulReportView(misleadingStarted, misleadingCompleted), BuildAssistantTestData.Plan(createdAtUtc: started.AddMinutes(-1)), started, completed);

            Assert.That(reduced.Entry.StartedAtUtc, Is.EqualTo(started));
            Assert.That(reduced.Entry.CompletedAtUtc, Is.EqualTo(completed));
            Assert.That(reduced.Entry.StartedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(reduced.Entry.CompletedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public void PersistenceEntryPoints_RejectAnEntryThatCannotBeReadBack()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var valid = BuildAssistantTestData.Entry("既存の正常な履歴");
            store.Save(new[] { valid });
            store.SaveRunState(new RunState(true, valid));
            var originalHistory = fake.GetFile(HistoryPath);
            var originalRunState = fake.GetFile(RunStatePath);
            var created = new DateTime(2026, 8, 31, 11, 28, 42, DateTimeKind.Utc);
            var invalid = BuildAssistantTestData.Entry(createdAtUtc: created, startedAtUtc: created.AddHours(-9), completedAtUtc: created.AddHours(-9).AddMinutes(1));

            Assert.Throws<InvalidDataException>(() => store.Save(new[] { invalid }));
            Assert.Throws<InvalidDataException>(() => store.SaveRunState(new RunState(true, invalid)));
            Assert.Throws<InvalidDataException>(() => HistoryStore.SerializeExport(invalid));
            Assert.That(fake.GetFile(HistoryPath), Is.EqualTo(originalHistory));
            Assert.That(fake.GetFile(RunStatePath), Is.EqualTo(originalRunState));
            Assert.That(fake.TemporaryFileCount, Is.Zero);
        }

        [Test]
        public void Save_KeepsOnlyNewestTwentyEntries()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var baseline = new DateTime(2026, 8, 23, 2, 0, 0, DateTimeKind.Utc);
            var entries = Enumerable.Range(0, 25).Select(index => BuildAssistantTestData.Entry("run-" + index.ToString("D2"), completedAtUtc: baseline.AddMinutes(index))).ToArray();

            store.Save(entries);
            var history = store.Load();

            Assert.That(history.Entries.Count, Is.EqualTo(20));
            Assert.That(history.Entries[0].RunId, Is.EqualTo("run-24"));
            Assert.That(history.Entries[19].RunId, Is.EqualTo("run-05"));
        }

        [Test]
        public void Save_KeepsTheRequiredRunWhenClockRollbackMakesItTheOldest()
        {
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, CreateFileSystem());
            var baseline = new DateTime(2026, 8, 24, 2, 0, 0, DateTimeKind.Utc);
            var existing = Enumerable.Range(0, 20).Select(index => BuildAssistantTestData.Entry("later-" + index.ToString("D2"), completedAtUtc: baseline.AddMinutes(index))).ToArray();
            var rolledBack = BuildAssistantTestData.Entry("current-after-clock-rollback", createdAtUtc: baseline.AddDays(-1), startedAtUtc: baseline.AddDays(-1).AddSeconds(1), completedAtUtc: baseline.AddDays(-1).AddMinutes(1));

            store.Save(existing);
            store.Save(store.Load().Entries.Concat(new[] { rolledBack }), rolledBack.RunId);
            var history = store.Load();

            Assert.That(history.Entries.Count, Is.EqualTo(20));
            Assert.That(history.Entries.Any(entry => entry.RunId == rolledBack.RunId), Is.True);
            Assert.That(history.Entries.Any(entry => entry.RunId == "later-00"), Is.False);
            Assert.That(history.Entries[0].RunId, Is.EqualTo(rolledBack.RunId));
        }

        [Test]
        public void Save_PreservesExecutionOrderAcrossRepeatedClockRollback()
        {
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, CreateFileSystem());
            var future = new DateTime(2026, 8, 24, 2, 0, 0, DateTimeKind.Utc);
            var existing = Enumerable.Range(0, 20).Select(index => BuildAssistantTestData.Entry("future-" + index.ToString("D2"), completedAtUtc: future.AddMinutes(index))).ToArray();
            store.Save(existing);
            var first = BuildAssistantTestData.Entry("rollback-01", createdAtUtc: future.AddDays(-1), startedAtUtc: future.AddDays(-1).AddSeconds(1), completedAtUtc: future.AddDays(-1).AddMinutes(1));
            store.Save(store.Load().Entries.Concat(new[] { first }), first.RunId);
            var second = BuildAssistantTestData.Entry("rollback-02", createdAtUtc: future.AddDays(-1).AddMinutes(2), startedAtUtc: future.AddDays(-1).AddMinutes(2).AddSeconds(1), completedAtUtc: future.AddDays(-1).AddMinutes(3));

            store.Save(store.Load().Entries.Concat(new[] { second }), second.RunId);
            var history = store.Load();
            var comparable = HistoryComparer.FindLatestComparable(history.Entries, BuildAssistantTestData.Environment());

            Assert.That(history.Entries.Count, Is.EqualTo(20));
            Assert.That(history.Entries[0].RunId, Is.EqualTo(second.RunId));
            Assert.That(history.Entries[1].RunId, Is.EqualTo(first.RunId));
            Assert.That(comparable.RunId, Is.EqualTo(second.RunId));
        }

        [Test]
        public void Export_UsesCreateNewAndProducesTimeIndependentSchemaOneJson()
        {
            var exportDirectory = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory), "BuildAssistantExports");
            var fake = new FakeBuildAssistantFileSystem(BuildAssistantTestData.ProjectRoot, exportDirectory);
            var exporter = new JsonExporter(BuildAssistantTestData.ProjectRoot, fake);
            var entry = BuildAssistantTestData.Entry();
            var firstPath = Path.Combine(exportDirectory, "first.json");
            var secondPath = Path.Combine(exportDirectory, "second.json");

            Assert.That(exporter.Export(entry, firstPath), Is.EqualTo(BuildAssistantError.None));
            Assert.That(exporter.Export(entry, secondPath), Is.EqualTo(BuildAssistantError.None));
            Assert.That(fake.GetFile(secondPath), Is.EqualTo(fake.GetFile(firstPath)));
            Assert.That(fake.GetFile(firstPath), Does.Contain("\"schemaVersion\": 1"));
            Assert.That(exporter.Export(entry, firstPath), Is.EqualTo(BuildAssistantError.OutputAlreadyExists));
            Assert.That(exporter.Export(entry, "relative.json"), Is.EqualTo(BuildAssistantError.InvalidOutputRoot));
        }

        [Test]
        public void Export_ReturnsABoundedErrorWhenAProbeIsDenied()
        {
            var exportDirectory = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory), "BuildAssistantExports");
            var fake = new FakeBuildAssistantFileSystem(BuildAssistantTestData.ProjectRoot, exportDirectory)
            {
                FileExistsException = new SecurityException("Injected export probe denial.")
            };

            Assert.That(new JsonExporter(BuildAssistantTestData.ProjectRoot, fake).Export(BuildAssistantTestData.Entry(), Path.Combine(exportDirectory, "result.json")), Is.EqualTo(BuildAssistantError.HistoryWriteFailed));
        }

        [Test]
        public void Export_RejectsWindowsAlternateStreamsDevicesAndInvalidJsonLeavesWithoutCreatingFiles()
        {
            if (Path.DirectorySeparatorChar != '\\')
                Assert.Ignore("Windows path components only apply on Windows.");
            var exportDirectory = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory), "BuildAssistantExports");
            var fake = new FakeBuildAssistantFileSystem(BuildAssistantTestData.ProjectRoot, exportDirectory);
            var exporter = new JsonExporter(BuildAssistantTestData.ProjectRoot, fake);
            var invalidPaths = new[]
            {
                Path.Combine(exportDirectory, "result.json:stream"),
                Path.Combine(exportDirectory, "CON.json"),
                Path.Combine(exportDirectory, "NUL.json"),
                Path.Combine(exportDirectory, "result.json."),
                Path.Combine(exportDirectory, "result.txt")
            };

            foreach (var path in invalidPaths)
            {
                Assert.That(exporter.Export(BuildAssistantTestData.Entry(), path), Is.EqualTo(BuildAssistantError.InvalidOutputRoot), path);
                Assert.That(fake.FileExists(path), Is.False, path);
            }
        }

        [Test]
        public void Export_RejectsAnOsReportedNetworkParent()
        {
            var exportDirectory = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory), "BuildAssistantExports");
            var fake = new FakeBuildAssistantFileSystem(BuildAssistantTestData.ProjectRoot, exportDirectory);
            fake.MarkNetworkDrive(exportDirectory);

            Assert.That(new JsonExporter(BuildAssistantTestData.ProjectRoot, fake).Export(BuildAssistantTestData.Entry(), Path.Combine(exportDirectory, "result.json")), Is.EqualTo(BuildAssistantError.UnsafeOutputPath));
        }

        [Test]
        public void Export_RejectsAReparseParentBeforeCreateNew()
        {
            var exportDirectory = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory), "BuildAssistantExports");
            var fake = new FakeBuildAssistantFileSystem(BuildAssistantTestData.ProjectRoot, exportDirectory);
            fake.MarkReparse(exportDirectory);
            var path = Path.Combine(exportDirectory, "result.json");

            Assert.That(new JsonExporter(BuildAssistantTestData.ProjectRoot, fake).Export(BuildAssistantTestData.Entry(), path), Is.EqualTo(BuildAssistantError.UnsafeOutputPath));
            Assert.That(fake.FileExists(path), Is.False);
        }

        [Test]
        public void Export_RejectsAUnityManagedParentBeforeCreateNew()
        {
            var assets = Path.Combine(BuildAssistantTestData.ProjectRoot, "Assets");
            var fake = new FakeBuildAssistantFileSystem(BuildAssistantTestData.ProjectRoot, assets);
            var path = Path.Combine(assets, "result.json");

            Assert.That(new JsonExporter(BuildAssistantTestData.ProjectRoot, fake).Export(BuildAssistantTestData.Entry(), path), Is.EqualTo(BuildAssistantError.UnsafeOutputPath));
            Assert.That(fake.FileExists(path), Is.False);
        }

        [Test]
        public void PartialRunStateCleanup_NeverLetsTheOlderRunningBackupReplaceSuccess()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var plan = BuildAssistantTestData.Plan();
            var started = new DateTime(2026, 8, 23, 1, 3, 0, DateTimeKind.Utc);
            var success = BuildAssistantTestData.Entry(plan.RunId, completedAtUtc: started.AddMinutes(1));
            store.SaveRunState(RunState.CreateRunning(plan, started));
            store.SaveRunState(new RunState(true, success));
            store.Save(new[] { success });
            var backupPath = Path.Combine(BuildAssistantTestData.ProjectRoot, "Library", "BuildAssistant", "run-state.json.bak");
            fake.ThrowOnDeletePath = backupPath;

            Assert.Throws<IOException>(() => store.DeleteRunState());
            fake.ThrowOnDeletePath = string.Empty;
            var recovered = store.RecoverInterrupted(started.AddMinutes(2));

            Assert.That(recovered.Entries.Count, Is.EqualTo(1));
            Assert.That(recovered.Entries[0].Status, Is.EqualTo(BuildAssistantHistoryStatus.Succeeded));
            Assert.That(recovered.Entries[0].RunId, Is.EqualTo(success.RunId));
        }

        [Test]
        public void RunningStateRecovery_PreservesAnAlreadyPersistedTerminalResult()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var plan = BuildAssistantTestData.Plan();
            var started = new DateTime(2026, 8, 23, 1, 3, 0, DateTimeKind.Utc);
            var success = BuildAssistantTestData.Entry(plan.RunId, completedAtUtc: started.AddMinutes(1));
            store.SaveRunState(RunState.CreateRunning(plan, started));
            store.Save(new[] { success });

            var recovered = store.RecoverInterrupted(started.AddMinutes(2));

            Assert.That(recovered.Entries.Count, Is.EqualTo(1));
            Assert.That(recovered.Entries[0].Status, Is.EqualTo(BuildAssistantHistoryStatus.Succeeded));
            Assert.That(recovered.Entries[0].RunId, Is.EqualTo(success.RunId));
        }

        [Test]
        public void CompletedStateRecovery_PreservesAnAlreadyPersistedTerminalResultWithTheSameRunId()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var plan = BuildAssistantTestData.Plan();
            var started = new DateTime(2026, 8, 23, 1, 3, 0, DateTimeKind.Utc);
            var persistedFailure = BuildAssistantTestData.Entry(plan.RunId, BuildAssistantHistoryStatus.Failed, started.AddMinutes(1));
            var laterSuccessState = BuildAssistantTestData.Entry(plan.RunId, BuildAssistantHistoryStatus.Succeeded, started.AddMinutes(2));
            store.Save(new[] { persistedFailure });
            store.SaveRunState(new RunState(true, laterSuccessState));

            var recovered = store.RecoverInterrupted(started.AddMinutes(3));

            Assert.That(recovered.Entries.Count, Is.EqualTo(1));
            Assert.That(recovered.Entries[0].Status, Is.EqualTo(BuildAssistantHistoryStatus.Failed));
            Assert.That(recovered.Entries[0].CompletedAtUtc, Is.EqualTo(persistedFailure.CompletedAtUtc));
            Assert.That(store.HasRunState, Is.False);
        }

        [Test]
        public void BuildInProgressHistoryLoad_DoesNotRecoverStateAfterTheStaticGuardWasReloaded()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var started = new DateTime(2026, 8, 23, 1, 3, 0, DateTimeKind.Utc);
            store.SaveRunState(RunState.CreateRunning(BuildAssistantTestData.Plan(), started));

            var duringBuild = BuildAssistantService.LoadHistory(store, started.AddSeconds(30), true);

            Assert.That(duringBuild.Entries, Is.Empty);
            Assert.That(store.HasRunState, Is.True);

            var afterBuild = BuildAssistantService.LoadHistory(store, started.AddMinutes(1), false);

            Assert.That(afterBuild.Entries.Count, Is.EqualTo(1));
            Assert.That(afterBuild.Entries[0].Status, Is.EqualTo(BuildAssistantHistoryStatus.Interrupted));
            Assert.That(store.HasRunState, Is.False);
        }

        [Test]
        public void CorruptRunStatePair_IsQuarantinedWithoutDeletionAndStopsOnlyTheCurrentAttempt()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var primaryPath = Path.Combine(BuildAssistantTestData.ProjectRoot, "Library", "BuildAssistant", "run-state.json");
            var backupPath = primaryPath + ".bak";
            fake.SetFile(primaryPath, "broken primary");
            fake.SetFile(backupPath, "broken backup");

            var failure = BuildAssistantService.RecoverPreviousRunState(store, DateTime.UtcNow, "1234abcd");

            Assert.That(failure.Error, Is.EqualTo(BuildAssistantError.HistoryWriteFailed));
            Assert.That(failure.Message, Does.Contain("削除せず別名へ隔離"));
            Assert.That(fake.FileExists(primaryPath), Is.False);
            Assert.That(fake.FileExists(backupPath), Is.False);
            Assert.That(fake.GetFile(primaryPath + ".invalid-1234abcd"), Is.EqualTo("broken primary"));
            Assert.That(fake.GetFile(backupPath + ".invalid-1234abcd"), Is.EqualTo("broken backup"));
            Assert.That(store.HasRunState, Is.False);
            Assert.That(BuildAssistantService.RecoverPreviousRunState(store, DateTime.UtcNow, "next"), Is.Null);
        }

        [Test]
        public void Quarantine_RefusesToMoveAValidBackupWhenOnlyThePrimaryIsCorrupt()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var primaryPath = Path.Combine(BuildAssistantTestData.ProjectRoot, "Library", "BuildAssistant", "run-state.json");
            store.SaveRunState(RunState.CreateRunning(BuildAssistantTestData.Plan(), DateTime.UtcNow));
            store.SaveRunState(RunState.CreateRunning(BuildAssistantTestData.Plan(entropy: "87654321"), DateTime.UtcNow));
            fake.SetFile(primaryPath, "broken primary");

            Assert.Throws<InvalidOperationException>(() => store.QuarantineInvalidRunState("1234abcd"));
            Assert.That(store.HasRunState, Is.True);
        }

        [Test]
        public void ReentrantHistoryLoad_DoesNotInterruptOrDeleteTheLiveRunState()
        {
            var fake = CreateFileSystem();
            var store = new HistoryStore(BuildAssistantTestData.ProjectRoot, fake);
            var plan = BuildAssistantTestData.Plan();
            var started = new DateTime(2026, 8, 23, 1, 3, 0, DateTimeKind.Utc);
            store.SaveRunState(RunState.CreateRunning(plan, started));
            Assert.That(ExecutionGuard.TryEnter(out var lease), Is.True);
            try
            {
                var duringBuild = BuildAssistantService.LoadHistory(store, started.AddSeconds(30));

                Assert.That(duringBuild.Entries, Is.Empty);
                Assert.That(store.HasRunState, Is.True);
            }
            finally
            {
                lease.Dispose();
            }

            var afterBuild = BuildAssistantService.LoadHistory(store, started.AddMinutes(1));

            Assert.That(afterBuild.Entries.Count, Is.EqualTo(1));
            Assert.That(afterBuild.Entries[0].Status, Is.EqualTo(BuildAssistantHistoryStatus.Interrupted));
            Assert.That(store.HasRunState, Is.False);
        }

        private static string HistoryPath => Path.Combine(BuildAssistantTestData.ProjectRoot, "Library", "BuildAssistant", "history.json");
        private static string RunStatePath => Path.Combine(BuildAssistantTestData.ProjectRoot, "Library", "BuildAssistant", "run-state.json");

        /// <summary>1.0.0の直列化順と全項目を固定し、保存済み独自プロファイルの空名も再現します。</summary>
        private static string CreateOnePointZeroCustomProfileHistoryJson()
        {
            const string runId = "BA-20260823-010203-legacy01";
            var outputRoot = BuildAssistantTestData.OutputRoot;
            var runDirectory = Path.Combine(outputRoot, runId);
            var artifactPath = Path.Combine(runDirectory, "Player.exe");
            var entryMembers = new[]
            {
                "\"runId\":" + QuoteJsonFixtureText(runId),
                "\"createdAtUtc\":\"2026-08-23T01:02:03.0000000Z\"",
                "\"startedAtUtc\":\"2026-08-23T01:02:04.0000000Z\"",
                "\"completedAtUtc\":\"2026-08-23T01:03:03.0000000Z\"",
                "\"status\":" + ((int)BuildAssistantHistoryStatus.Succeeded).ToString(CultureInfo.InvariantCulture),
                "\"error\":" + ((int)BuildAssistantError.None).ToString(CultureInfo.InvariantCulture),
                "\"message\":\"\"",
                "\"outputRoot\":" + QuoteJsonFixtureText(outputRoot),
                "\"runDirectory\":" + QuoteJsonFixtureText(runDirectory),
                "\"artifactPath\":" + QuoteJsonFixtureText(artifactPath),
                "\"profileKind\":" + ((int)BuildAssistantProfileKind.Custom).ToString(CultureInfo.InvariantCulture),
                "\"profileGuid\":\"0123456789abcdef0123456789abcdef\"",
                "\"profileName\":\"\"",
                "\"profilePath\":\"Assets/Settings/CustomProfile.asset\"",
                "\"profileDependencyHash\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\"",
                "\"profileStableId\":\"custom:0123456789abcdef0123456789abcdef\"",
                "\"target\":" + ((int)BuildTarget.StandaloneWindows64).ToString(CultureInfo.InvariantCulture),
                "\"targetGroup\":" + ((int)BuildTargetGroup.Standalone).ToString(CultureInfo.InvariantCulture),
                "\"namedBuildTarget\":\"Standalone\"",
                "\"subtarget\":" + ((int)StandaloneBuildSubtarget.Player).ToString(CultureInfo.InvariantCulture),
                "\"scriptingBackend\":" + ((int)ScriptingImplementation.Mono2x).ToString(CultureInfo.InvariantCulture),
                "\"options\":" + ((int)BuildOptions.DetailedBuildReport).ToString(CultureInfo.InvariantCulture),
                "\"effectiveDefines\":[\"GLOBAL\"]",
                "\"scenes\":[{\"order\":0,\"guid\":\"scene-guid\",\"assetPath\":\"Assets/Main.unity\",\"enabled\":true,\"dependencyHash\":\"scene-hash\"}]",
                "\"totalErrors\":0",
                "\"totalWarnings\":0",
                "\"totalOutputBytes\":\"100\"",
                "\"packedContentBytes\":\"80\"",
                "\"packedOverheadBytes\":\"5\"",
                "\"assets\":[{\"assetPath\":\"Assets/A.asset\",\"packedBytes\":\"80\",\"occurrenceCount\":1}]",
                "\"types\":[{\"typeName\":\"Type.A\",\"packedBytes\":\"80\",\"occurrenceCount\":1,\"assetCount\":1}]",
                "\"previousRunId\":\"\"",
                "\"totalOutputDeltaBytes\":\"0\"",
                "\"packedContentDeltaBytes\":\"0\""
            };
            return "{\"schemaVersion\":1,\"entries\":[{" + string.Join(",", entryMembers) + "}]}";
        }

        /// <summary>固定試験文のパス区切りと引用符をJSON文字列として扱える形へ変換します。</summary>
        private static string QuoteJsonFixtureText(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        /// <summary>整形済みJSONから、指定した最初の項目行を取り除きます。</summary>
        private static string RemoveFirstMember(string json, string memberName)
        {
            var lines = json.Replace("\r\n", "\n").Split('\n').ToList();
            var marker = "\"" + memberName + "\":";
            var index = lines.FindIndex(line => line.TrimStart().StartsWith(marker, StringComparison.Ordinal));
            Assert.That(index, Is.GreaterThanOrEqualTo(0), memberName + " の項目行がありません。");
            if (!lines[index].TrimEnd().EndsWith(",", StringComparison.Ordinal))
            {
                var previous = index - 1;
                while (previous >= 0 && string.IsNullOrWhiteSpace(lines[previous]))
                    previous--;
                Assert.That(previous, Is.GreaterThanOrEqualTo(0), memberName + " の直前行がありません。");
                var previousEnd = lines[previous].LastIndexOf(',');
                Assert.That(previousEnd, Is.GreaterThanOrEqualTo(0), memberName + " の直前項目に区切りがありません。");
                lines[previous] = lines[previous].Remove(previousEnd, 1);
            }
            lines.RemoveAt(index);
            return string.Join("\n", lines);
        }

        private static FakeBuildAssistantFileSystem CreateFileSystem()
        {
            return new FakeBuildAssistantFileSystem(BuildAssistantTestData.ProjectRoot, Path.Combine(BuildAssistantTestData.ProjectRoot, "Library"));
        }

        private sealed class ThrowingSummaryMetricReportView : IBuildReportView
        {
            public BuildResult Result => BuildResult.Succeeded;
            public DateTime BuildStartedAt => throw new IOException("Injected summary metric failure.");
            public DateTime BuildEndedAt => throw new InvalidOperationException();
            public int TotalErrors => throw new InvalidOperationException();
            public int TotalWarnings => throw new InvalidOperationException();
            public ulong TotalSize => throw new InvalidOperationException();
            public PackedAssets[] PackedAssets => throw new InvalidOperationException();
        }

        /// <summary>成功報告に、種類情報が曖昧な時刻を含める試験用の表示です。</summary>
        private sealed class SuccessfulReportView : IBuildReportView
        {
            // Unityが報告した開始時刻を保持します。
            private readonly DateTime reportedStartedAt;
            // Unityが報告した終了時刻を保持します。
            private readonly DateTime reportedCompletedAt;

            /// <summary>試験で指定した報告時刻を保持します。</summary>
            internal SuccessfulReportView(DateTime reportedStartedAt, DateTime reportedCompletedAt)
            {
                this.reportedStartedAt = reportedStartedAt;
                this.reportedCompletedAt = reportedCompletedAt;
            }

            public BuildResult Result => BuildResult.Succeeded;
            public DateTime BuildStartedAt => reportedStartedAt;
            public DateTime BuildEndedAt => reportedCompletedAt;
            public int TotalErrors => 0;
            public int TotalWarnings => 0;
            public ulong TotalSize => 128;
            public PackedAssets[] PackedAssets => Array.Empty<PackedAssets>();
        }
    }
}
