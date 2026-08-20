using System;

namespace ReplayTape
{
    /// <summary>tick順のcommand payloadを有界なversion 1 canonical tapeへ構築する。</summary>
    public sealed class ReplayTapeBuilder : IDisposable
    {
        /// <summary>既定で許可するtape全体のbyte数。</summary>
        public const int DefaultMaximumByteCount = 1024 * 1024;

        /// <summary>1つのbuilderへ設定できる最大byte数。</summary>
        public const int MaximumAllowedByteCount = 16 * 1024 * 1024;

        /// <summary>既定で許可するentry数。</summary>
        public const int DefaultMaximumEntryCount = 65536;

        /// <summary>1つのbuilderへ設定できる最大entry数。</summary>
        public const int MaximumAllowedEntryCount = 1000000;

        private readonly int _maximumByteCount;
        private readonly int _maximumEntryCount;
        private byte[] _buffer;
        private int _byteCount;
        private int _entryCount;
        private ulong _lastTick;
        private bool _hasLastTick;
        private bool _disposed;

        /// <summary>byte数とentry数の上限を持つ空のbuilderを作る。</summary>
        /// <param name="maximumByteCount">headerを含むtape byte数の上限。</param>
        /// <param name="maximumEntryCount">追加できるentry数の上限。</param>
        public ReplayTapeBuilder(int maximumByteCount = DefaultMaximumByteCount, int maximumEntryCount = DefaultMaximumEntryCount)
        {
            if (maximumByteCount < ReplayTapeFormat.HeaderByteCount || maximumByteCount > MaximumAllowedByteCount) throw new ArgumentOutOfRangeException(nameof(maximumByteCount));
            if (maximumEntryCount < 1 || maximumEntryCount > MaximumAllowedEntryCount) throw new ArgumentOutOfRangeException(nameof(maximumEntryCount));
            _maximumByteCount = maximumByteCount;
            _maximumEntryCount = maximumEntryCount;
            _buffer = new byte[Math.Min(maximumByteCount, 256)];
            Initialize();
        }

        /// <summary>headerを含む現在のtape byte数。</summary>
        public int ByteCount => _byteCount;

        /// <summary>追加済みentry数。</summary>
        public int EntryCount => _entryCount;

        /// <summary>このbuilderが許可するtape byte数。</summary>
        public int MaximumByteCount => _maximumByteCount;

        /// <summary>このbuilderが許可するentry数。</summary>
        public int MaximumEntryCount => _maximumEntryCount;

        /// <summary>破棄済みかを返す。</summary>
        public bool IsDisposed => _disposed;

        /// <summary>commandを指定tickへ追加する。同tick内は呼出順を保持する。</summary>
        /// <param name="tick">利用側が定義した非減少の整数tick。</param>
        /// <param name="commandId">0以外の利用側schema id。</param>
        /// <param name="payload">解釈せずcopyするpayload。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>追加できた場合にtrue。</returns>
        public bool TryAppend(ulong tick, uint commandId, ReadOnlySpan<byte> payload, out ReplayTapeError error)
        {
            if (_disposed)
            {
                error = ReplayTapeError.Disposed;
                return false;
            }

            if (commandId == 0)
            {
                error = ReplayTapeError.InvalidInput;
                return false;
            }

            if (_hasLastTick && tick < _lastTick)
            {
                error = ReplayTapeError.TickOrderViolation;
                return false;
            }

            if (_entryCount >= _maximumEntryCount)
            {
                error = ReplayTapeError.CapacityExceeded;
                return false;
            }

            var requiredByteCount = (long)_byteCount + ReplayTapeFormat.RecordHeaderByteCount + payload.Length;
            if (requiredByteCount > _maximumByteCount)
            {
                error = ReplayTapeError.CapacityExceeded;
                return false;
            }

            EnsureCapacity((int)requiredByteCount);
            var recordOffset = _byteCount;
            ReplayTapeFormat.WriteUInt64(_buffer, recordOffset, tick);
            ReplayTapeFormat.WriteUInt32(_buffer, recordOffset + 8, commandId);
            ReplayTapeFormat.WriteUInt32(_buffer, recordOffset + 12, (uint)payload.Length);
            payload.CopyTo(_buffer.AsSpan(recordOffset + ReplayTapeFormat.RecordHeaderByteCount, payload.Length));
            _byteCount = (int)requiredByteCount;
            _entryCount++;
            _lastTick = tick;
            _hasLastTick = true;
            ReplayTapeFormat.WriteCounts(_buffer, _entryCount, _byteCount);
            error = ReplayTapeError.None;
            return true;
        }

        /// <summary>現在の内容をcopyしたimmutable tapeを作る。builderの内容は保持する。</summary>
        /// <param name="value">成功時のtape。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>tapeを作れた場合にtrue。</returns>
        public bool TryBuild(out ReplayTapeValue value, out ReplayTapeError error)
        {
            value = default;
            if (_disposed)
            {
                error = ReplayTapeError.Disposed;
                return false;
            }

            var bytes = new byte[_byteCount];
            Buffer.BlockCopy(_buffer, 0, bytes, 0, _byteCount);
            value = ReplayTapeValue.FromValidatedBytes(bytes, _entryCount);
            error = ReplayTapeError.None;
            return true;
        }

        /// <summary>設定上限を保ったまま空のtapeへ戻す。</summary>
        /// <returns>初期化結果。</returns>
        public ReplayTapeError Reset()
        {
            if (_disposed) return ReplayTapeError.Disposed;
            Initialize();
            return ReplayTapeError.None;
        }

        /// <summary>内部bufferを消去し、以後の変更操作を拒否する。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            Array.Clear(_buffer, 0, _buffer.Length);
            _buffer = Array.Empty<byte>();
            _byteCount = 0;
            _entryCount = 0;
            _lastTick = 0;
            _hasLastTick = false;
            _disposed = true;
        }

        private void Initialize()
        {
            _byteCount = ReplayTapeFormat.HeaderByteCount;
            _entryCount = 0;
            _lastTick = 0;
            _hasLastTick = false;
            ReplayTapeFormat.WriteEmptyHeader(_buffer);
        }

        private void EnsureCapacity(int requiredByteCount)
        {
            if (_buffer.Length >= requiredByteCount) return;
            var capacity = _buffer.Length;
            while (capacity < requiredByteCount)
            {
                capacity = capacity <= _maximumByteCount / 2 ? capacity * 2 : _maximumByteCount;
            }

            Array.Resize(ref _buffer, capacity);
        }
    }
}
