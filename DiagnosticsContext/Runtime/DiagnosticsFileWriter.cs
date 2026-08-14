using System;
using System.Globalization;
using System.IO;
using System.Security;

namespace DiagnosticsContext
{
    /// <summary>専用directory内だけで一時fileを一意な最終reportへ移動する。</summary>
    internal static class DiagnosticsFileWriter
    {
        /// <summary>UTF-8 byte列を一意な一時fileへflushし、未使用の最終名へ移動する。</summary>
        /// <param name="reportDirectory">persistentDataPath配下へ確定済みの専用directory。</param>
        /// <param name="createdUtc">report snapshotのUTC作成時刻。</param>
        /// <param name="uniqueId">利用者入力を含まない一意値。</param>
        /// <param name="bytes">上限確認済みのUTF-8 JSON。</param>
        /// <returns>成功時は最終pathとbyte数、失敗時は分類済みerror。</returns>
        internal static DiagnosticsWriteResult Write(string reportDirectory, DateTime createdUtc, Guid uniqueId, byte[] bytes)
        {
            var preparationError = TryPrepareDirectory(reportDirectory, out var normalizedDirectory);
            if (preparationError != DiagnosticsError.None) return DiagnosticsWriteResult.Failure(preparationError);

            var timestamp = createdUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfffffff'Z'", CultureInfo.InvariantCulture);
            var finalName = string.Format(CultureInfo.InvariantCulture, "diagnostics-{0}-{1}.json", timestamp, uniqueId.ToString("N"));
            var temporaryName = string.Format(CultureInfo.InvariantCulture, ".{0}.{1}.tmp", finalName, Guid.NewGuid().ToString("N"));
            string finalPath;
            string temporaryPath;
            try
            {
                finalPath = GetContainedPath(normalizedDirectory, finalName);
                temporaryPath = GetContainedPath(normalizedDirectory, temporaryName);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return DiagnosticsWriteResult.Failure(DiagnosticsError.StorageUnavailable);
            }

            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                File.Move(temporaryPath, finalPath);
                return DiagnosticsWriteResult.Success(finalPath, bytes.Length);
            }
            catch (Exception exception) when (IsWriteException(exception))
            {
                TryDeleteOwnTemporaryFile(temporaryPath);
                return DiagnosticsWriteResult.Failure(DiagnosticsError.WriteFailed);
            }
        }

        /// <summary>専用directoryを作成または検証し、reparse pointでない絶対pathを返す。</summary>
        /// <param name="reportDirectory">作成前でもよい専用directory path。</param>
        /// <param name="normalizedDirectory">成功時に利用できる正規化済み絶対path。</param>
        /// <returns>利用できる場合はNone、それ以外はStorageUnavailable。</returns>
        internal static DiagnosticsError TryPrepareDirectory(string reportDirectory, out string normalizedDirectory)
        {
            normalizedDirectory = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(reportDirectory)) return DiagnosticsError.StorageUnavailable;
                var candidate = Path.GetFullPath(reportDirectory);
                Directory.CreateDirectory(candidate);
                var directoryInfo = new DirectoryInfo(candidate);
                if ((directoryInfo.Attributes & FileAttributes.ReparsePoint) != 0) return DiagnosticsError.StorageUnavailable;
                normalizedDirectory = directoryInfo.FullName;
                return DiagnosticsError.None;
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                normalizedDirectory = string.Empty;
                return DiagnosticsError.StorageUnavailable;
            }
        }

        /// <summary>指定file名が専用directory直下から逸脱しない絶対pathを返す。</summary>
        private static string GetContainedPath(string normalizedDirectory, string fileName)
        {
            var candidate = Path.GetFullPath(Path.Combine(normalizedDirectory, fileName));
            var rootWithSeparator = normalizedDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)) throw new IOException("Report path escaped the diagnostics directory.");
            if (!string.Equals(Path.GetDirectoryName(candidate), normalizedDirectory, StringComparison.OrdinalIgnoreCase)) throw new IOException("Report path must remain in the diagnostics directory.");
            return candidate;
        }

        /// <summary>今回だけの一時fileを失敗後に可能な範囲で除去する。</summary>
        private static void TryDeleteOwnTemporaryFile(string temporaryPath)
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch (Exception exception) when (IsWriteException(exception))
            {
            }
        }

        /// <summary>保存先準備の失敗として扱える例外ならtrueを返す。</summary>
        private static bool IsStorageException(Exception exception)
        {
            return exception is ArgumentException ||
                   exception is NotSupportedException ||
                   exception is PathTooLongException ||
                   exception is DirectoryNotFoundException ||
                   exception is UnauthorizedAccessException ||
                   exception is SecurityException ||
                   exception is IOException;
        }

        /// <summary>report書出しの失敗として扱える例外ならtrueを返す。</summary>
        private static bool IsWriteException(Exception exception)
        {
            return IsStorageException(exception) || exception is ObjectDisposedException;
        }
    }
}
