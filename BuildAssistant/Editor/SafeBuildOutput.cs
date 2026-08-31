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
        internal bool IsReserved => !disposed && Error == BuildAssistantError.None;

        internal BuildAssistantError Revalidate(BuildAssistantPlan plan, out string message)
        {
            if (!IsReserved || plan == null || fileSystem == null || rootLease == null || runLease == null)
            {
                message = "出力先の予約を利用できません。";
                return BuildAssistantError.OutputReservationFailed;
            }

            try
            {
                if (!fileSystem.FileExists(reservationPath) || !StringComparer.Ordinal.Equals(fileSystem.ReadAllText(reservationPath), plan.RunId + Environment.NewLine))
                {
                    message = "ビルド開始前に出力先の予約情報が変更されました。";
                    return BuildAssistantError.OutputReservationFailed;
                }
                if ((fileSystem.GetAttributes(plan.OutputRoot) & FileAttributes.ReparsePoint) != 0 || (fileSystem.GetAttributes(plan.RunDirectory) & FileAttributes.ReparsePoint) != 0)
                {
                    message = "出力先または予約済み実行フォルダーが再解析点へ変更されました。";
                    return BuildAssistantError.UnsafeOutputPath;
                }
                var canonicalRoot = fileSystem.GetCanonicalDirectoryPath(plan.OutputRoot);
                var canonicalRun = fileSystem.GetCanonicalDirectoryPath(plan.RunDirectory);
                if (!LocationPolicy.CanonicalEquals(rootLease.CanonicalPath, canonicalRoot) || !LocationPolicy.CanonicalEquals(runLease.CanonicalPath, canonicalRun) || !LocationPolicy.CanonicalContains(canonicalRoot, canonicalRun))
                {
                    message = "ビルド開始前に保持中の出力先の物理識別子が変更されました。";
                    return BuildAssistantError.UnsafeOutputPath;
                }
                if (fileSystem.FileExists(plan.ArtifactPath) || fileSystem.DirectoryExists(plan.ArtifactPath) || !fileSystem.IsDirectoryEmpty(plan.RunDirectory))
                {
                    message = "ビルド開始前に新規実行フォルダーへ内容が追加されました。";
                    return BuildAssistantError.OutputAlreadyExists;
                }

                message = string.Empty;
                return BuildAssistantError.None;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException || exception is ArgumentException || exception is NotSupportedException)
            {
                message = "保持中の出力先の物理識別子を再確認できませんでした。";
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
                    return new OutputReservation(BuildAssistantError.UnsafeOutputPath, "正規化後の出力先が計画と一致しません。覚えのない変更がないか確認してください。");

                var expectedRunDirectory = Path.Combine(plan.OutputRoot, plan.RunId);
                var expectedArtifactPath = Path.Combine(expectedRunDirectory, PlanFactory.GetArtifactName(plan.Target));
                if (!PathEquals(expectedRunDirectory, plan.RunDirectory) || !PathEquals(expectedArtifactPath, plan.ArtifactPath) || !IsContained(plan.OutputRoot, plan.RunDirectory) || !IsContained(plan.RunDirectory, plan.ArtifactPath))
                    return new OutputReservation(BuildAssistantError.UnsafeOutputPath, "実行フォルダーまたは成果物の経路が出力先の範囲外です。");
                if (IsRunPathBusy(plan.OutputRoot, plan.RunId))
                    return new OutputReservation(BuildAssistantError.OutputAlreadyExists, "今回の実行フォルダーまたは予約が既に存在します。");

                reservationPath = GetReservationPath(plan.OutputRoot, plan.RunId);
                if (!fileSystem.DirectoryExists(plan.OutputRoot))
                    fileSystem.CreateDirectory(plan.OutputRoot);
                var createdRootInspection = Inspect(plan.OutputRoot);
                if (!createdRootInspection.IsValid || createdRootInspection.Mode != OutputRootMode.ExistingDirectory || !LocationPolicy.CanonicalEquals(inspection.CanonicalPath, createdRootInspection.CanonicalPath))
                    return new OutputReservation(BuildAssistantError.UnsafeOutputPath, "出力先の予約中に出力先が変更されました。");
                rootLease = fileSystem.AcquireDirectoryIdentityLease(plan.OutputRoot);
                if (!LocationPolicy.CanonicalEquals(createdRootInspection.CanonicalPath, rootLease.CanonicalPath))
                    return new OutputReservation(BuildAssistantError.UnsafeOutputPath, "出力先を保持する間に物理識別子が変更されました。");
                fileSystem.WriteAllTextFlushed(reservationPath, plan.RunId + Environment.NewLine, FileMode.CreateNew);
                if (fileSystem.DirectoryExists(plan.RunDirectory) || fileSystem.FileExists(plan.RunDirectory))
                {
                    TryDeleteReservation(reservationPath);
                    return new OutputReservation(BuildAssistantError.OutputAlreadyExists, "今回の実行フォルダーが既に存在します。");
                }

                fileSystem.CreateDirectoryNew(plan.RunDirectory);
                runLease = fileSystem.AcquireDirectoryIdentityLease(plan.RunDirectory);
                if ((fileSystem.GetAttributes(plan.RunDirectory) & FileAttributes.ReparsePoint) != 0)
                {
                    TryDeleteReservation(reservationPath);
                    return new OutputReservation(BuildAssistantError.UnsafeOutputPath, "予約済み実行フォルダーが再解析点です。");
                }
                var finalRootInspection = Inspect(plan.OutputRoot);
                var canonicalRunDirectory = runLease.CanonicalPath;
                if (!finalRootInspection.IsValid || !LocationPolicy.CanonicalEquals(rootLease.CanonicalPath, finalRootInspection.CanonicalPath) || LocationPolicy.CanonicalEquals(rootLease.CanonicalPath, canonicalRunDirectory) || !LocationPolicy.CanonicalContains(rootLease.CanonicalPath, canonicalRunDirectory))
                {
                    TryDeleteReservation(reservationPath);
                    return new OutputReservation(BuildAssistantError.UnsafeOutputPath, "予約済み実行フォルダーが変更されたか、物理的な出力範囲外へ移動しました。");
                }
                var reservation = new OutputReservation(BuildAssistantError.None, string.Empty, fileSystem, reservationPath, rootLease, runLease);
                leasesTransferred = true;
                return reservation;
            }
            catch (CreateNewFileCollisionException)
            {
                return new OutputReservation(BuildAssistantError.OutputAlreadyExists, "出力先の予約が別の処理によって先に作成されました。");
            }
            catch (CreateNewDirectoryCollisionException)
            {
                TryDeleteReservation(reservationPath);
                return new OutputReservation(BuildAssistantError.OutputAlreadyExists, "実行フォルダーが別の処理によって先に作成されました。");
            }
            catch (IOException)
            {
                TryDeleteReservation(reservationPath);
                return new OutputReservation(BuildAssistantError.OutputReservationFailed, "出力先を予約できませんでした。アクセス権と空き容量を確認してください。");
            }
            catch (UnauthorizedAccessException)
            {
                TryDeleteReservation(reservationPath);
                return new OutputReservation(BuildAssistantError.OutputReservationFailed, "出力先を予約する権限がありません。");
            }
            catch (SecurityException)
            {
                TryDeleteReservation(reservationPath);
                return new OutputReservation(BuildAssistantError.OutputReservationFailed, "安全制限により出力先を予約できませんでした。");
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                return new OutputReservation(BuildAssistantError.UnsafeOutputPath, "出力先の経路を安全に解決できませんでした。");
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

        private static StringComparison PathComparison => BuildAssistantFileSystem.GetPathComparison(Path.DirectorySeparatorChar);
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
