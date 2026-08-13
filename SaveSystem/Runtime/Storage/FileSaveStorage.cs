using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("SaveSystem.Tests")]

namespace SaveSystem
{
    /// <summary>
    /// ローカルファイルへ同期保存し、直前の主データを1世代だけバックアップとして残す。
    /// <see cref="File.Replace(string, string, string)"/> 対応環境では単一の置換操作を使い、
    /// 非対応環境では先に旧主データをバックアップへ耐久書き込みしてから切り替える。
    /// 後者は切り替え中の原子性を保証しないが、失敗時も旧主データを主データまたはバックアップに残す。
    /// 同じスロットへの並行操作には対応せず、すべての操作は呼び出したスレッドを完了まで待たせる。
    /// WebGL Player と tvOS Player は同期ファイル置換を保証できないため対応しない。
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private const string PrimaryExtension = ".save";
        private const string BackupExtension = ".save.bak";
        private const string TemporaryExtension = ".save.tmp";
        private const string RestoreExtension = ".save.restore";
        private const string DamagedExtension = ".save.damaged";

        private readonly IFileSaveStorageOperations _operations;

        /// <summary>絶対パスの専用保存フォルダーを指定して作る。</summary>
        /// <param name="rootDirectory">保存ファイルだけを置く、ファイルシステム直下ではない絶対パス。</param>
        /// <exception cref="ArgumentException">パスが空、相対パス、またはファイルシステム直下の場合。</exception>
        /// <exception cref="PlatformNotSupportedException">WebGL Player または tvOS Player で作成した場合。</exception>
        public FileSaveStorage(string rootDirectory)
            : this(rootDirectory, SystemFileSaveStorageOperations.Instance)
        {
        }

        internal FileSaveStorage(string rootDirectory, IFileSaveStorageOperations operations)
        {
#if (UNITY_WEBGL || UNITY_TVOS) && !UNITY_EDITOR
            throw new PlatformNotSupportedException("FileSaveStorage は WebGL Player と tvOS Player の同期ファイル保存に対応していません。");
#else
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            RootDirectory = NormalizeRootDirectory(rootDirectory);
#endif
        }

        /// <summary>保存先外への結合を防ぐため、末尾区切りを除いて正規化した絶対パス。</summary>
        public string RootDirectory { get; }

        /// <inheritdoc/>
        public bool TryRead(string slot, out string contents) => TryReadPath(PathFor(slot, PrimaryExtension), out contents);

        /// <inheritdoc/>
        public bool TryReadBackup(string slot, out string contents) => TryReadPath(PathFor(slot, BackupExtension), out contents);

        /// <inheritdoc/>
        public void Write(string slot, string contents)
        {
            ValidateSlot(slot);
            if (contents == null) throw new ArgumentNullException(nameof(contents));

            var primaryPath = PathFor(slot, PrimaryExtension);
            var backupPath = PathFor(slot, BackupExtension);
            var temporaryPath = PathFor(slot, TemporaryExtension);
            var restorePath = PathFor(slot, RestoreExtension);
            var damagedPath = PathFor(slot, DamagedExtension);

            _operations.CreateDirectory(RootDirectory);
            PrepareMutation(primaryPath, temporaryPath, restorePath, damagedPath);

            try
            {
                _operations.WriteDurable(temporaryPath, contents);

                if (!_operations.FileExists(primaryPath))
                {
                    _operations.MoveFile(temporaryPath, primaryPath);
                    return;
                }

                ReplaceWithBackup(temporaryPath, primaryPath, backupPath);
            }
            finally
            {
                CleanupAfterMutation(primaryPath, temporaryPath, restorePath, damagedPath);
            }
        }

        /// <inheritdoc/>
        public bool RestoreBackup(string slot)
        {
            var backupPath = PathFor(slot, BackupExtension);
            if (!_operations.FileExists(backupPath)) return false;

            var primaryPath = PathFor(slot, PrimaryExtension);
            var temporaryPath = PathFor(slot, TemporaryExtension);
            var restorePath = PathFor(slot, RestoreExtension);
            var damagedPath = PathFor(slot, DamagedExtension);

            _operations.CreateDirectory(RootDirectory);
            DeleteIfPresent(temporaryPath);
            DeleteIfPresent(restorePath);
            if (_operations.FileExists(primaryPath)) DeleteIfPresent(damagedPath);

            try
            {
                _operations.CopyDurable(backupPath, restorePath);

                if (_operations.FileExists(primaryPath)) ReplaceWithoutBackup(restorePath, primaryPath, damagedPath);
                else _operations.MoveFile(restorePath, primaryPath);

                return true;
            }
            finally
            {
                CleanupAfterMutation(primaryPath, temporaryPath, restorePath, damagedPath);
            }
        }

        /// <inheritdoc/>
        public bool Delete(string slot)
        {
            ValidateSlot(slot);

            var deleted = false;
            deleted |= DeleteIfPresent(PathFor(slot, PrimaryExtension));
            deleted |= DeleteIfPresent(PathFor(slot, BackupExtension));
            deleted |= DeleteIfPresent(PathFor(slot, TemporaryExtension));
            deleted |= DeleteIfPresent(PathFor(slot, RestoreExtension));
            deleted |= DeleteIfPresent(PathFor(slot, DamagedExtension));
            return deleted;
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> ListSlots()
        {
            if (!_operations.DirectoryExists(RootDirectory)) return Array.Empty<string>();

            var slots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in _operations.EnumerateFiles(RootDirectory, "*" + PrimaryExtension))
            {
                AddSlot(Path.GetFileName(path), PrimaryExtension, slots);
            }

            foreach (var path in _operations.EnumerateFiles(RootDirectory, "*" + BackupExtension))
            {
                AddSlot(Path.GetFileName(path), BackupExtension, slots);
            }

            return slots.OrderBy(slot => slot, StringComparer.Ordinal).ToArray();
        }

        private static string NormalizeRootDirectory(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("保存先フォルダーが空です。", nameof(rootDirectory));
            if (!Path.IsPathFullyQualified(rootDirectory)) throw new ArgumentException("保存先フォルダーには絶対パスを指定してください。", nameof(rootDirectory));

            var fullPath = Path.GetFullPath(rootDirectory);
            var pathRoot = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(pathRoot)) throw new ArgumentException("保存先フォルダーのファイルシステム直下を確認できません。", nameof(rootDirectory));

            var normalizedPath = TrimEndingDirectorySeparators(fullPath);
            var normalizedRoot = TrimEndingDirectorySeparators(pathRoot);
            if (string.Equals(normalizedPath, normalizedRoot, PathComparison))
            {
                throw new ArgumentException("ファイルシステム直下を保存先フォルダーには指定できません。", nameof(rootDirectory));
            }

            return normalizedPath;
        }

        private bool TryReadPath(string path, out string contents)
        {
            if (!_operations.FileExists(path))
            {
                contents = null;
                return false;
            }

            contents = _operations.ReadAllText(path);
            return true;
        }

        private void ReplaceWithBackup(string sourcePath, string destinationPath, string backupPath)
        {
            try
            {
                _operations.ReplaceFile(sourcePath, destinationPath, backupPath);
            }
            catch (PlatformNotSupportedException)
            {
                PortableReplaceWithBackup(sourcePath, destinationPath, backupPath);
            }
        }

        private void PortableReplaceWithBackup(string sourcePath, string destinationPath, string backupPath)
        {
            _operations.CopyDurable(destinationPath, backupPath);

            try
            {
                _operations.DeleteFile(destinationPath);
                _operations.MoveFile(sourcePath, destinationPath);
            }
            catch
            {
                RestorePrimaryFromBackup(destinationPath, backupPath);
                throw;
            }
        }

        private void ReplaceWithoutBackup(string sourcePath, string destinationPath, string damagedPath)
        {
            try
            {
                _operations.ReplaceFile(sourcePath, destinationPath, null);
            }
            catch (PlatformNotSupportedException)
            {
                PortableReplaceWithoutBackup(sourcePath, destinationPath, damagedPath);
            }
        }

        private void PortableReplaceWithoutBackup(string sourcePath, string destinationPath, string damagedPath)
        {
            DeleteIfPresent(damagedPath);
            _operations.MoveFile(destinationPath, damagedPath);

            try
            {
                _operations.MoveFile(sourcePath, destinationPath);
            }
            catch
            {
                RestoreMovedPrimary(destinationPath, damagedPath);
                throw;
            }
        }

        private void RestorePrimaryFromBackup(string primaryPath, string backupPath)
        {
            if (_operations.FileExists(primaryPath) || !_operations.FileExists(backupPath)) return;

            try
            {
                _operations.CopyDurable(backupPath, primaryPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        private void RestoreMovedPrimary(string primaryPath, string damagedPath)
        {
            if (_operations.FileExists(primaryPath) || !_operations.FileExists(damagedPath)) return;

            try
            {
                _operations.MoveFile(damagedPath, primaryPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        private void PrepareMutation(string primaryPath, string temporaryPath, string restorePath, string damagedPath)
        {
            DeleteIfPresent(temporaryPath);
            if (!_operations.FileExists(primaryPath)) return;

            DeleteIfPresent(restorePath);
            DeleteIfPresent(damagedPath);
        }

        private void CleanupAfterMutation(string primaryPath, string temporaryPath, string restorePath, string damagedPath)
        {
            TryDeleteArtifact(temporaryPath);
            TryDeleteArtifact(restorePath);
            if (_operations.FileExists(primaryPath)) TryDeleteArtifact(damagedPath);
        }

        private bool DeleteIfPresent(string path)
        {
            if (!_operations.FileExists(path)) return false;
            _operations.DeleteFile(path);
            return true;
        }

        private void TryDeleteArtifact(string path)
        {
            try
            {
                DeleteIfPresent(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        private static void AddSlot(string fileName, string extension, ISet<string> slots)
        {
            if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) return;

            var slot = fileName.Substring(0, fileName.Length - extension.Length);
            if (SaveSlot.IsValid(slot)) slots.Add(slot);
        }

        private string PathFor(string slot, string extension)
        {
            ValidateSlot(slot);

            var path = Path.GetFullPath(Path.Combine(RootDirectory, slot + extension));
            var rootPrefix = RootDirectory + Path.DirectorySeparatorChar;
            if (!path.StartsWith(rootPrefix, PathComparison)) throw new InvalidOperationException("保存先フォルダー外のパスは使用できません。");
            return path;
        }

        private static string TrimEndingDirectorySeparators(string path) => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static StringComparison PathComparison => Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private static void ValidateSlot(string slot)
        {
            if (!SaveSlot.IsValid(slot)) throw new ArgumentException("スロット名には文字、数字、ハイフン、アンダースコアを64文字まで使用できます。", nameof(slot));
        }
    }

    internal interface IFileSaveStorageOperations
    {
        bool FileExists(string path);

        bool DirectoryExists(string path);

        void CreateDirectory(string path);

        IEnumerable<string> EnumerateFiles(string path, string searchPattern);

        string ReadAllText(string path);

        void WriteDurable(string path, string contents);

        void CopyDurable(string sourcePath, string destinationPath);

        void ReplaceFile(string sourcePath, string destinationPath, string backupPath);

        void MoveFile(string sourcePath, string destinationPath);

        void DeleteFile(string path);
    }

    internal sealed class SystemFileSaveStorageOperations : IFileSaveStorageOperations
    {
        internal static readonly SystemFileSaveStorageOperations Instance = new SystemFileSaveStorageOperations();

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private SystemFileSaveStorageOperations()
        {
        }

        public bool FileExists(string path) => File.Exists(path);

        public bool DirectoryExists(string path) => Directory.Exists(path);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern) => Directory.EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly);

        public string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);

        public void WriteDurable(string path, string contents)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, Utf8WithoutBom);
            writer.Write(contents);
            writer.Flush();
            stream.Flush(true);
        }

        public void CopyDurable(string sourcePath, string destinationPath)
        {
            using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            source.CopyTo(destination);
            destination.Flush(true);
        }

        public void ReplaceFile(string sourcePath, string destinationPath, string backupPath) => File.Replace(sourcePath, destinationPath, backupPath);

        public void MoveFile(string sourcePath, string destinationPath) => File.Move(sourcePath, destinationPath);

        public void DeleteFile(string path) => File.Delete(path);
    }
}
