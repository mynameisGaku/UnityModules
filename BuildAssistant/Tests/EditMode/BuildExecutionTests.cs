using System;
using System.IO;
using System.Linq;
using BuildAssistant.Editor;
using NUnit.Framework;

namespace BuildAssistant.Tests
{
    public sealed class BuildExecutionTests
    {
        [Test]
        public void RunningStateWriteFailure_DoesNotInvokeTheBuildAndReleasesTheReservation()
        {
            var context = CreateContext();
            var invoked = false;
            context.FileSystem.ThrowOnWritePathPrefix = RunStatePath + ".";

            var result = Execute(context, (plan, reservation, startedAtUtc) =>
            {
                invoked = true;
                return Success(plan, startedAtUtc);
            });

            Assert.That(invoked, Is.False);
            Assert.That(result.Error, Is.EqualTo(BuildAssistantError.OutputReservationFailed));
            Assert.That(context.Store.HasRunState, Is.False);
            Assert.That(context.FileSystem.FileExists(ReservationPath(context.Plan)), Is.False);
        }

        [Test]
        public void UnexpectedInitialRevalidationFailure_IsPersistedAndReleasesTheReservation()
        {
            var context = CreateContext();
            var invoked = false;
            context.FileSystem.UnexpectedGetAttributesPath = context.Plan.RunDirectory;
            context.FileSystem.UnexpectedGetAttributesCall = 2;

            var result = Execute(context, (plan, reservation, startedAtUtc) =>
            {
                invoked = true;
                return Success(plan, startedAtUtc);
            });

            Assert.That(invoked, Is.False);
            Assert.That(result.BuildSucceeded, Is.False);
            Assert.That(result.HistoryPersisted, Is.True);
            Assert.That(result.Error, Is.EqualTo(BuildAssistantError.OutputReservationFailed));
            Assert.That(context.Store.Load().Entries.Single().Error, Is.EqualTo(BuildAssistantError.OutputReservationFailed));
            Assert.That(context.Store.HasRunState, Is.False);
            Assert.That(context.FileSystem.FileExists(ReservationPath(context.Plan)), Is.False);
        }

        [Test]
        public void PreprocessorOutputRejection_PreservesItsReasonThroughTheWholeBuildService()
        {
            var context = CreateContext();

            var result = Execute(context, (plan, reservation, startedAtUtc) =>
            {
                var executor = new UnityBuildExecutor(activePlan =>
                {
                    context.FileSystem.SetFile(Path.Combine(activePlan.RunDirectory, "先行前処理の内容.txt"), "unexpected");
                    BuildInputPreprocessor.Validate(() => BuildAssistantTestData.Environment());
                    return null;
                });
                executor.Execute(plan, reservation);
                return Success(plan, startedAtUtc);
            });

            Assert.That(result.BuildSucceeded, Is.False);
            Assert.That(result.HistoryPersisted, Is.True);
            Assert.That(result.Error, Is.EqualTo(BuildAssistantError.OutputAlreadyExists));
            Assert.That(context.Store.Load().Entries.Single().Error, Is.EqualTo(BuildAssistantError.OutputAlreadyExists));
            Assert.That(context.Store.HasRunState, Is.False);
            Assert.That(context.FileSystem.FileExists(ReservationPath(context.Plan)), Is.False);
            Assert.DoesNotThrow(() => BuildInputGuard.Begin(BuildAssistantTestData.Plan(entropy: "87654321")).Dispose());
        }

        [Test]
        public void TerminalStateWriteFailure_PersistsHistoryAndKeepsTheRunningStateForRecovery()
        {
            var context = CreateContext();

            var result = Execute(context, (plan, reservation, startedAtUtc) =>
            {
                context.FileSystem.ThrowOnReplacePath = RunStatePath;
                return Success(plan, startedAtUtc);
            });

            Assert.That(result.BuildSucceeded, Is.True);
            Assert.That(result.HistoryPersisted, Is.True);
            Assert.That(result.Message, Does.Contain("後片付けを次回に再試行"));
            Assert.That(context.Store.HasRunState, Is.True);
            Assert.That(context.Store.Load().Entries.Single().RunId, Is.EqualTo(context.Plan.RunId));

            context.FileSystem.ThrowOnReplacePath = string.Empty;
            var recovered = context.Store.RecoverInterrupted(context.Now.AddMinutes(1));
            Assert.That(recovered.Entries.Single().Status, Is.EqualTo(BuildAssistantHistoryStatus.Succeeded));
            Assert.That(context.Store.HasRunState, Is.False);
        }

        [Test]
        public void CorruptRunStatePairDuringBuild_IsNotDeletedAfterHistorySuccess()
        {
            var context = CreateContext();
            var backupPath = RunStatePath + ".bak";

            var result = Execute(context, (plan, reservation, startedAtUtc) =>
            {
                context.FileSystem.SetFile(RunStatePath, "broken primary");
                context.FileSystem.SetFile(backupPath, "broken backup");
                return Success(plan, startedAtUtc);
            });

            Assert.That(result.BuildSucceeded, Is.True);
            Assert.That(result.HistoryPersisted, Is.True);
            Assert.That(result.Message, Does.Contain("後片付けを次回に再試行"));
            Assert.That(context.Store.Load().Entries.Single().RunId, Is.EqualTo(context.Plan.RunId));
            Assert.That(context.FileSystem.GetFile(RunStatePath), Is.EqualTo("broken primary"));
            Assert.That(context.FileSystem.GetFile(backupPath), Is.EqualTo("broken backup"));
        }

        [Test]
        public void TerminalAndHistoryWriteFailures_KeepTheOriginalRunStatePair()
        {
            var context = CreateContext();
            var backupPath = RunStatePath + ".bak";

            var result = Execute(context, (plan, reservation, startedAtUtc) =>
            {
                context.FileSystem.SetFile(RunStatePath, "broken primary");
                context.FileSystem.SetFile(backupPath, "broken backup");
                context.FileSystem.ThrowOnWritePathPrefix = HistoryPath + ".";
                return Success(plan, startedAtUtc);
            });

            Assert.That(result.BuildSucceeded, Is.True);
            Assert.That(result.HistoryPersisted, Is.False);
            Assert.That(result.Error, Is.EqualTo(BuildAssistantError.HistoryWriteFailed));
            Assert.That(result.Message, Does.Contain("JSONへ書き出して保管"));
            Assert.That(context.Store.Load().Entries, Is.Empty);
            Assert.That(context.FileSystem.GetFile(RunStatePath), Is.EqualTo("broken primary"));
            Assert.That(context.FileSystem.GetFile(backupPath), Is.EqualTo("broken backup"));
        }

        [Test]
        public void HistoryWriteFailure_PreservesTheTerminalStateForRecovery()
        {
            var context = CreateContext();
            context.Store.Save(new[] { BuildAssistantTestData.Entry("earlier") });

            var result = Execute(context, (plan, reservation, startedAtUtc) =>
            {
                context.FileSystem.ThrowOnReplacePath = HistoryPath;
                return Success(plan, startedAtUtc);
            });

            Assert.That(result.BuildSucceeded, Is.True);
            Assert.That(result.HistoryPersisted, Is.False);
            Assert.That(result.Error, Is.EqualTo(BuildAssistantError.HistoryWriteFailed));
            Assert.That(context.Store.HasRunState, Is.True);
            Assert.That(context.Store.Load().Entries.Single().RunId, Is.EqualTo("earlier"));

            context.FileSystem.ThrowOnReplacePath = string.Empty;
            var recovered = context.Store.RecoverInterrupted(context.Now.AddMinutes(1));
            Assert.That(recovered.Entries.Any(entry => entry.RunId == context.Plan.RunId && entry.Status == BuildAssistantHistoryStatus.Succeeded), Is.True);
            Assert.That(context.Store.HasRunState, Is.False);
        }

        [Test]
        public void CleanupFailure_ReportsTheRetryAndKeepsARecoverableTerminalState()
        {
            var context = CreateContext();

            var result = Execute(context, (plan, reservation, startedAtUtc) =>
            {
                context.FileSystem.ThrowOnDeletePath = RunStatePath;
                return Success(plan, startedAtUtc);
            });

            Assert.That(result.BuildSucceeded, Is.True);
            Assert.That(result.HistoryPersisted, Is.True);
            Assert.That(result.Message, Does.Contain("後片付けを次回に再試行"));
            Assert.That(context.Store.HasRunState, Is.True);

            context.FileSystem.ThrowOnDeletePath = string.Empty;
            var recovered = context.Store.RecoverInterrupted(context.Now.AddMinutes(1));
            Assert.That(recovered.Entries.Count(entry => entry.RunId == context.Plan.RunId), Is.EqualTo(1));
            Assert.That(recovered.Entries.Single(entry => entry.RunId == context.Plan.RunId).Status, Is.EqualTo(BuildAssistantHistoryStatus.Succeeded));
            Assert.That(context.Store.HasRunState, Is.False);
        }

        [Test]
        public void UnexpectedBuildException_IsPersistedWithoutItsEnglishDiagnostic()
        {
            var context = CreateContext();

            var result = Execute(context, (plan, reservation, startedAtUtc) => throw new InvalidOperationException("Legacy English diagnostic."));

            Assert.That(result.BuildSucceeded, Is.False);
            Assert.That(result.HistoryPersisted, Is.True);
            Assert.That(result.Error, Is.EqualTo(BuildAssistantError.BuildInvocationFailed));
            Assert.That(result.Message, Does.Contain("予期しない問題"));
            Assert.That(result.Message, Does.Not.Contain("Legacy English"));
            Assert.That(context.Store.HasRunState, Is.False);
        }

        [Test]
        public void FullHistoryAfterClockRollback_StillPersistsTheCurrentResult()
        {
            var context = CreateContext();
            var later = Enumerable.Range(0, 20).Select(index => BuildAssistantTestData.Entry("later-" + index.ToString("D2"), completedAtUtc: context.Now.AddDays(1).AddMinutes(index))).ToArray();
            context.Store.Save(later);

            var result = Execute(context, (plan, reservation, startedAtUtc) => Success(plan, startedAtUtc));
            var history = context.Store.Load();

            Assert.That(result.BuildSucceeded, Is.True);
            Assert.That(result.HistoryPersisted, Is.True);
            Assert.That(history.Entries.Count, Is.EqualTo(20));
            Assert.That(history.Entries.Any(entry => entry.RunId == context.Plan.RunId), Is.True);
            Assert.That(history.Entries[0].RunId, Is.EqualTo(context.Plan.RunId));
            Assert.That(context.Store.HasRunState, Is.False);
        }

        private static BuildAssistantBuildResult Execute(Context context, Func<BuildAssistantPlan, OutputReservation, DateTime, BuildReportReduction> executeBuild)
        {
            return BuildAssistantService.ExecuteReservedBuild(context.Plan, context.Store, context.SafeOutput, () => context.Now, executeBuild);
        }

        private static BuildReportReduction Success(BuildAssistantPlan plan, DateTime startedAtUtc)
        {
            var entry = BuildAssistantTestData.Entry(plan.RunId, createdAtUtc: plan.CreatedAtUtc, startedAtUtc: startedAtUtc, completedAtUtc: startedAtUtc.AddSeconds(1), profileStableId: plan.ProfileStableId, target: plan.Target, subtarget: plan.Subtarget, backend: plan.ScriptingBackend, options: plan.Options, scenes: plan.Scenes, targetGroup: plan.TargetGroup);
            return new BuildReportReduction(true, BuildAssistantError.None, string.Empty, entry);
        }

        private static Context CreateContext()
        {
            var parent = Path.GetDirectoryName(BuildAssistantTestData.OutputRoot);
            var fileSystem = new FakeBuildAssistantFileSystem(parent, BuildAssistantTestData.OutputRoot, BuildAssistantTestData.ProjectRoot);
            var plan = BuildAssistantTestData.Plan();
            return new Context(fileSystem, new HistoryStore(BuildAssistantTestData.ProjectRoot, fileSystem), new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fileSystem), plan, plan.CreatedAtUtc.AddMinutes(1));
        }

        private static string HistoryPath => Path.Combine(BuildAssistantTestData.ProjectRoot, "Library", "BuildAssistant", "history.json");
        private static string RunStatePath => Path.Combine(BuildAssistantTestData.ProjectRoot, "Library", "BuildAssistant", "run-state.json");
        private static string ReservationPath(BuildAssistantPlan plan) => Path.Combine(plan.OutputRoot, "." + plan.RunId + ".reserve");

        private sealed class Context
        {
            internal Context(FakeBuildAssistantFileSystem fileSystem, HistoryStore store, SafeBuildOutput safeOutput, BuildAssistantPlan plan, DateTime now)
            {
                FileSystem = fileSystem;
                Store = store;
                SafeOutput = safeOutput;
                Plan = plan;
                Now = now;
            }

            internal FakeBuildAssistantFileSystem FileSystem { get; }
            internal HistoryStore Store { get; }
            internal SafeBuildOutput SafeOutput { get; }
            internal BuildAssistantPlan Plan { get; }
            internal DateTime Now { get; }
        }
    }
}
