using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SaveSystem
{
    /// <summary>
    /// 型付きデータの保存、整合性確認、直前バックアップからの復旧をまとめる。
    /// 保存先と文字列変換を差し替えられるため、ゲーム固有の型やクラウド保存にも対応できる。
    /// </summary>
    public sealed class SaveService
    {
        private const int CurrentFormatVersion = 2;

        private readonly ISaveStorage _storage;
        private readonly ISaveSerializer _serializer;
        private readonly Func<DateTime> _utcNow;

        /// <summary>保存先と変換方法を指定して作る。</summary>
        /// <param name="storage">ファイルやクラウドなどの保存先。</param>
        /// <param name="serializer">ゲームデータの変換方法。省略時は Unity JSON。</param>
        /// <param name="utcNow">保存時刻の供給元。省略時は現在の UTC。</param>
        public SaveService(ISaveStorage storage, ISaveSerializer serializer = null, Func<DateTime> utcNow = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _serializer = serializer ?? new UnityJsonSaveSerializer();
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <summary><see cref="Application.persistentDataPath"/> 以下へ保存する標準構成を作る。</summary>
        /// <param name="folderName">persistentDataPath 直下のフォルダー名。</param>
        /// <returns>標準のファイル保存先と JSON 変換を使う保存サービス。</returns>
        /// <exception cref="PlatformNotSupportedException">WebGL Player または tvOS Player で作成した場合。</exception>
        public static SaveService CreateDefault(string folderName = "Saves")
        {
#if (UNITY_WEBGL || UNITY_TVOS) && !UNITY_EDITOR
            throw new PlatformNotSupportedException("SaveService.CreateDefault は WebGL Player と tvOS Player の同期ファイル保存に対応していません。別の ISaveStorage を指定してください。");
#else
            if (!SaveSlot.IsValid(folderName)) throw new ArgumentException("保存フォルダー名には文字、数字、ハイフン、アンダースコアを使用してください。", nameof(folderName));

            var path = System.IO.Path.Combine(Application.persistentDataPath, folderName);
            return new SaveService(new FileSaveStorage(path));
#endif
        }

        /// <summary>値を保存する。同じスロットの以前の主データはバックアップへ移る。</summary>
        /// <typeparam name="T">保存する値の型。</typeparam>
        /// <param name="slot">保存スロット。</param>
        /// <param name="value">保存する値。</param>
        /// <param name="dataVersion">ゲーム側が決めるデータ版。</param>
        /// <returns>保存の成否、失敗理由、保存データの識別情報を持つ結果。</returns>
        public SaveOperationResult Save<T>(string slot, T value, string dataVersion = "1")
        {
            if (!SaveSlot.IsValid(slot)) return SaveOperationResult.Failure(SaveError.InvalidSlot, "スロット名が不正です。");
            if (ReferenceEquals(value, null)) return SaveOperationResult.Failure(SaveError.InvalidData, "保存する値が null です。");
            if (string.IsNullOrWhiteSpace(dataVersion)) return SaveOperationResult.Failure(SaveError.InvalidData, "データ版が空です。");

            string payload;
            try
            {
                payload = _serializer.Serialize(value);
                if (payload == null) return SaveOperationResult.Failure(SaveError.SerializationFailed, "保存データの変換結果が null です。");
            }
            catch (Exception exception)
            {
                return SaveOperationResult.Failure(SaveError.SerializationFailed, $"保存データを変換できませんでした: {exception.Message}");
            }

            DateTime savedAtUtc;
            try
            {
                savedAtUtc = _utcNow().ToUniversalTime();
            }
            catch (Exception exception)
            {
                return SaveOperationResult.Failure(SaveError.TimeProviderFailed, $"保存時刻を取得できませんでした: {exception.Message}");
            }

            SaveEnvelope envelope;
            try
            {
                envelope = new SaveEnvelope
                {
                    FormatVersion = CurrentFormatVersion,
                    DataVersion = dataVersion,
                    TypeId = GetTypeId(typeof(T)),
                    SavedAtUtcTicks = savedAtUtc.Ticks,
                    Payload = payload,
                };
                envelope.Checksum = ComputeChecksum(envelope);
            }
            catch (Exception exception)
            {
                return SaveOperationResult.Failure(SaveError.SerializationFailed, $"保存形式を作成できませんでした: {exception.Message}");
            }

            string contents;
            try
            {
                contents = JsonUtility.ToJson(envelope);
                if (string.IsNullOrEmpty(contents)) return SaveOperationResult.Failure(SaveError.SerializationFailed, "保存形式を JSON に変換できませんでした。");
            }
            catch (Exception exception)
            {
                return SaveOperationResult.Failure(SaveError.SerializationFailed, $"保存形式を JSON に変換できませんでした: {exception.Message}");
            }

            try
            {
                _storage.Write(slot, contents);
            }
            catch (Exception exception)
            {
                return SaveOperationResult.Failure(SaveError.StorageFailed, $"保存先へ書き込めませんでした: {exception.Message}");
            }

            return SaveOperationResult.Success(new SaveMetadata(slot, dataVersion, savedAtUtc, false));
        }

        /// <summary>値を読み込み、主データが壊れていれば直前のバックアップから復旧する。</summary>
        /// <typeparam name="T">読み込む値の型。</typeparam>
        /// <param name="slot">保存スロット。</param>
        /// <param name="expectedDataVersion">必要なデータ版。null または空なら版を問わない。</param>
        /// <returns>読込値、成否、失敗理由、保存データの識別情報を持つ結果。</returns>
        public SaveLoadResult<T> Load<T>(string slot, string expectedDataVersion = null)
        {
            if (!SaveSlot.IsValid(slot)) return SaveLoadResult<T>.Failure(SaveError.InvalidSlot, "スロット名が不正です。");

            var primary = ReadCandidate<T>(slot, false, expectedDataVersion);
            if (primary.IsSuccess) return CandidateSuccess(slot, primary, false, string.Empty);

            if (primary.Exists && primary.Error != SaveError.CorruptData)
            {
                return SaveLoadResult<T>.Failure(primary.Error, primary.Message);
            }

            var backup = ReadCandidate<T>(slot, true, expectedDataVersion);
            if (backup.IsSuccess)
            {
                var message = "主データを読めなかったため、直前のバックアップから復旧しました。";
                try
                {
                    if (!_storage.RestoreBackup(slot)) message += " バックアップを主データへ戻せませんでした。";
                }
                catch (Exception exception)
                {
                    message += $" 主データへの書き戻しに失敗しました: {exception.Message}";
                }

                return CandidateSuccess(slot, backup, true, message);
            }

            return SelectFailure<T>(primary, backup);
        }

        /// <summary>主データとバックアップを削除する。存在しない場合も成功として扱う。</summary>
        /// <param name="slot">保存スロット。</param>
        /// <returns>削除の成否と失敗理由を持つ結果。</returns>
        public SaveOperationResult Delete(string slot)
        {
            if (!SaveSlot.IsValid(slot)) return SaveOperationResult.Failure(SaveError.InvalidSlot, "スロット名が不正です。");

            try
            {
                var deleted = _storage.Delete(slot);
                return SaveOperationResult.Success(message: deleted ? "保存データを削除しました。" : "削除する保存データはありませんでした。");
            }
            catch (Exception exception)
            {
                return SaveOperationResult.Failure(SaveError.StorageFailed, $"保存データを削除できませんでした: {exception.Message}");
            }
        }

        /// <summary>主データまたはバックアップが存在するスロットを名前順で取得する。</summary>
        /// <returns>取得の成否、スロット一覧、失敗理由を持つ結果。</returns>
        public SaveSlotListResult ListSlots()
        {
            try
            {
                return SaveSlotListResult.Success(_storage.ListSlots());
            }
            catch (Exception exception)
            {
                return SaveSlotListResult.Failure(SaveError.StorageFailed, $"保存スロット一覧を取得できませんでした: {exception.Message}");
            }
        }

        private Candidate<T> ReadCandidate<T>(string slot, bool backup, string expectedDataVersion)
        {
            string contents;
            try
            {
                var found = backup ? _storage.TryReadBackup(slot, out contents) : _storage.TryRead(slot, out contents);
                if (!found) return Candidate<T>.Missing();
            }
            catch (Exception exception)
            {
                return Candidate<T>.Failure(SaveError.StorageFailed, $"保存先を読めませんでした: {exception.Message}");
            }

            SaveEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<SaveEnvelope>(contents);
            }
            catch (Exception exception)
            {
                return Candidate<T>.Failure(SaveError.CorruptData, $"保存形式を読めませんでした: {exception.Message}");
            }

            if (envelope == null)
            {
                return Candidate<T>.Failure(SaveError.CorruptData, "保存形式に必要な情報がありません。");
            }

            if (envelope.FormatVersion <= 0)
            {
                return Candidate<T>.Failure(SaveError.CorruptData, "保存形式版が欠落しているか不正です。");
            }

            if (envelope.FormatVersion != CurrentFormatVersion)
            {
                return Candidate<T>.Failure(SaveError.FormatVersionMismatch, $"保存形式版 {envelope.FormatVersion} は対応版 {CurrentFormatVersion} と一致しません。");
            }

            if (string.IsNullOrEmpty(envelope.DataVersion) || string.IsNullOrEmpty(envelope.TypeId) || envelope.Payload == null || string.IsNullOrEmpty(envelope.Checksum))
            {
                return Candidate<T>.Failure(SaveError.CorruptData, "保存形式に必要な情報がありません。");
            }

            string checksum;
            try
            {
                checksum = ComputeChecksum(envelope);
            }
            catch (Exception exception)
            {
                return Candidate<T>.Failure(SaveError.CorruptData, $"保存データのチェックサムを確認できませんでした: {exception.Message}");
            }

            if (!string.Equals(envelope.Checksum, checksum, StringComparison.OrdinalIgnoreCase))
            {
                return Candidate<T>.Failure(SaveError.CorruptData, "保存データのチェックサムが一致しません。");
            }

            var requestedTypeId = GetTypeId(typeof(T));
            if (!string.Equals(envelope.TypeId, requestedTypeId, StringComparison.Ordinal))
            {
                return Candidate<T>.Failure(SaveError.TypeMismatch, $"保存データの型 {envelope.TypeId} は要求された型 {requestedTypeId} と一致しません。");
            }

            if (!string.IsNullOrEmpty(expectedDataVersion) && !string.Equals(expectedDataVersion, envelope.DataVersion, StringComparison.Ordinal))
            {
                return Candidate<T>.Failure(SaveError.VersionMismatch, $"保存データ版 {envelope.DataVersion} は必要な版 {expectedDataVersion} と一致しません。");
            }

            DateTime savedAtUtc;
            try
            {
                savedAtUtc = new DateTime(envelope.SavedAtUtcTicks, DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Candidate<T>.Failure(SaveError.CorruptData, "保存時刻が不正です。");
            }

            try
            {
                var value = _serializer.Deserialize<T>(envelope.Payload);
                if (ReferenceEquals(value, null)) return Candidate<T>.Failure(SaveError.SerializationFailed, "保存データの変換結果が null です。");
                return Candidate<T>.Success(value, envelope.DataVersion, savedAtUtc);
            }
            catch (Exception exception)
            {
                return Candidate<T>.Failure(SaveError.SerializationFailed, $"保存データを要求された型へ変換できませんでした: {exception.Message}");
            }
        }

        private static SaveLoadResult<T> CandidateSuccess<T>(string slot, Candidate<T> candidate, bool recoveredFromBackup, string message)
        {
            var metadata = new SaveMetadata(slot, candidate.DataVersion, candidate.SavedAtUtc, recoveredFromBackup);
            return SaveLoadResult<T>.Success(candidate.Value, metadata, message);
        }

        private static SaveLoadResult<T> SelectFailure<T>(Candidate<T> primary, Candidate<T> backup)
        {
            if (!primary.Exists && !backup.Exists) return SaveLoadResult<T>.Failure(SaveError.NotFound, "指定した保存スロットは存在しません。");

            var preferred = primary.Exists ? primary : backup;
            if (primary.Error == SaveError.FormatVersionMismatch || backup.Error == SaveError.FormatVersionMismatch)
            {
                preferred = primary.Error == SaveError.FormatVersionMismatch ? primary : backup;
            }
            else if (primary.Error == SaveError.VersionMismatch || backup.Error == SaveError.VersionMismatch)
            {
                preferred = primary.Error == SaveError.VersionMismatch ? primary : backup;
            }
            else if (primary.Error == SaveError.StorageFailed || backup.Error == SaveError.StorageFailed)
            {
                preferred = primary.Error == SaveError.StorageFailed ? primary : backup;
            }

            return SaveLoadResult<T>.Failure(preferred.Error, preferred.Message);
        }

        private static string ComputeChecksum(SaveEnvelope envelope)
        {
            var canonical = $"{envelope.FormatVersion}\n{envelope.DataVersion.Length}:{envelope.DataVersion}\n{envelope.TypeId.Length}:{envelope.TypeId}\n{envelope.SavedAtUtcTicks}\n{envelope.Payload.Length}:{envelope.Payload}";
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var builder = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }

        private static string GetTypeId(Type type)
        {
            if (type.IsArray)
            {
                var ranks = new string(',', type.GetArrayRank() - 1);
                return $"{GetTypeId(type.GetElementType())}[{ranks}]";
            }

            var assemblyName = type.Assembly.GetName().Name ?? string.Empty;
            var typeName = type.FullName ?? type.Name;
            if (!type.IsGenericType) return $"{assemblyName}:{typeName}";

            var definition = type.GetGenericTypeDefinition();
            var definitionAssemblyName = definition.Assembly.GetName().Name ?? string.Empty;
            var definitionName = definition.FullName ?? definition.Name;
            var arguments = type.GetGenericArguments();
            var builder = new StringBuilder();
            builder.Append(definitionAssemblyName).Append(':').Append(definitionName).Append('[');
            for (var i = 0; i < arguments.Length; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(GetTypeId(arguments[i]));
            }

            return builder.Append(']').ToString();
        }

        private sealed class Candidate<T>
        {
            private Candidate(bool exists, bool isSuccess, T value, SaveError error, string message, string dataVersion, DateTime savedAtUtc)
            {
                Exists = exists;
                IsSuccess = isSuccess;
                Value = value;
                Error = error;
                Message = message;
                DataVersion = dataVersion;
                SavedAtUtc = savedAtUtc;
            }

            public bool Exists { get; }

            public bool IsSuccess { get; }

            public T Value { get; }

            public SaveError Error { get; }

            public string Message { get; }

            public string DataVersion { get; }

            public DateTime SavedAtUtc { get; }

            public static Candidate<T> Missing() => new Candidate<T>(false, false, default, SaveError.NotFound, string.Empty, null, default);

            public static Candidate<T> Failure(SaveError error, string message) => new Candidate<T>(true, false, default, error, message, null, default);

            public static Candidate<T> Success(T value, string dataVersion, DateTime savedAtUtc) => new Candidate<T>(true, true, value, SaveError.None, string.Empty, dataVersion, savedAtUtc);
        }
    }
}
