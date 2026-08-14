using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DiagnosticsContext
{
    /// <summary>明示context、breadcrumb、対象Unity logをowner寿命内で有界保持し、手動reportへ保存する。</summary>
    public sealed class DiagnosticsContextService : IDisposable
    {
        /// <summary>同時に保持できるcontext keyの最大件数。</summary>
        public const int MaximumContextEntryCount = 32;

        /// <summary>同時に保持できるbreadcrumbの最大件数。</summary>
        public const int MaximumBreadcrumbCount = 64;

        /// <summary>同時に保持できるcaptured logの最大件数。</summary>
        public const int MaximumCapturedLogCount = 32;

        /// <summary>context keyに許可する最大Unicode scalar数。</summary>
        public const int MaximumContextKeyScalarCount = 64;

        /// <summary>context valueに許可する最大Unicode scalar数。</summary>
        public const int MaximumContextValueScalarCount = 256;

        /// <summary>breadcrumb本文に許可する最大Unicode scalar数。</summary>
        public const int MaximumBreadcrumbMessageScalarCount = 512;

        /// <summary>captured log本文に保持する最大Unicode scalar数。</summary>
        public const int MaximumLogMessageScalarCount = 1024;

        /// <summary>captured log stackに保持する最大Unicode scalar数。</summary>
        public const int MaximumLogStackTraceScalarCount = 2048;

        /// <summary>report reasonに保持する最大Unicode scalar数。</summary>
        public const int MaximumReasonScalarCount = 256;

        /// <summary>report JSON全体に許可する最大UTF-8 byte数。</summary>
        public const int MaximumReportByteCount = 512 * 1024;

        /// <summary>BOMを付けず、不正Unicodeを拒否するUTF-8 encoder。</summary>
        private static readonly UTF8Encoding Utf8Encoding = new UTF8Encoding(false, true);

        /// <summary>状態、callback、snapshot開始を直列化するlock。</summary>
        private readonly object _sync = new object();

        /// <summary>現在のcontextをordinal key比較で保持する領域。</summary>
        private readonly Dictionary<string, string> _context = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>古い項目から追い出すbreadcrumb領域。</summary>
        private readonly BoundedRing<DiagnosticsBreadcrumbItem> _breadcrumbs = new BoundedRing<DiagnosticsBreadcrumbItem>(MaximumBreadcrumbCount);

        /// <summary>古い項目から追い出すcaptured log領域。</summary>
        private readonly BoundedRing<DiagnosticsLogItem> _logs = new BoundedRing<DiagnosticsLogItem>(MaximumCapturedLogCount);

        /// <summary>reportを保存する専用directoryの正規化済み絶対path。</summary>
        private readonly string _reportDirectory;

        /// <summary>snapshot時刻を得るproductionまたはtest用関数。</summary>
        private readonly Func<DateTime> _utcNow;

        /// <summary>利用者入力と無関係な最終file名用IDを得る関数。</summary>
        private readonly Func<Guid> _uniqueId;

        /// <summary>上限確認済みUTF-8を最終reportへ保存するproductionまたはtest用関数。</summary>
        private readonly Func<string, DateTime, Guid, byte[], DiagnosticsWriteResult> _reportWriter;

        /// <summary>次に追加する時系列項目へ割り当てるsequence。</summary>
        private long _nextSequence = 1;

        /// <summary>容量上限により追い出したbreadcrumb総数。</summary>
        private long _droppedBreadcrumbCount;

        /// <summary>容量上限により追い出したcaptured log総数。</summary>
        private long _droppedLogCount;

        /// <summary>snapshot開始済みでまだ終了していないreport書出し数。</summary>
        private int _activeWriteCount;

        /// <summary>0は利用中、1は終了処理中、2は終了完了を表す。</summary>
        private int _disposeState;

        /// <summary>Unity threaded log callbackを購読中ならtrue。</summary>
        private bool _subscribed;

        /// <summary>保存先と依存関数を受け取り、必要な場合だけUnity logを購読する。</summary>
        /// <param name="reportDirectory">正規化済みの専用保存directory。</param>
        /// <param name="utcNow">snapshot時刻を返す関数。</param>
        /// <param name="uniqueId">一意なfile名用IDを返す関数。</param>
        /// <param name="subscribeToUnityLogs">Unity log callbackを購読する場合はtrue。</param>
        /// <param name="reportWriter">nullならproduction file writerを使う書出し関数。</param>
        internal DiagnosticsContextService(
            string reportDirectory,
            Func<DateTime> utcNow,
            Func<Guid> uniqueId,
            bool subscribeToUnityLogs,
            Func<string, DateTime, Guid, byte[], DiagnosticsWriteResult> reportWriter = null)
        {
            _reportDirectory = reportDirectory;
            _utcNow = utcNow;
            _uniqueId = uniqueId;
            _reportWriter = reportWriter ?? DiagnosticsFileWriter.Write;

            if (subscribeToUnityLogs)
            {
                Application.logMessageReceivedThreaded += HandleUnityLog;
                _subscribed = true;
            }
        }

        /// <summary>現在保持しているcontext keyの件数。</summary>
        public int ContextEntryCount
        {
            get
            {
                lock (_sync) return _context.Count;
            }
        }

        /// <summary>現在保持しているbreadcrumbの件数。</summary>
        public int BreadcrumbCount
        {
            get
            {
                lock (_sync) return _breadcrumbs.Count;
            }
        }

        /// <summary>現在保持している対象Unity logの件数。</summary>
        public int CapturedLogCount
        {
            get
            {
                lock (_sync) return _logs.Count;
            }
        }

        /// <summary>容量上限により追い出したbreadcrumbの累積件数。</summary>
        public long DroppedBreadcrumbCount
        {
            get
            {
                lock (_sync) return _droppedBreadcrumbCount;
            }
        }

        /// <summary>容量上限により追い出した対象Unity logの累積件数。</summary>
        public long DroppedLogCount
        {
            get
            {
                lock (_sync) return _droppedLogCount;
            }
        }

        /// <summary>Unityメインスレッドで、persistentDataPath配下を保存先とするServiceを作成する。</summary>
        /// <param name="service">成功時に明示ownerが保持する新しいService。</param>
        /// <param name="error">作成できなかった理由。成功時はNone。</param>
        /// <returns>Serviceを作成してlog購読を開始できた場合はtrue。</returns>
        public static bool TryCreate(out DiagnosticsContextService service, out DiagnosticsError error)
        {
            service = null;
            if (!DiagnosticsMainThread.IsCurrent)
            {
                error = DiagnosticsError.MainThreadRequired;
                return false;
            }

            try
            {
                var persistentDataPath = Application.persistentDataPath;
                if (string.IsNullOrWhiteSpace(persistentDataPath))
                {
                    error = DiagnosticsError.StorageUnavailable;
                    return false;
                }

                var normalizedRoot = Path.GetFullPath(persistentDataPath);
                var reportDirectory = Path.GetFullPath(Path.Combine(normalizedRoot, "DiagnosticsContext"));
                var rootWithSeparator = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!reportDirectory.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    error = DiagnosticsError.StorageUnavailable;
                    return false;
                }

                service = new DiagnosticsContextService(reportDirectory, GetUtcNow, Guid.NewGuid, true);
                error = DiagnosticsError.None;
                return true;
            }
            catch (Exception exception) when (IsCreationException(exception))
            {
                service = null;
                error = DiagnosticsError.StorageUnavailable;
                return false;
            }
        }

        /// <summary>現在状態を表すkeyとvalueを追加または更新する。</summary>
        /// <param name="key">空でなく上限内のordinal key。</param>
        /// <param name="value">nullでない現在値。空を許可し、上限超過と不正surrogateは安全に整える。</param>
        /// <returns>保持できた場合はNone、それ以外は入力、容量、寿命の失敗理由。</returns>
        public DiagnosticsError SetContext(string key, string value)
        {
            lock (_sync)
            {
                if (_disposeState != 0) return DiagnosticsError.Disposed;
                if (!DiagnosticsText.IsValidRequiredInput(key, MaximumContextKeyScalarCount) || value == null) return DiagnosticsError.InvalidInput;
                if (!_context.ContainsKey(key) && _context.Count >= MaximumContextEntryCount) return DiagnosticsError.ContextCapacityExceeded;

                _context[key] = DiagnosticsText.NormalizeAndTruncate(value, MaximumContextValueScalarCount);
                return DiagnosticsError.None;
            }
        }

        /// <summary>指定keyのcontextを取り除く。存在しないkeyは成功として扱う。</summary>
        /// <param name="key">空でなく上限内のordinal key。</param>
        /// <returns>削除要求を処理できた場合はNone、それ以外は入力または寿命の失敗理由。</returns>
        public DiagnosticsError RemoveContext(string key)
        {
            lock (_sync)
            {
                if (_disposeState != 0) return DiagnosticsError.Disposed;
                if (!DiagnosticsText.IsValidRequiredInput(key, MaximumContextKeyScalarCount)) return DiagnosticsError.InvalidInput;
                _context.Remove(key);
                return DiagnosticsError.None;
            }
        }

        /// <summary>利用側が明示した短い出来事を時系列末尾へ追加する。</summary>
        /// <param name="message">空でないbreadcrumb本文。上限超過と不正surrogateは安全に整える。</param>
        /// <returns>保持できた場合はNone、それ以外は入力または寿命の失敗理由。</returns>
        public DiagnosticsError AddBreadcrumb(string message)
        {
            lock (_sync)
            {
                if (_disposeState != 0) return DiagnosticsError.Disposed;
                if (string.IsNullOrWhiteSpace(message)) return DiagnosticsError.InvalidInput;

                var normalizedMessage = DiagnosticsText.NormalizeAndTruncate(message, MaximumBreadcrumbMessageScalarCount);
                var dropped = _breadcrumbs.Add(new DiagnosticsBreadcrumbItem(TakeSequence(), normalizedMessage));
                if (dropped) SaturatingIncrement(ref _droppedBreadcrumbCount);
                return DiagnosticsError.None;
            }
        }

        /// <summary>現在までの有界snapshotを専用directoryの新しいJSON reportへ保存する。</summary>
        /// <param name="reason">空でない書出し理由。上限を超えた部分はUnicode scalar境界で切り詰める。</param>
        /// <returns>成功時は最終pathとUTF-8 byte数、失敗時は理由を持つ結果。</returns>
        public DiagnosticsWriteResult WriteReport(string reason)
        {
            DiagnosticsReportSnapshot snapshot;
            lock (_sync)
            {
                if (_disposeState != 0) return DiagnosticsWriteResult.Failure(DiagnosticsError.Disposed);
                if (!DiagnosticsMainThread.IsCurrent) return DiagnosticsWriteResult.Failure(DiagnosticsError.MainThreadRequired);
                if (string.IsNullOrWhiteSpace(reason)) return DiagnosticsWriteResult.Failure(DiagnosticsError.InvalidInput);

                var normalizedReason = DiagnosticsText.NormalizeAndTruncate(reason, MaximumReasonScalarCount);
                snapshot = CreateSnapshot(normalizedReason, _utcNow().ToUniversalTime());
                _activeWriteCount++;
            }

            try
            {
                var json = DiagnosticsJsonSerializer.Serialize(snapshot);
                var byteCount = Utf8Encoding.GetByteCount(json);
                if (byteCount > MaximumReportByteCount) return DiagnosticsWriteResult.Failure(DiagnosticsError.ReportTooLarge);

                var bytes = Utf8Encoding.GetBytes(json);
                var storageError = DiagnosticsFileWriter.TryPrepareDirectory(_reportDirectory, out var preparedDirectory);
                if (storageError != DiagnosticsError.None) return DiagnosticsWriteResult.Failure(storageError);
                return _reportWriter(preparedDirectory, snapshot.CreatedUtc, _uniqueId(), bytes);
            }
            finally
            {
                CompleteWrite();
            }
        }

        /// <summary>log購読と保留状態を終了する。別threadと繰返し呼出しを許容する。</summary>
        public void Dispose()
        {
            lock (_sync)
            {
                while (_disposeState == 1) Monitor.Wait(_sync);
                if (_disposeState == 2) return;
                _disposeState = 1;
            }

            try
            {
                if (_subscribed) Application.logMessageReceivedThreaded -= HandleUnityLog;
            }
            catch
            {
                lock (_sync)
                {
                    _disposeState = 0;
                    Monitor.PulseAll(_sync);
                }

                throw;
            }

            lock (_sync)
            {
                _subscribed = false;
                while (_activeWriteCount > 0) Monitor.Wait(_sync);
                _context.Clear();
                _breadcrumbs.Clear();
                _logs.Clear();
                _disposeState = 2;
                Monitor.PulseAll(_sync);
            }
        }

        /// <summary>Unity threaded callbackから対象log文字列だけを有界領域へcopyする。</summary>
        /// <param name="condition">Unityから渡されたlog本文。</param>
        /// <param name="stackTrace">Unityから渡されたstack文字列。</param>
        /// <param name="type">Unity logの種別。</param>
        private void HandleUnityLog(string condition, string stackTrace, LogType type)
        {
            CaptureLog(condition, stackTrace, type);
        }

        /// <summary>Unity API、path、clock、fileへ触れず対象logをlock内で有界copyする。</summary>
        private void CaptureLog(string condition, string stackTrace, LogType type)
        {
            if (!IsCapturedLogType(type)) return;

            lock (_sync)
            {
                if (_disposeState != 0) return;
                var boundedMessage = DiagnosticsText.NormalizeAndTruncate(condition, MaximumLogMessageScalarCount);
                var boundedStackTrace = DiagnosticsText.NormalizeAndTruncate(stackTrace, MaximumLogStackTraceScalarCount);
                var dropped = _logs.Add(new DiagnosticsLogItem(TakeSequence(), type, boundedMessage, boundedStackTrace));
                if (dropped) SaturatingIncrement(ref _droppedLogCount);
            }
        }

        /// <summary>決定論的testからUnity eventなしで同じlog copy経路を実行する。</summary>
        internal void CaptureLogForTesting(string condition, string stackTrace, LogType type)
        {
            CaptureLog(condition, stackTrace, type);
        }

        /// <summary>現在状態を独立したordinal順snapshotへcopyする。</summary>
        private DiagnosticsReportSnapshot CreateSnapshot(string reason, DateTime createdUtc)
        {
            var keys = new string[_context.Count];
            _context.Keys.CopyTo(keys, 0);
            Array.Sort(keys, StringComparer.Ordinal);
            var contextItems = new DiagnosticsContextItem[keys.Length];
            for (var index = 0; index < keys.Length; index++) contextItems[index] = new DiagnosticsContextItem(keys[index], _context[keys[index]]);

            return new DiagnosticsReportSnapshot(
                createdUtc,
                reason,
                _droppedBreadcrumbCount,
                _droppedLogCount,
                contextItems,
                _breadcrumbs.Snapshot(),
                _logs.Snapshot());
        }

        /// <summary>時系列項目へ現在sequenceを渡し、上限到達後は同値を使ってring順の非減少性を保つ。</summary>
        private long TakeSequence()
        {
            var sequence = _nextSequence;
            if (_nextSequence < long.MaxValue) _nextSequence++;
            return sequence;
        }

        /// <summary>sequence上限の回帰test用に次値を明示設定する。</summary>
        /// <param name="nextSequence">次の時系列項目へ割り当てる1以上の値。</param>
        internal void SetNextSequenceForTesting(long nextSequence)
        {
            lock (_sync) _nextSequence = nextSequence;
        }

        /// <summary>long上限で止まる累積件数を1増やす。</summary>
        private static void SaturatingIncrement(ref long value)
        {
            if (value < long.MaxValue) value++;
        }

        /// <summary>自動取得対象のWarning、Error、Assert、Exceptionならtrueを返す。</summary>
        private static bool IsCapturedLogType(LogType type)
        {
            return type == LogType.Warning || type == LogType.Error || type == LogType.Assert || type == LogType.Exception;
        }

        /// <summary>production snapshot用の現在UTC時刻を返す。</summary>
        private static DateTime GetUtcNow()
        {
            return DateTime.UtcNow;
        }

        /// <summary>作成時のpath解決またはlog購読失敗として扱う例外ならtrueを返す。</summary>
        private static bool IsCreationException(Exception exception)
        {
            return exception is ArgumentException ||
                   exception is NotSupportedException ||
                   exception is PathTooLongException ||
                   exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is SecurityException ||
                   exception is InvalidOperationException;
        }

        /// <summary>report書出し完了を通知し、終了待ちのDisposeを再開する。</summary>
        private void CompleteWrite()
        {
            lock (_sync)
            {
                _activeWriteCount--;
                if (_activeWriteCount == 0) Monitor.PulseAll(_sync);
            }
        }
    }
}
