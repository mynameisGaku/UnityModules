using System;
using System.IO;
using System.Linq;
using System.Security;
using BuildAssistant.Editor;
using NUnit.Framework;
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
            Assert.That(history.Entries[0].Message, Does.Contain("not restarted"));
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
        public void SummaryMetricFailure_PreservesAnAlreadyReadUnitySuccess()
        {
            var started = new DateTime(2026, 8, 23, 1, 3, 0, DateTimeKind.Utc);

            var reduced = BuildReportReducer.Reduce(new ThrowingSummaryMetricReportView(), BuildAssistantTestData.Plan(), started, started.AddMinutes(1));

            Assert.That(reduced.BuildSucceeded, Is.True);
            Assert.That(reduced.Error, Is.EqualTo(BuildAssistantError.ReportReadFailed));
            Assert.That(reduced.Entry.Status, Is.EqualTo(BuildAssistantHistoryStatus.Succeeded));
            Assert.That(reduced.Entry.StartedAtUtc, Is.EqualTo(started));
            Assert.That(reduced.Entry.TotalOutputBytes, Is.Zero);
            Assert.That(reduced.Message, Does.Contain("summary analytics"));
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
    }
}
