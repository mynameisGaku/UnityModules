using System;
using System.IO;
using System.Security;

namespace BuildAssistant.Editor
{
    internal sealed class OutputReservation : IDisposable
    {
        private readonly BuildAssistantFileSystem fileSystem;
        private readonly string reservationPath;
        private readonly DirectoryIdentityLease rootLease;
        private readonly DirectoryIdentityLease runLease;
        private bool disposed;

        internal OutputReservation(BuildAssistantError error, string message, BuildAssistantFileSystem fileSystem = null, string reservationPath = "", DirectoryIdentityLease rootLease = null, DirectoryIdentityLease runLease = null)
        {
            Error = error;
            Message = message ?? string.Empty;
            this.fileSystem = fileSystem;
            this.reservationPath = reservationPath ?? string.Empty;
            this.rootLease = rootLease;
            this.runLease = runLease;
        }

        internal BuildAssistantError Error { get; }
        internal string Message { get; }
        internal bool IsReserved => Error == BuildAssistantError.None;

        internal BuildAssistantError Revalidate(BuildAssistantPlan plan, out string message)
        {
            if (!IsReserved || plan == null || fileSystem == null || rootLease == null || runLease == null)
            {
                message = "The output reservation is unavailable.";
                return BuildAssistantError.OutputReservationFailed;
            }

            try
            {
                if (!fileSystem.FileExists(reservationPath) || !StringComparer.Ordinal.Equals(fileSystem.ReadAllText(reservationPath), plan.RunId + Environment.NewLine))
                {
                    message = "The output reservation marker changed before build invocation.";
                    return BuildAssistantError.OutputReservationFailed;
                }
                if ((fileSystem.GetAttributes(plan.OutputRoot) & FileAttributes.ReparsePoint) != 0 || (fileSystem.GetAttributes(plan.RunDirectory) & FileAttributes.ReparsePoint) != 0)
                {
                    message = "The output root or reserved run directory became a reparse point.";
                    return BuildAssistantError.UnsafeOutputPath;
                }
                var canonicalRoot = fileSystem.GetCanonicalDirectoryPath(plan.OutputRoot);
                var canonicalRun = fileSystem.GetCanonicalDirectoryPath(plan.RunDirectory);
                if (!LocationPolicy.CanonicalEquals(rootLease.CanonicalPath, canonicalRoot) || !LocationPolicy.CanonicalEquals(runLease.CanonicalPath, canonicalRun) || !LocationPolicy.CanonicalContains(canonicalRoot, canonicalRun))
                {
                    message = "The leased output identity changed before build invocation.";
                    return BuildAssistantError.UnsafeOutputPath;
                }
                if (fileSystem.FileExists(plan.ArtifactPath) || fileSystem.DirectoryExists(plan.ArtifactPath) || !fileSystem.IsDirectoryEmpty(plan.RunDirectory))
                {
                    message = "The fresh run directory received content before build invocation.";
                    return BuildAssistantError.OutputAlreadyExists;
                }

                message = string.Empty;
                return BuildAssistantError.None;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException || exception is ArgumentException || exception is NotSupportedException)
            {
                message = "The leased output identity could not be revalidated: " + exception.Message;
                return BuildAssistantError.OutputReservationFailed;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (fileSystem != null && reservationPath.Length > 0)
            {
                try
                {
                    fileSystem.DeleteFile(reservationPath);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException || exception is ArgumentException || exception is NotSupportedException)
                {
                }
            }
            runLease?.Dispose();
            rootLease?.Dispose();
        }
    }

    internal sealed class SafeBuildOutput
    {
        private readonly LocationPolicy locationPolicy;
        private readonly BuildAssistantFileSystem fileSystem;

        internal SafeBuildOutput(string projectRoot, BuildAssistantFileSystem fileSystem = null)
        {
            this.fileSystem = fileSystem ?? new BuildAssistantFileSystem();
            locationPolicy = new LocationPolicy(projectRoot, this.fileSystem);
        }

        internal LocationInspection Inspect(string outputRoot) => locationPolicy.Inspect(outputRoot);

        internal bool IsRunPathBusy(string outputRoot, string runId)
        {
            var runDirectory = Path.Combine(outputRoot, runId);
            var reservationPath = GetReservationPath(outputRoot, runId);
            return fileSystem.DirectoryExists(runDirectory) || fileSystem.FileExists(runDirectory) || fileSystem.FileExists(reservationPath) || fileSystem.DirectoryExists(reservationPath);
        }

        internal OutputReservation Reserve(BuildAssistantPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            var reservationPath = string.Empty;
            DirectoryIdentityLease rootLease = null;
            DirectoryIdentityLease runLease = null;
            var leasesTransferred = false;
            try
            {
                var inspection = Inspect(plan.OutputRoot);
                if (!inspection.IsValid)
                    return new OutputReservation(inspection.Error, inspection.Message);
                if (!PathEquals(inspection.NormalizedPath, plan.OutputRoot))
                    return new OutputReservation(BuildAssistantError.UnsafeOutputPath, "The normalized output root no longer matches the plan.");

                var expectedRunDirectory = Path.Combine(plan.OutputRoot, plan.RunId);
                var expectedArtifactPath = Path.Combine(expectedRunDirectory, PlanFactory.GetArtifactName(plan.Target));
                if (!PathEquals(expectedRunDirectory, plan.RunDirectory) || !PathEquals(expectedArtifactPath, plan.ArtifactPath) || !IsContained(plan.OutputRoot, plan.RunDirectory) || !IsContained(plan.RunDirectory, plan.ArtifactPath))
                    return new OutputReservation(BuildAssistantError.UnsafeOutputPath, "The planned run or artifact path escaped its output boundary.");
                if (IsRunPathBusy(plan.OutputRoot, plan.RunId))
                    return new OutputReservation(BuildAssistantError.OutputAlreadyExists, "The planned run directory or reservation already exists.");

                reservationPath = GetReservationPath(plan.OutputRoot, plan.RunId);
                if (!fileSystem.DirectoryExists(plan.OutputRoot))
                    fileSystem.CreateDirectory(plan.OutputRoot);
                var createdRootInspection = Inspect(plan.OutputRoot);
                if (!createdRootInspection.IsValid || createdRootInspection.Mode != OutputRootMode.ExistingDirectory || !LocationPolicy.CanonicalEquals(inspection.CanonicalPath, createdRootInspection.CanonicalPath))
                    return new OutputReservation(BuildAssistantError.UnsafeOutputPath, "The output root changed while it was being reserved.");
                rootLease = fileSystem.AcquireDirectoryIdentityLease(plan.OutputRoot);
                if (!LocationPolicy.CanonicalEquals(createdRootInspection.CanonicalPath, rootLease.CanonicalPath))
                    return new OutputReservation(BuildAssistantError.UnsafeOutputPath, "The output root identity changed while its lease was acquired.");
                fileSystem.WriteAllTextFlushed(reservationPath, plan.RunId + Environment.NewLine, FileMode.CreateNew);
                if (fileSystem.DirectoryExists(plan.RunDirectory) || fileSystem.FileExists(plan.RunDirectory))
                {
                    TryDeleteReservation(reservationPath);
                    return new OutputReservation(BuildAssistantError.OutputAlreadyExists, "The planned run directory already exists.");
                }

                fileSystem.CreateDirectoryNew(plan.RunDirectory);
                runLease = fileSystem.AcquireDirectoryIdentityLease(plan.RunDirectory);
                if ((fileSystem.GetAttributes(plan.RunDirectory) & FileAttributes.ReparsePoint) != 0)
                {
                    TryDeleteReservation(reservationPath);
                    return new OutputReservation(BuildAssistantError.UnsafeOutputPath, "The reserved run directory is a reparse point.");
                }
                var finalRootInspection = Inspect(plan.OutputRoot);
                var canonicalRunDirectory = runLease.CanonicalPath;
                if (!finalRootInspection.IsValid || !LocationPolicy.CanonicalEquals(rootLease.CanonicalPath, finalRootInspection.CanonicalPath) || LocationPolicy.CanonicalEquals(rootLease.CanonicalPath, canonicalRunDirectory) || !LocationPolicy.CanonicalContains(rootLease.CanonicalPath, canonicalRunDirectory))
                {
                    TryDeleteReservation(reservationPath);
                    return new OutputReservation(BuildAssistantError.UnsafeOutputPath, "The reserved run directory changed or escaped its physical output boundary.");
                }
                var reservation = new OutputReservation(BuildAssistantError.None, string.Empty, fileSystem, reservationPath, rootLease, runLease);
                leasesTransferred = true;
                return reservation;
            }
            catch (CreateNewFileCollisionException exception)
            {
                return new OutputReservation(BuildAssistantError.OutputAlreadyExists, exception.Message);
            }
            catch (CreateNewDirectoryCollisionException exception)
            {
                TryDeleteReservation(reservationPath);
                return new OutputReservation(BuildAssistantError.OutputAlreadyExists, exception.Message);
            }
            catch (IOException exception)
            {
                TryDeleteReservation(reservationPath);
                return new OutputReservation(BuildAssistantError.OutputReservationFailed, exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                TryDeleteReservation(reservationPath);
                return new OutputReservation(BuildAssistantError.OutputReservationFailed, exception.Message);
            }
            catch (SecurityException exception)
            {
                TryDeleteReservation(reservationPath);
                return new OutputReservation(BuildAssistantError.OutputReservationFailed, exception.Message);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                return new OutputReservation(BuildAssistantError.UnsafeOutputPath, exception.Message);
            }
            finally
            {
                if (!leasesTransferred)
                {
                    runLease?.Dispose();
                    rootLease?.Dispose();
                }
            }
        }

        internal static bool IsContained(string boundary, string candidate)
        {
            if (string.IsNullOrEmpty(boundary) || string.IsNullOrEmpty(candidate))
                return false;
            var normalizedBoundary = Path.GetFullPath(boundary).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (PathEquals(normalizedBoundary, normalizedCandidate))
                return true;
            return normalizedCandidate.StartsWith(normalizedBoundary + Path.DirectorySeparatorChar, PathComparison);
        }

        private static StringComparison PathComparison => StringComparison.OrdinalIgnoreCase;
        private static bool PathEquals(string left, string right) => string.Equals(left, right, PathComparison);

        private void TryDeleteReservation(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                fileSystem.DeleteFile(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException || exception is ArgumentException || exception is NotSupportedException)
            {
            }
        }

        private static string GetReservationPath(string outputRoot, string runId) => Path.Combine(outputRoot, "." + runId + ".reserve");
    }
}
