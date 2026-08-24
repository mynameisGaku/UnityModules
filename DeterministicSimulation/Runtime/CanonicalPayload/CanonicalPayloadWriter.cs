using System;

namespace CanonicalPayload
{
    /// <summary>利用側schema順の値を有界canonical byte列へ追加するwriter。</summary>
    public sealed class CanonicalPayloadWriter : IDisposable
    {
        /// <summary>既定のpayload上限。64 KiB。</summary>
        public const int DefaultMaximumByteCount = 64 * 1024;

        /// <summary>指定可能な最大payload上限。16 MiB。</summary>
        public const int MaximumSupportedByteCount = 16 * 1024 * 1024;

        private byte[] _buffer;
        private int _count;
        private bool _disposed;

        /// <summary>設定したpayload上限。</summary>
        public int MaximumByteCount { get; }

        /// <summary>現在追加済みのbyte数。破棄後は0。</summary>
        public int ByteCount => _disposed ? 0 : _count;

        /// <summary>writerが破棄済みか。</summary>
        public bool IsDisposed => _disposed;

        /// <summary>64 KiB上限の空writerを作る。</summary>
        public CanonicalPayloadWriter() : this(DefaultMaximumByteCount)
        {
        }

        /// <summary>指定byte上限の空writerを作る。</summary>
        /// <param name="maximumByteCount">0から16 MiBまでの上限。</param>
        /// <exception cref="ArgumentOutOfRangeException">上限が対応範囲外の場合。</exception>
        public CanonicalPayloadWriter(int maximumByteCount)
        {
            if (maximumByteCount < 0 || maximumByteCount > MaximumSupportedByteCount) throw new ArgumentOutOfRangeException(nameof(maximumByteCount));
            MaximumByteCount = maximumByteCount;
            _buffer = new byte[Math.Min(maximumByteCount, 256)];
        }

        /// <summary>booleanを0または1の1 byteで追加する。</summary>
        /// <param name="value">追加する値。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>追加できた場合にtrue。</returns>
        public bool TryWriteBoolean(bool value, out CanonicalPayloadError error)
        {
            if (!TryReserve(1, out error)) return false;
            _buffer[_count++] = value ? (byte)1 : (byte)0;
            return true;
        }

        /// <summary>signed 32-bit整数をlittle-endianで追加する。</summary>
        /// <param name="value">追加する値。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>追加できた場合にtrue。</returns>
        public bool TryWriteInt32(int value, out CanonicalPayloadError error) => TryWriteUInt32(unchecked((uint)value), out error);

        /// <summary>unsigned 32-bit整数をlittle-endianで追加する。</summary>
        /// <param name="value">追加する値。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>追加できた場合にtrue。</returns>
        public bool TryWriteUInt32(uint value, out CanonicalPayloadError error)
        {
            if (!TryReserve(4, out error)) return false;
            CanonicalPayloadEncoding.WriteUInt32(_buffer, _count, value);
            _count += 4;
            return true;
        }

        /// <summary>signed 64-bit整数をlittle-endianで追加する。</summary>
        /// <param name="value">追加する値。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>追加できた場合にtrue。</returns>
        public bool TryWriteInt64(long value, out CanonicalPayloadError error) => TryWriteUInt64(unchecked((ulong)value), out error);

        /// <summary>unsigned 64-bit整数をlittle-endianで追加する。</summary>
        /// <param name="value">追加する値。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>追加できた場合にtrue。</returns>
        public bool TryWriteUInt64(ulong value, out CanonicalPayloadError error)
        {
            if (!TryReserve(8, out error)) return false;
            CanonicalPayloadEncoding.WriteUInt64(_buffer, _count, value);
            _count += 8;
            return true;
        }

        /// <summary>IEEE 754 singleのbit表現をlittle-endianで追加する。</summary>
        /// <param name="value">追加する値。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>追加できた場合にtrue。</returns>
        public bool TryWriteSingle(float value, out CanonicalPayloadError error) => TryWriteInt32(BitConverter.SingleToInt32Bits(value), out error);

        /// <summary>IEEE 754 doubleのbit表現をlittle-endianで追加する。</summary>
        /// <param name="value">追加する値。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>追加できた場合にtrue。</returns>
        public bool TryWriteDouble(double value, out CanonicalPayloadError error) => TryWriteInt64(BitConverter.DoubleToInt64Bits(value), out error);

        /// <summary>uint32 byte長と厳格UTF-8 stringを追加する。</summary>
        /// <param name="value">追加する非null文字列。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>追加できた場合にtrue。</returns>
        public bool TryWriteString(string value, out CanonicalPayloadError error)
        {
            if (_disposed)
            {
                error = CanonicalPayloadError.Disposed;
                return false;
            }

            if (!CanonicalPayloadEncoding.TryGetUtf8Bytes(value, out var bytes))
            {
                error = value == null ? CanonicalPayloadError.InvalidInput : CanonicalPayloadError.InvalidUtf8;
                return false;
            }

            return TryWriteLengthPrefixed(bytes, out error);
        }

        /// <summary>uint32 byte長とcallerからcopyしたbyte列を追加する。</summary>
        /// <param name="value">追加するbyte列。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>追加できた場合にtrue。</returns>
        public bool TryWriteBytes(ReadOnlySpan<byte> value, out CanonicalPayloadError error)
        {
            if (_disposed)
            {
                error = CanonicalPayloadError.Disposed;
                return false;
            }

            return TryWriteLengthPrefixed(value, out error);
        }

        /// <summary>現在のbyte列からwriterと独立したimmutable valueを作る。</summary>
        /// <param name="value">成功時のpayload。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>作成できた場合にtrue。</returns>
        public bool TryBuild(out CanonicalPayloadValue value, out CanonicalPayloadError error)
        {
            value = default;
            if (_disposed)
            {
                error = CanonicalPayloadError.Disposed;
                return false;
            }

            var bytes = new byte[_count];
            if (_count > 0) Buffer.BlockCopy(_buffer, 0, bytes, 0, _count);
            value = CanonicalPayloadValue.FromOwnedBytes(bytes);
            error = CanonicalPayloadError.None;
            return true;
        }

        /// <summary>設定上限を保ったまま空payloadへ戻す。</summary>
        /// <returns>成功または破棄済みの結果。</returns>
        public CanonicalPayloadError Reset()
        {
            if (_disposed) return CanonicalPayloadError.Disposed;
            _count = 0;
            return CanonicalPayloadError.None;
        }

        /// <summary>bufferを解放する。複数回呼んでも安全。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _count = 0;
            _buffer = null;
        }

        private bool TryWriteLengthPrefixed(ReadOnlySpan<byte> value, out CanonicalPayloadError error)
        {
            if (value.Length > MaximumSupportedByteCount || MaximumByteCount < 4 || value.Length > MaximumByteCount - 4)
            {
                error = CanonicalPayloadError.CapacityExceeded;
                return false;
            }

            if (!TryReserve(4 + value.Length, out error)) return false;

            CanonicalPayloadEncoding.WriteUInt32(_buffer, _count, (uint)value.Length);
            value.CopyTo(_buffer.AsSpan(_count + 4, value.Length));
            _count += 4 + value.Length;
            error = CanonicalPayloadError.None;
            return true;
        }

        private bool TryReserve(int length, out CanonicalPayloadError error)
        {
            if (_disposed)
            {
                error = CanonicalPayloadError.Disposed;
                return false;
            }

            if (length < 0 || _count > MaximumByteCount - length)
            {
                error = CanonicalPayloadError.CapacityExceeded;
                return false;
            }

            var required = _count + length;
            if (_buffer.Length < required)
            {
                var capacity = Math.Min(MaximumByteCount, Math.Max(required, Math.Max(4, _buffer.Length * 2)));
                Array.Resize(ref _buffer, capacity);
            }

            error = CanonicalPayloadError.None;
            return true;
        }
    }
}
