using System;
using System.IO;
using System.Linq;
using System.Security;
using UnityEditor;

namespace BuildAssistant.Editor
{
    /// <summary>Previews, executes, records, and explicitly exports one safe desktop standalone build at a time.</summary>
    public static class BuildAssistantService
    {
        /// <summary>Captures the effective profile, settings, scenes, imported-content revision, package manifests, and StreamingAssets without mutating them.</summary>
        /// <param name="absoluteOutputRoot">An existing absolute directory or exactly one missing child of an existing directory.</param>
        /// <returns>An immutable plan whose Error explains why it cannot be built.</returns>
        public static BuildAssistantPlan Preview(string absoluteOutputRoot)
        {
            EnvironmentSnapshot environment = null;
            try
            {
                environment = new UnityBuildEnvironment().Capture();
                var safeOutput = new SafeBuildOutput(ProjectRoot);
                var location = safeOutput.Inspect(absoluteOutputRoot);
                if (!location.IsValid)
                    return PlanFactory.CreateFailure(environment, location.Error, location.Message, location.NormalizedPath, location.Mode);

                var createdAtUtc = DateTime.UtcNow;
                var entropy = Guid.NewGuid().ToString("N").Substring(0, 8);
                var runId = PlanFactory.CreateRunId(createdAtUtc, entropy);
                var history = new HistoryStore(ProjectRoot).Load();
                var previous = HistoryComparer.FindLatestComparable(history.Entries, environment);
                var context = new PlanningContext(environment, location.NormalizedPath, location.Mode, createdAtUtc, entropy, safeOutput.IsRunPathBusy(location.NormalizedPath, runId), previous);
                return PlanFactory.Create(context);
            }
            catch (EnvironmentCaptureException exception)
            {
                return PlanFactory.CreateFailure(environment, exception.Error, exception.Message);
            }
            catch (Exception exception)
            {
                return PlanFactory.CreateFailure(environment, BuildAssistantError.InvalidOutputRoot, exception.Message);
            }
        }

        /// <summary>Builds exactly one fresh plan after recapturing the preview snapshot and reserving a new run directory.</summary>
        /// <param name="plan">A ready plan returned by Preview.</param>
        /// <returns>A detached result that reports build success independently from history persistence.</returns>
        public static BuildAssistantBuildResult Build(BuildAssistantPlan plan)
        {
            if (plan == null)
                return Failure(BuildAssistantError.StalePlan, "A plan is required.");
            if (!plan.IsReady)
                return Failure(plan.Error, plan.Message);
            if (!ExecutionGuard.TryEnter(out var lease))
                return Failure(BuildAssistantError.BuildAlreadyRunning, "Build Assistant is already running a build.");

            using (lease)
            {
                if (BuildPipeline.isBuildingPlayer)
                    return Failure(BuildAssistantError.BuildAlreadyRunning, "Unity is already building a player.");
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                    return Failure(BuildAssistantError.EditorBusy, "The editor is compiling, updating, or changing play mode.");

                var historyStore = new HistoryStore(ProjectRoot);
                if (historyStore.HasRunState)
                {
                    try
                    {
                        historyStore.RecoverInterrupted(DateTime.UtcNow);
                    }
                    catch (Exception exception)
                    {
                        return Failure(BuildAssistantError.HistoryWriteFailed, "The previous durable run state could not be recovered: " + exception.Message);
                    }
                }

                EnvironmentSnapshot current;
                try
                {
                    current = new UnityBuildEnvironment().Capture();
                }
                catch (EnvironmentCaptureException exception)
                {
                    return Failure(exception.Error, exception.Message);
                }

                if (!SnapshotComparer.AreEquivalent(plan, current, out var difference))
                    return Failure(BuildAssistantError.StalePlan, difference);

                if (!TryCreateSafeOutput(() => new SafeBuildOutput(ProjectRoot), out var safeOutput, out var outputFailure))
                    return outputFailure;
                using (var reservation = safeOutput.Reserve(plan))
                {
                    if (!reservation.IsReserved)
                        return Failure(reservation.Error, reservation.Message);

                    var startedAtUtc = DateTime.UtcNow;
                    var runningState = RunState.CreateRunning(plan, startedAtUtc);
                    try
                    {
                        historyStore.SaveRunState(runningState);
                    }
                    catch (Exception exception)
                    {
                        return Failure(BuildAssistantError.OutputReservationFailed, "The durable run state could not be reserved: " + exception.Message);
                    }

                    BuildReportReduction reduction;
                    var revalidationError = reservation.Revalidate(plan, out var revalidationMessage);
                    if (revalidationError != BuildAssistantError.None)
                    {
                        reduction = BuildReportReducer.CreateFailure(plan, startedAtUtc, DateTime.UtcNow, revalidationError, revalidationMessage);
                    }
                    else
                    {
                        try
                        {
                            var report = new UnityBuildExecutor().Execute(plan);
                            reduction = BuildReportReducer.Reduce(report, plan, startedAtUtc, DateTime.UtcNow);
                        }
                        catch (Exception exception)
                        {
                            reduction = BuildReportReducer.CreateFailure(plan, startedAtUtc, DateTime.UtcNow, BuildAssistantError.BuildInvocationFailed, exception.Message);
                        }
                    }

                    try
                    {
                        historyStore.SaveRunState(new RunState(true, reduction.Entry));
                    }
                    catch (Exception)
                    {
                    }

                    try
                    {
                        var history = historyStore.Load();
                        historyStore.Save(history.Entries.Where(entry => !StringComparer.Ordinal.Equals(entry.RunId, reduction.Entry.RunId)).Concat(new[] { reduction.Entry }));
                        var message = reduction.Message;
                        try
                        {
                            historyStore.DeleteRunState();
                        }
                        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException || exception is ArgumentException || exception is NotSupportedException)
                        {
                            message = message.Length == 0 ? "History was saved; run-state cleanup will be retried: " + exception.Message : message + " Run-state cleanup will be retried: " + exception.Message;
                        }

                        return new BuildAssistantBuildResult(reduction.BuildSucceeded, true, reduction.Error, message, reduction.Entry);
                    }
                    catch (Exception exception)
                    {
                        var error = reduction.BuildSucceeded ? BuildAssistantError.HistoryWriteFailed : reduction.Error;
                        var message = reduction.Message.Length == 0 ? "The build completed, but history persistence failed: " + exception.Message : reduction.Message + " History persistence also failed: " + exception.Message;
                        return new BuildAssistantBuildResult(reduction.BuildSucceeded, false, error, message, reduction.Entry);
                    }
                }
            }
        }

        /// <summary>Loads the newest 20 detached history entries and records a leftover non-live running state as Interrupted.</summary>
        /// <returns>An immutable newest-first history snapshot.</returns>
        public static BuildAssistantHistory LoadHistory()
        {
            return LoadHistory(new HistoryStore(ProjectRoot), DateTime.UtcNow);
        }

        internal static BuildAssistantHistory LoadHistory(HistoryStore store, DateTime nowUtc)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (!ExecutionGuard.TryEnter(out var lease))
                return LoadHistoryWithoutRecovery(store);
            using (lease)
            {
                try
                {
                    return store.HasRunState ? store.RecoverInterrupted(nowUtc) : store.Load();
                }
                catch (Exception exception)
                {
                    return new BuildAssistantHistory(Array.Empty<BuildAssistantHistoryEntry>(), false, exception.Message);
                }
            }
        }

        private static BuildAssistantHistory LoadHistoryWithoutRecovery(HistoryStore store)
        {
            try
            {
                return store.Load();
            }
            catch (Exception exception)
            {
                return new BuildAssistantHistory(Array.Empty<BuildAssistantHistoryEntry>(), false, exception.Message);
            }
        }

        /// <summary>Exports exactly one history entry as schema-1 JSON using create-new semantics.</summary>
        /// <param name="entry">The detached history entry to export.</param>
        /// <param name="absolutePath">The absolute new .json path whose existing local parent meets the output safety policy. Existing files are never overwritten.</param>
        /// <returns>None on success, or a bounded path or persistence error.</returns>
        public static BuildAssistantError ExportJson(BuildAssistantHistoryEntry entry, string absolutePath)
        {
            return new JsonExporter(ProjectRoot).Export(entry, absolutePath);
        }

        internal static bool TryCreateSafeOutput(Func<SafeBuildOutput> factory, out SafeBuildOutput safeOutput, out BuildAssistantBuildResult failure)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            try
            {
                safeOutput = factory();
                if (safeOutput == null)
                {
                    failure = Failure(BuildAssistantError.UnsafeOutputPath, "The output policy could not be created.");
                    return false;
                }
                failure = null;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                safeOutput = null;
                failure = Failure(BuildAssistantError.InvalidOutputRoot, "The output policy could not resolve the project root: " + exception.Message);
                return false;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException)
            {
                safeOutput = null;
                failure = Failure(BuildAssistantError.UnsafeOutputPath, "The output policy could not verify the project root safely: " + exception.Message);
                return false;
            }
        }

        private static string ProjectRoot => Directory.GetParent(UnityEngine.Application.dataPath).FullName;

        private static BuildAssistantBuildResult Failure(BuildAssistantError error, string message) => new BuildAssistantBuildResult(false, false, error, message, null);
    }
}
