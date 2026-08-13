using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace SaveSystem.Tests
{
    /// <summary>ファイル置換の環境差と中断時の保存データ保持を確かめる。</summary>
    public sealed class FileSaveStorageTests
    {
        private string _temporaryDirectory;

        /// <summary>各テスト専用の絶対パスを作る。</summary>
        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), "FileSaveStorageTests", Guid.NewGuid().ToString("N"));
        }

        /// <summary>テスト専用フォルダーだけを削除する。</summary>
        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrEmpty(_temporaryDirectory) || !Directory.Exists(_temporaryDirectory)) return;

            var testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FileSaveStorageTests"));
            var target = Path.GetFullPath(_temporaryDirectory);
            Assert.That(target.StartsWith(testRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Directory.Delete(target, true);
        }

        /// <summary>作業フォルダー依存の相対パスと、広すぎるファイルシステム直下を拒否する。</summary>
        [Test]
        public void Constructor_RejectsRelativeAndFilesystemRoot()
        {
            var fileSystemRoot = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()));

            Assert.Throws<ArgumentException>(() => new FileSaveStorage("relative-saves"));
            Assert.Throws<ArgumentException>(() => new FileSaveStorage(fileSystemRoot));
        }

        /// <summary>置換APIが使えない環境でも、成功時に直前の主データだけをバックアップへ残す。</summary>
        [Test]
        public void Write_PortableFallbackRotatesOneGenerationAndCleansArtifacts()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            File.WriteAllText(PathFor("slot.save"), "old-primary");
            File.WriteAllText(PathFor("slot.save.bak"), "older-backup");
            File.WriteAllText(PathFor("slot.save.restore"), "stale-restore");
            File.WriteAllText(PathFor("slot.save.damaged"), "stale-damaged");

            var operations = new FaultingFileOperations { ForceUnsupportedReplace = true };
            var storage = new FileSaveStorage(_temporaryDirectory, operations);

            storage.Write("slot", "new-primary");

            Assert.That(File.ReadAllText(PathFor("slot.save")), Is.EqualTo("new-primary"));
            Assert.That(File.ReadAllText(PathFor("slot.save.bak")), Is.EqualTo("old-primary"));
            Assert.That(File.Exists(PathFor("slot.save.tmp")), Is.False);
            Assert.That(File.Exists(PathFor("slot.save.restore")), Is.False);
            Assert.That(File.Exists(PathFor("slot.save.damaged")), Is.False);
        }

        /// <summary>移動失敗時は旧主データを主とバックアップの両方へ残し、一時ファイルだけを除く。</summary>
        [Test]
        public void Write_PortablePromotionFailureKeepsPreviousPrimaryAndCleansTemporary()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            File.WriteAllText(PathFor("slot.save"), "old-primary");
            File.WriteAllText(PathFor("slot.save.bak"), "older-backup");

            var operations = new FaultingFileOperations
            {
                ForceUnsupportedReplace = true,
                MoveFailure = (source, destination) => source.EndsWith(".save.tmp", StringComparison.Ordinal) && destination.EndsWith(".save", StringComparison.Ordinal)
                    ? new IOException("injected promotion failure")
                    : null,
            };
            var storage = new FileSaveStorage(_temporaryDirectory, operations);

            Assert.Throws<IOException>(() => storage.Write("slot", "new-primary"));

            Assert.That(File.ReadAllText(PathFor("slot.save")), Is.EqualTo("old-primary"));
            Assert.That(File.ReadAllText(PathFor("slot.save.bak")), Is.EqualTo("old-primary"));
            Assert.That(File.Exists(PathFor("slot.save.tmp")), Is.False);
        }

        /// <summary>復旧用ファイルの昇格に失敗しても、置換前の主データを元の場所へ戻す。</summary>
        [Test]
        public void RestoreBackup_PortablePromotionFailureRestoresPreviousPrimary()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            File.WriteAllText(PathFor("slot.save"), "current-primary");
            File.WriteAllText(PathFor("slot.save.bak"), "valid-backup");
            File.WriteAllText(PathFor("slot.save.tmp"), "stale-temporary");

            var operations = new FaultingFileOperations
            {
                ForceUnsupportedReplace = true,
                MoveFailure = (source, destination) => source.EndsWith(".save.restore", StringComparison.Ordinal) && destination.EndsWith(".save", StringComparison.Ordinal)
                    ? new IOException("injected restore failure")
                    : null,
            };
            var storage = new FileSaveStorage(_temporaryDirectory, operations);

            Assert.Throws<IOException>(() => storage.RestoreBackup("slot"));

            Assert.That(File.ReadAllText(PathFor("slot.save")), Is.EqualTo("current-primary"));
            Assert.That(File.ReadAllText(PathFor("slot.save.bak")), Is.EqualTo("valid-backup"));
            Assert.That(File.Exists(PathFor("slot.save.tmp")), Is.False);
            Assert.That(File.Exists(PathFor("slot.save.restore")), Is.False);
            Assert.That(File.Exists(PathFor("slot.save.damaged")), Is.False);
        }

        /// <summary>新規保存が完了した場合は、以前の中断で残った処理用ファイルを公開状態に残さない。</summary>
        [Test]
        public void Write_NewPrimaryRemovesInterruptedArtifacts()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            File.WriteAllText(PathFor("slot.save.tmp"), "stale-temporary");
            File.WriteAllText(PathFor("slot.save.restore"), "stale-restore");
            File.WriteAllText(PathFor("slot.save.damaged"), "stale-damaged");

            var storage = new FileSaveStorage(_temporaryDirectory);

            storage.Write("slot", "new-primary");

            Assert.That(File.ReadAllText(PathFor("slot.save")), Is.EqualTo("new-primary"));
            Assert.That(File.Exists(PathFor("slot.save.tmp")), Is.False);
            Assert.That(File.Exists(PathFor("slot.save.restore")), Is.False);
            Assert.That(File.Exists(PathFor("slot.save.damaged")), Is.False);
        }

        /// <summary>削除時は公開ファイルだけでなく、同じスロットの処理残骸も消す。</summary>
        [Test]
        public void Delete_RemovesPrimaryBackupAndOperationArtifacts()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            var names = new[] { "slot.save", "slot.save.bak", "slot.save.tmp", "slot.save.restore", "slot.save.damaged" };
            foreach (var name in names) File.WriteAllText(PathFor(name), name);

            var storage = new FileSaveStorage(_temporaryDirectory);

            Assert.That(storage.Delete("slot"), Is.True);
            foreach (var name in names) Assert.That(File.Exists(PathFor(name)), Is.False, name);
        }

        /// <summary>一覧には主とバックアップだけを含め、処理残骸や不正な名前を公開しない。</summary>
        [Test]
        public void ListSlots_IgnoresArtifactsAndInvalidNames()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            File.WriteAllText(PathFor("zeta.save"), "primary");
            File.WriteAllText(PathFor("alpha.save.bak"), "backup");
            File.WriteAllText(PathFor("ignored.save.tmp"), "temporary");
            File.WriteAllText(PathFor("ignored.save.restore"), "restore");
            File.WriteAllText(PathFor("ignored.save.damaged"), "damaged");
            File.WriteAllText(PathFor("invalid name.save"), "invalid");

            var storage = new FileSaveStorage(_temporaryDirectory);

            CollectionAssert.AreEqual(new[] { "alpha", "zeta" }, storage.ListSlots());
        }

        /// <summary>列挙不能を空一覧へ変換せず、上位層が失敗として扱えるよう例外を伝える。</summary>
        [Test]
        public void ListSlots_EnumerationFailureIsNotReportedAsEmpty()
        {
            Directory.CreateDirectory(_temporaryDirectory);
            var operations = new FaultingFileOperations { EnumerationFailure = new IOException("injected enumeration failure") };
            var storage = new FileSaveStorage(_temporaryDirectory, operations);

            Assert.Throws<IOException>(() => storage.ListSlots());
        }

        private string PathFor(string name) => Path.Combine(_temporaryDirectory, name);

        private sealed class FaultingFileOperations : IFileSaveStorageOperations
        {
            private readonly IFileSaveStorageOperations _inner = SystemFileSaveStorageOperations.Instance;

            public bool ForceUnsupportedReplace { get; set; }

            public Func<string, string, Exception> MoveFailure { get; set; }

            public Exception EnumerationFailure { get; set; }

            public bool FileExists(string path) => _inner.FileExists(path);

            public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

            public void CreateDirectory(string path) => _inner.CreateDirectory(path);

            public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
            {
                if (EnumerationFailure != null) throw EnumerationFailure;
                return _inner.EnumerateFiles(path, searchPattern);
            }

            public string ReadAllText(string path) => _inner.ReadAllText(path);

            public void WriteDurable(string path, string contents) => _inner.WriteDurable(path, contents);

            public void CopyDurable(string sourcePath, string destinationPath) => _inner.CopyDurable(sourcePath, destinationPath);

            public void ReplaceFile(string sourcePath, string destinationPath, string backupPath)
            {
                if (ForceUnsupportedReplace) throw new PlatformNotSupportedException("injected unsupported replace");
                _inner.ReplaceFile(sourcePath, destinationPath, backupPath);
            }

            public void MoveFile(string sourcePath, string destinationPath)
            {
                var exception = MoveFailure?.Invoke(sourcePath, destinationPath);
                if (exception != null) throw exception;
                _inner.MoveFile(sourcePath, destinationPath);
            }

            public void DeleteFile(string path) => _inner.DeleteFile(path);
        }
    }
}
