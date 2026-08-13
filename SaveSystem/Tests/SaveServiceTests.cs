using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace SaveSystem.Tests
{
    /// <summary>実ファイルを使い、保存から復旧までの公開動作を確かめる。</summary>
    public sealed class SaveServiceTests
    {
        private string _temporaryDirectory;
        private SaveService _service;

        /// <summary>各テスト専用の保存先を作る。</summary>
        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), "SaveSystemTests", Guid.NewGuid().ToString("N"));
            var savedAt = new DateTime(2026, 8, 11, 12, 34, 56, DateTimeKind.Utc);
            _service = new SaveService(new FileSaveStorage(_temporaryDirectory), utcNow: () => savedAt);
        }

        /// <summary>テスト専用フォルダーだけを片付ける。</summary>
        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrEmpty(_temporaryDirectory) || !Directory.Exists(_temporaryDirectory)) return;

            var testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SaveSystemTests"));
            var target = Path.GetFullPath(_temporaryDirectory);
            Assert.That(target.StartsWith(testRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Directory.Delete(target, true);
        }

        /// <summary>型付きデータと識別情報が往復することを確かめる。</summary>
        [Test]
        public void SaveAndLoad_RoundTripsTypedDataAndMetadata()
        {
            var source = new TestSaveData
            {
                Name = "プレイヤー",
                Score = 4200,
                Items = new List<int> { 3, 5, 8 },
            };

            var saved = _service.Save("slot_1", source, "2");
            var loaded = _service.Load<TestSaveData>("slot_1", "2");

            Assert.That(saved.IsSuccess, Is.True, saved.Message);
            Assert.That(saved.Metadata.DataVersion, Is.EqualTo("2"));
            Assert.That(loaded.IsSuccess, Is.True, loaded.Message);
            Assert.That(loaded.Value.Name, Is.EqualTo("プレイヤー"));
            Assert.That(loaded.Value.Score, Is.EqualTo(4200));
            CollectionAssert.AreEqual(new[] { 3, 5, 8 }, loaded.Value.Items);
            Assert.That(loaded.Metadata.SavedAtUtc, Is.EqualTo(new DateTime(2026, 8, 11, 12, 34, 56, DateTimeKind.Utc)));
            Assert.That(loaded.Metadata.RecoveredFromBackup, Is.False);
        }

        /// <summary>主ファイル破損時に直前の保存へ戻り、その内容を主ファイルへ書き戻すことを確かめる。</summary>
        [Test]
        public void Load_CorruptPrimaryRestoresPreviousBackup()
        {
            Assert.That(_service.Save("auto", DataWithScore(10)).IsSuccess, Is.True);
            Assert.That(_service.Save("auto", DataWithScore(20)).IsSuccess, Is.True);

            File.WriteAllText(Path.Combine(_temporaryDirectory, "auto.save"), "{ broken", Encoding.UTF8);

            var recovered = _service.Load<TestSaveData>("auto");
            var loadedAgain = _service.Load<TestSaveData>("auto");

            Assert.That(recovered.IsSuccess, Is.True, recovered.Message);
            Assert.That(recovered.Value.Score, Is.EqualTo(10));
            Assert.That(recovered.Metadata.RecoveredFromBackup, Is.True);
            Assert.That(loadedAgain.IsSuccess, Is.True, loadedAgain.Message);
            Assert.That(loadedAgain.Value.Score, Is.EqualTo(10));
            Assert.That(loadedAgain.Metadata.RecoveredFromBackup, Is.False);
        }

        /// <summary>主ファイルが消失した場合もバックアップを主ファイルへ戻すことを確かめる。</summary>
        [Test]
        public void Load_MissingPrimaryRestoresPreviousBackup()
        {
            Assert.That(_service.Save("auto", DataWithScore(10)).IsSuccess, Is.True);
            Assert.That(_service.Save("auto", DataWithScore(20)).IsSuccess, Is.True);
            File.Delete(Path.Combine(_temporaryDirectory, "auto.save"));

            var recovered = _service.Load<TestSaveData>("auto");
            var loadedAgain = _service.Load<TestSaveData>("auto");

            Assert.That(recovered.IsSuccess, Is.True, recovered.Message);
            Assert.That(recovered.Value.Score, Is.EqualTo(10));
            Assert.That(recovered.Metadata.RecoveredFromBackup, Is.True);
            Assert.That(loadedAgain.IsSuccess, Is.True, loadedAgain.Message);
            Assert.That(loadedAgain.Value.Score, Is.EqualTo(10));
            Assert.That(loadedAgain.Metadata.RecoveredFromBackup, Is.False);
        }

        /// <summary>3回目の保存でもバックアップが直前の主データへ更新されることを確かめる。</summary>
        [Test]
        public void Save_UpdatesExistingBackupOnEveryReplacement()
        {
            Assert.That(_service.Save("manual", DataWithScore(1)).IsSuccess, Is.True);
            Assert.That(_service.Save("manual", DataWithScore(2)).IsSuccess, Is.True);
            Assert.That(_service.Save("manual", DataWithScore(3)).IsSuccess, Is.True);

            File.WriteAllText(Path.Combine(_temporaryDirectory, "manual.save"), "corrupt", Encoding.UTF8);
            var recovered = _service.Load<TestSaveData>("manual");

            Assert.That(recovered.IsSuccess, Is.True, recovered.Message);
            Assert.That(recovered.Value.Score, Is.EqualTo(2));
        }

        /// <summary>JSONだけを書き換えてもチェックサムで検出できることを確かめる。</summary>
        [Test]
        public void Load_TamperedPayloadWithoutBackupReturnsCorruptData()
        {
            Assert.That(_service.Save("manual", DataWithScore(42)).IsSuccess, Is.True);

            var path = Path.Combine(_temporaryDirectory, "manual.save");
            var tampered = File.ReadAllText(path, Encoding.UTF8).Replace("42", "43");
            File.WriteAllText(path, tampered, Encoding.UTF8);

            var result = _service.Load<TestSaveData>("manual");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.CorruptData));
        }

        /// <summary>型識別を書き換えた場合もチェックサムで破損として検出することを確かめる。</summary>
        [Test]
        public void Load_TamperedTypeIdentifierReturnsCorruptData()
        {
            Assert.That(_service.Save("typed", DataWithScore(42)).IsSuccess, Is.True);

            var path = Path.Combine(_temporaryDirectory, "typed.save");
            var original = File.ReadAllText(path, Encoding.UTF8);
            StringAssert.Contains("SaveSystem.Tests:SaveSystem.Tests.SaveServiceTests+TestSaveData", original);
            StringAssert.DoesNotContain("Version=", original);
            var tampered = original.Replace("+TestSaveData", "+AlternativeSaveData");
            Assert.That(tampered, Is.Not.EqualTo(original));
            File.WriteAllText(path, tampered, Encoding.UTF8);

            var result = _service.Load<TestSaveData>("typed");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.CorruptData));
        }

        /// <summary>期待するゲームデータ版が違う場合に値を渡さないことを確かめる。</summary>
        [Test]
        public void Load_UnexpectedDataVersionReturnsVersionMismatch()
        {
            Assert.That(_service.Save("versioned", DataWithScore(7), "3").IsSuccess, Is.True);

            var result = _service.Load<TestSaveData>("versioned", "4");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.VersionMismatch));
        }

        /// <summary>主データの版違いでは古いバックアップへ巻き戻さないことを確かめる。</summary>
        [Test]
        public void Load_PrimaryVersionMismatchDoesNotReadOrRestoreBackup()
        {
            Assert.That(_service.Save("versioned", DataWithScore(10), "1").IsSuccess, Is.True);
            Assert.That(_service.Save("versioned", DataWithScore(20), "2").IsSuccess, Is.True);

            var primaryPath = Path.Combine(_temporaryDirectory, "versioned.save");
            var primaryBefore = File.ReadAllText(primaryPath, Encoding.UTF8);
            var storage = new TrackingStorage(new FileSaveStorage(_temporaryDirectory));
            var service = new SaveService(storage);

            var result = service.Load<TestSaveData>("versioned", "1");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.VersionMismatch));
            Assert.That(storage.BackupReadCount, Is.Zero);
            Assert.That(storage.RestoreBackupCount, Is.Zero);
            Assert.That(File.ReadAllText(primaryPath, Encoding.UTF8), Is.EqualTo(primaryBefore));
        }

        /// <summary>正の未知形式版では破損復旧を行わず、主データとバックアップを変更しないことを確かめる。</summary>
        [Test]
        public void Load_PositiveUnknownFormatVersionDoesNotReadOrRestoreBackup()
        {
            Assert.That(_service.Save("format", DataWithScore(10)).IsSuccess, Is.True);
            Assert.That(_service.Save("format", DataWithScore(20)).IsSuccess, Is.True);

            var primaryPath = Path.Combine(_temporaryDirectory, "format.save");
            var backupPath = Path.Combine(_temporaryDirectory, "format.save.bak");
            var unsupportedFormat = File.ReadAllText(primaryPath, Encoding.UTF8).Replace("\"FormatVersion\":2", "\"FormatVersion\":1");
            Assert.That(unsupportedFormat, Is.Not.EqualTo(File.ReadAllText(primaryPath, Encoding.UTF8)));
            File.WriteAllText(primaryPath, unsupportedFormat, Encoding.UTF8);
            var primaryBefore = File.ReadAllText(primaryPath, Encoding.UTF8);
            var backupBefore = File.ReadAllText(backupPath, Encoding.UTF8);
            var storage = new TrackingStorage(new FileSaveStorage(_temporaryDirectory));
            var service = new SaveService(storage);

            var result = service.Load<TestSaveData>("format");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.FormatVersionMismatch));
            Assert.That(storage.BackupReadCount, Is.Zero);
            Assert.That(storage.RestoreBackupCount, Is.Zero);
            Assert.That(File.ReadAllText(primaryPath, Encoding.UTF8), Is.EqualTo(primaryBefore));
            Assert.That(File.ReadAllText(backupPath, Encoding.UTF8), Is.EqualTo(backupBefore));
        }

        /// <summary>欠落または不正な形式版を破損として扱い、有効なバックアップから復旧することを確かめる。</summary>
        /// <param name="corruptPrimary">主データへ書き込む不正な保存形式。</param>
        [TestCase("{}")]
        [TestCase("{\"FormatVersion\":0}")]
        [TestCase("{\"FormatVersion\":-1}")]
        public void Load_MissingOrInvalidFormatVersionRestoresValidBackup(string corruptPrimary)
        {
            Assert.That(_service.Save("format", DataWithScore(10)).IsSuccess, Is.True);
            Assert.That(_service.Save("format", DataWithScore(20)).IsSuccess, Is.True);

            var primaryPath = Path.Combine(_temporaryDirectory, "format.save");
            File.WriteAllText(primaryPath, corruptPrimary, Encoding.UTF8);

            var recovered = _service.Load<TestSaveData>("format");
            var loadedAgain = _service.Load<TestSaveData>("format");

            Assert.That(recovered.IsSuccess, Is.True, recovered.Message);
            Assert.That(recovered.Value.Score, Is.EqualTo(10));
            Assert.That(recovered.Metadata.RecoveredFromBackup, Is.True);
            Assert.That(loadedAgain.IsSuccess, Is.True, loadedAgain.Message);
            Assert.That(loadedAgain.Value.Score, Is.EqualTo(10));
            Assert.That(loadedAgain.Metadata.RecoveredFromBackup, Is.False);
        }

        /// <summary>要求型が違う場合に、型が一致する古いバックアップへ巻き戻さないことを確かめる。</summary>
        [Test]
        public void Load_PrimaryTypeMismatchDoesNotReadOrRestoreBackup()
        {
            Assert.That(_service.Save("typed", new AlternativeSaveData { Label = "old" }).IsSuccess, Is.True);
            Assert.That(_service.Save("typed", DataWithScore(20)).IsSuccess, Is.True);

            var primaryPath = Path.Combine(_temporaryDirectory, "typed.save");
            var primaryBefore = File.ReadAllText(primaryPath, Encoding.UTF8);
            var storage = new TrackingStorage(new FileSaveStorage(_temporaryDirectory));
            var service = new SaveService(storage);

            var result = service.Load<AlternativeSaveData>("typed");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.TypeMismatch));
            Assert.That(storage.BackupReadCount, Is.Zero);
            Assert.That(storage.RestoreBackupCount, Is.Zero);
            Assert.That(File.ReadAllText(primaryPath, Encoding.UTF8), Is.EqualTo(primaryBefore));
        }

        /// <summary>主データの変換失敗ではバックアップを読まず、例外を結果へ変えることを確かめる。</summary>
        [Test]
        public void Load_PrimarySerializationExceptionDoesNotReadBackup()
        {
            Assert.That(_service.Save("serialized", DataWithScore(10)).IsSuccess, Is.True);
            Assert.That(_service.Save("serialized", DataWithScore(20)).IsSuccess, Is.True);

            var storage = new TrackingStorage(new FileSaveStorage(_temporaryDirectory));
            var service = new SaveService(storage, new ThrowingSerializer());

            var result = service.Load<TestSaveData>("serialized");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.SerializationFailed));
            Assert.That(storage.BackupReadCount, Is.Zero);
            Assert.That(storage.RestoreBackupCount, Is.Zero);
        }

        /// <summary>主データの保存先例外ではバックアップを読まず、例外を結果へ変えることを確かめる。</summary>
        [Test]
        public void Load_PrimaryStorageExceptionDoesNotReadBackup()
        {
            var storage = new TrackingStorage(new FileSaveStorage(_temporaryDirectory)) { ThrowOnPrimaryRead = true };
            var service = new SaveService(storage);

            var result = service.Load<TestSaveData>("storage");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.StorageFailed));
            Assert.That(storage.BackupReadCount, Is.Zero);
            Assert.That(storage.RestoreBackupCount, Is.Zero);
        }

        /// <summary>バックアップ読み込みの保存先例外も呼び出し元へ漏れないことを確かめる。</summary>
        [Test]
        public void Load_BackupStorageExceptionReturnsStorageFailed()
        {
            var storage = new TrackingStorage(new FileSaveStorage(_temporaryDirectory)) { ThrowOnBackupRead = true };
            var service = new SaveService(storage);

            var result = service.Load<TestSaveData>("storage");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.StorageFailed));
            Assert.That(storage.BackupReadCount, Is.EqualTo(1));
            Assert.That(storage.RestoreBackupCount, Is.Zero);
        }

        /// <summary>主データへの復旧例外が漏れず、読めたバックアップ値は成功結果として返ることを確かめる。</summary>
        [Test]
        public void Load_RestoreStorageExceptionReturnsRecoveredValue()
        {
            Assert.That(_service.Save("restore", DataWithScore(10)).IsSuccess, Is.True);
            Assert.That(_service.Save("restore", DataWithScore(20)).IsSuccess, Is.True);
            File.WriteAllText(Path.Combine(_temporaryDirectory, "restore.save"), "corrupt", Encoding.UTF8);

            var storage = new TrackingStorage(new FileSaveStorage(_temporaryDirectory)) { ThrowOnRestoreBackup = true };
            var service = new SaveService(storage);

            var result = service.Load<TestSaveData>("restore");

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(result.Value.Score, Is.EqualTo(10));
            Assert.That(result.Metadata.RecoveredFromBackup, Is.True);
            StringAssert.Contains("書き戻しに失敗", result.Message);
        }

        /// <summary>保存データ変換の例外が呼び出し元へ漏れず、保存先へ書き込まないことを確かめる。</summary>
        [Test]
        public void Save_SerializerExceptionReturnsSerializationFailed()
        {
            var storage = new TrackingStorage(new FileSaveStorage(_temporaryDirectory));
            var service = new SaveService(storage, new ThrowingSerializer());

            var result = service.Save("serializer", DataWithScore(1));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.SerializationFailed));
            Assert.That(storage.WriteCount, Is.Zero);
        }

        /// <summary>Unity JSON が値を欠落させる保存ルート型を拒否し、保存先を変更しないことを確かめる。</summary>
        [Test]
        public void Save_DefaultSerializerRejectsUnsupportedRootTypesBeforeWriting()
        {
            AssertUnsupportedRoot(42);
            AssertUnsupportedRoot(TestEnum.One);
            AssertUnsupportedRoot("text");
            AssertUnsupportedRoot(new[] { 1, 2 });
            AssertUnsupportedRoot(new List<int> { 1, 2 });
            AssertUnsupportedRoot(new Dictionary<string, int> { { "one", 1 } });
            AssertUnsupportedRoot(new NonSerializableSaveData { Value = 5 });

            var gameObject = new UnityEngine.GameObject("SaveSystem unsupported root test");
            try
            {
                AssertUnsupportedRoot(gameObject);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        /// <summary>宣言型より具体的な実行時型を標準変換が拒否し、派生フィールドの欠落を防ぐことを確かめる。</summary>
        [Test]
        public void Save_DefaultSerializerRejectsRuntimeTypeMismatchBeforeWriting()
        {
            BaseSaveData baseValue = new DerivedSaveData { BaseValue = 1, DerivedValue = 2 };
            object objectValue = DataWithScore(3);
            ITestSaveData interfaceValue = new InterfaceSaveData { Value = 4 };

            AssertUnsupportedRoot(baseValue);
            AssertUnsupportedRoot(objectValue);
            AssertUnsupportedRoot(interfaceValue);
        }

        /// <summary>Serializable を付けた具象構造体を標準変換で往復できることを確かめる。</summary>
        [Test]
        public void SaveAndLoad_DefaultSerializerAcceptsSerializableStruct()
        {
            var saved = _service.Save("struct", new StructSaveData { Value = 17 });
            var loaded = _service.Load<StructSaveData>("struct");

            Assert.That(saved.IsSuccess, Is.True, saved.Message);
            Assert.That(loaded.IsSuccess, Is.True, loaded.Message);
            Assert.That(loaded.Value.Value, Is.EqualTo(17));
        }

        /// <summary>時刻供給元の例外が呼び出し元へ漏れず、保存先へ書き込まないことを確かめる。</summary>
        [Test]
        public void Save_ClockExceptionReturnsTimeProviderFailed()
        {
            var storage = new TrackingStorage(new FileSaveStorage(_temporaryDirectory));
            var service = new SaveService(storage, utcNow: () => throw new InvalidOperationException("clock failed"));

            var result = service.Save("clock", DataWithScore(1));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.TimeProviderFailed));
            Assert.That(storage.WriteCount, Is.Zero);
        }

        /// <summary>書き込み先の例外が呼び出し元へ漏れず、保存失敗として返ることを確かめる。</summary>
        [Test]
        public void Save_StorageExceptionReturnsStorageFailed()
        {
            var storage = new TrackingStorage(new FileSaveStorage(_temporaryDirectory)) { ThrowOnWrite = true };
            var service = new SaveService(storage);

            var result = service.Save("storage", DataWithScore(1));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.StorageFailed));
            Assert.That(storage.WriteCount, Is.EqualTo(1));
        }

        /// <summary>削除先の例外が呼び出し元へ漏れず、削除失敗として返ることを確かめる。</summary>
        [Test]
        public void Delete_StorageExceptionReturnsStorageFailed()
        {
            var storage = new TrackingStorage(new FileSaveStorage(_temporaryDirectory)) { ThrowOnDelete = true };
            var service = new SaveService(storage);

            var result = service.Delete("storage");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.StorageFailed));
        }

        /// <summary>パスとして解釈できる文字列が保存先へ到達しないことを確かめる。</summary>
        [TestCase("../outside")]
        [TestCase("folder/slot")]
        [TestCase("folder\\slot")]
        [TestCase("slot name")]
        [TestCase("")]
        public void Save_UnsafeSlotNameIsRejected(string slot)
        {
            var result = _service.Save(slot, DataWithScore(1));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.InvalidSlot));
            Assert.That(Directory.Exists(_temporaryDirectory), Is.False);
        }

        /// <summary>Windows の予約デバイス名と拡張子相当、末尾の点や空白を拒否することを確かめる。</summary>
        [Test]
        public void Save_WindowsReservedSlotNamesAreRejected()
        {
            var reservedNames = new[]
            {
                "CON", "con", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
                "CON.txt", "save.", "save ",
            };

            foreach (var slot in reservedNames)
            {
                var result = _service.Save(slot, DataWithScore(1));
                Assert.That(result.IsSuccess, Is.False, slot);
                Assert.That(result.Error, Is.EqualTo(SaveError.InvalidSlot), slot);
            }

            Assert.That(Directory.Exists(_temporaryDirectory), Is.False);
        }

        /// <summary>日本語を含む安全なスロット名は利用できることを確かめる。</summary>
        [Test]
        public void Save_JapaneseSlotNameIsAccepted()
        {
            var result = _service.Save("手動セーブ_1", DataWithScore(9));

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(File.Exists(Path.Combine(_temporaryDirectory, "手動セーブ_1.save")), Is.True);
        }

        /// <summary>一覧が重複せず名前順になり、削除で主データとバックアップが消えることを確かめる。</summary>
        [Test]
        public void ListSlotsAndDelete_HandlePrimaryAndBackupTogether()
        {
            Assert.That(_service.Save("zeta", DataWithScore(1)).IsSuccess, Is.True);
            Assert.That(_service.Save("alpha", DataWithScore(2)).IsSuccess, Is.True);
            Assert.That(_service.Save("alpha", DataWithScore(3)).IsSuccess, Is.True);

            var listed = _service.ListSlots();
            Assert.That(listed.IsSuccess, Is.True, listed.Message);
            CollectionAssert.AreEqual(new[] { "alpha", "zeta" }, listed.Slots);

            var deleted = _service.Delete("alpha");

            Assert.That(deleted.IsSuccess, Is.True, deleted.Message);
            Assert.That(File.Exists(Path.Combine(_temporaryDirectory, "alpha.save")), Is.False);
            Assert.That(File.Exists(Path.Combine(_temporaryDirectory, "alpha.save.bak")), Is.False);
            var listedAfterDelete = _service.ListSlots();
            Assert.That(listedAfterDelete.IsSuccess, Is.True, listedAfterDelete.Message);
            CollectionAssert.AreEqual(new[] { "zeta" }, listedAfterDelete.Slots);
        }

        /// <summary>一覧取得の保存先例外が漏れず、空一覧を持つ失敗結果になることを確かめる。</summary>
        [Test]
        public void ListSlots_StorageExceptionReturnsStorageFailed()
        {
            var storage = new TrackingStorage(new FileSaveStorage(_temporaryDirectory)) { ThrowOnListSlots = true };
            var service = new SaveService(storage);

            var result = service.ListSlots();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.StorageFailed));
            Assert.That(result.Slots, Is.Empty);
        }

        /// <summary>一覧成功結果が元の一覧から独立した読み取り専用の複製になることを確かめる。</summary>
        [Test]
        public void SaveSlotListResult_SuccessCreatesReadOnlySnapshot()
        {
            var source = new List<string> { "alpha", "zeta" };

            var result = SaveSlotListResult.Success(source);
            source[0] = "changed";

            Assert.That(result.IsSuccess, Is.True);
            CollectionAssert.AreEqual(new[] { "alpha", "zeta" }, result.Slots);
            Assert.That(result.Slots, Is.InstanceOf<System.Collections.ObjectModel.ReadOnlyCollection<string>>());
        }

        /// <summary>存在しないスロットが明確な失敗理由を返すことを確かめる。</summary>
        [Test]
        public void Load_MissingSlotReturnsNotFound()
        {
            var result = _service.Load<TestSaveData>("missing");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.NotFound));
        }

        private static TestSaveData DataWithScore(int score) => new TestSaveData { Name = "test", Score = score, Items = new List<int>() };

        /// <summary>標準変換が対象型を拒否し、保存先を変更しないことを確かめる。</summary>
        /// <typeparam name="T">拒否を期待する宣言型。</typeparam>
        /// <param name="value">拒否を期待する値。</param>
        private void AssertUnsupportedRoot<T>(T value)
        {
            var storage = new TrackingStorage(new FileSaveStorage(_temporaryDirectory));
            var service = new SaveService(storage);

            var result = service.Save("unsupported", value);

            Assert.That(result.IsSuccess, Is.False, typeof(T).FullName);
            Assert.That(result.Error, Is.EqualTo(SaveError.SerializationFailed), typeof(T).FullName);
            Assert.That(storage.WriteCount, Is.Zero, typeof(T).FullName);
            Assert.That(Directory.Exists(_temporaryDirectory), Is.False, typeof(T).FullName);
        }

        /// <summary>保存ルートで拒否する列挙型。</summary>
        private enum TestEnum
        {
            /// <summary>拒否テストへ渡す値。</summary>
            One,
        }

        [Serializable]
        private sealed class TestSaveData
        {
            public string Name;
            public int Score;
            public List<int> Items;
        }

        [Serializable]
        private sealed class AlternativeSaveData
        {
            public string Label;
        }

        /// <summary>実行時型不一致を作る基底保存型。</summary>
        [Serializable]
        private class BaseSaveData
        {
            /// <summary>基底型で保存される値。</summary>
            public int BaseValue;
        }

        /// <summary>基底型にはないフィールドを持つ派生保存型。</summary>
        [Serializable]
        private sealed class DerivedSaveData : BaseSaveData
        {
            /// <summary>誤った宣言型では欠落する値。</summary>
            public int DerivedValue;
        }

        /// <summary>保存ルートで拒否するインターフェース。</summary>
        private interface ITestSaveData
        {
        }

        /// <summary>インターフェース経由で渡す保存型。</summary>
        [Serializable]
        private sealed class InterfaceSaveData : ITestSaveData
        {
            /// <summary>保存対象として見える値。</summary>
            public int Value;
        }

        /// <summary>Serializable を付けないため拒否する保存型。</summary>
        private sealed class NonSerializableSaveData
        {
            /// <summary>誤って保存されないことを確認する値。</summary>
            public int Value;
        }

        /// <summary>標準変換で許可する構造体。</summary>
        [Serializable]
        private struct StructSaveData
        {
            /// <summary>往復を確認する値。</summary>
            public int Value;
        }

        private sealed class ThrowingSerializer : ISaveSerializer
        {
            public string Serialize<T>(T value) => throw new InvalidOperationException("serialize failed");

            public T Deserialize<T>(string serialized) => throw new InvalidOperationException("deserialize failed");
        }

        private sealed class TrackingStorage : ISaveStorage
        {
            private readonly ISaveStorage _inner;

            public TrackingStorage(ISaveStorage inner)
            {
                _inner = inner;
            }

            public bool ThrowOnPrimaryRead { get; set; }

            public bool ThrowOnBackupRead { get; set; }

            public bool ThrowOnWrite { get; set; }

            public bool ThrowOnRestoreBackup { get; set; }

            public bool ThrowOnDelete { get; set; }

            public bool ThrowOnListSlots { get; set; }

            public int BackupReadCount { get; private set; }

            public int RestoreBackupCount { get; private set; }

            public int WriteCount { get; private set; }

            public bool TryRead(string slot, out string contents)
            {
                if (ThrowOnPrimaryRead) throw new IOException("read failed");
                return _inner.TryRead(slot, out contents);
            }

            public bool TryReadBackup(string slot, out string contents)
            {
                BackupReadCount++;
                if (ThrowOnBackupRead) throw new IOException("backup read failed");
                return _inner.TryReadBackup(slot, out contents);
            }

            public void Write(string slot, string contents)
            {
                WriteCount++;
                if (ThrowOnWrite) throw new IOException("write failed");
                _inner.Write(slot, contents);
            }

            public bool RestoreBackup(string slot)
            {
                RestoreBackupCount++;
                if (ThrowOnRestoreBackup) throw new IOException("restore failed");
                return _inner.RestoreBackup(slot);
            }

            public bool Delete(string slot)
            {
                if (ThrowOnDelete) throw new IOException("delete failed");
                return _inner.Delete(slot);
            }

            public IReadOnlyList<string> ListSlots()
            {
                if (ThrowOnListSlots) throw new IOException("list failed");
                return _inner.ListSlots();
            }
        }
    }
}
