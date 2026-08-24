using System;
using System.Security.Cryptography;
using System.Text;

namespace StateFingerprint
{
    /// <summary>型・field id・値を明示した順序でcanonical化し、再現可能なSHA-256 fingerprintを作る。</summary>
    public sealed class StateFingerprintBuilder : IDisposable
    {
        /// <summary>既定で許可するcanonical byte数。</summary>
        public const int DefaultMaximumByteCount = 1024 * 1024;

        /// <summary>1つのbuilderへ設定できる最大canonical byte数。</summary>
        public const int MaximumAllowedByteCount = 16 * 1024 * 1024;

        private const int HeaderByteCount = 4;
        private const int RecordHeaderByteCount = 9;
        private const byte NullTag = 0;
        private const byte BooleanTag = 1;
        private const byte Int32Tag = 2;
        private const byte UInt32Tag = 3;
        private const byte Int64Tag = 4;
        private const byte UInt64Tag = 5;
        private const byte SingleTag = 6;
        private const byte DoubleTag = 7;
        private const byte StringTag = 8;
        private const byte BytesTag = 9;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly int _maximumByteCount;
        private byte[] _buffer;
        private int _byteCount;
        private int _operationCount;
        private bool _disposed;

        /// <summary>指定上限を持つ空のversion 1 builderを作る。</summary>
        /// <param name="maximumByteCount">headerを含むcanonical byte列の上限。</param>
        public StateFingerprintBuilder(int maximumByteCount = DefaultMaximumByteCount)
        {
            if (maximumByteCount < HeaderByteCount || maximumByteCount > MaximumAllowedByteCount)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumByteCount));
            }

            _maximumByteCount = maximumByteCount;
            _buffer = new byte[Math.Min(maximumByteCount, 256)];
            InitializeHeader();
        }

        /// <summary>headerを含む現在のcanonical byte数。</summary>
        public int ByteCount => _byteCount;

        /// <summary>追加済みfield操作数。</summary>
        public int OperationCount => _operationCount;

        /// <summary>このbuilderが許可するcanonical byte数。</summary>
        public int MaximumByteCount => _maximumByteCount;

        /// <summary>破棄済みかを返す。</summary>
        public bool IsDisposed => _disposed;

        /// <summary>明示的なnull fieldを追加する。</summary>
        public StateFingerprintError WriteNull(uint fieldId) => WriteEmptyRecord(NullTag, fieldId);

        /// <summary>booleanを1 byteの0または1として追加する。</summary>
        public StateFingerprintError WriteBoolean(uint fieldId, bool value)
        {
            var error = BeginRecord(BooleanTag, fieldId, 1, out var payloadOffset);
            if (error != StateFingerprintError.None) return error;
            _buffer[payloadOffset] = value ? (byte)1 : (byte)0;
            return StateFingerprintError.None;
        }

        /// <summary>signed 32-bit整数をlittle-endianで追加する。</summary>
        public StateFingerprintError WriteInt32(uint fieldId, int value) => WriteUInt32Record(Int32Tag, fieldId, unchecked((uint)value));

        /// <summary>unsigned 32-bit整数をlittle-endianで追加する。</summary>
        public StateFingerprintError WriteUInt32(uint fieldId, uint value) => WriteUInt32Record(UInt32Tag, fieldId, value);

        /// <summary>signed 64-bit整数をlittle-endianで追加する。</summary>
        public StateFingerprintError WriteInt64(uint fieldId, long value) => WriteUInt64Record(Int64Tag, fieldId, unchecked((ulong)value));

        /// <summary>unsigned 64-bit整数をlittle-endianで追加する。</summary>
        public StateFingerprintError WriteUInt64(uint fieldId, ulong value) => WriteUInt64Record(UInt64Tag, fieldId, value);

        /// <summary>singleのraw IEEE 754 bit列をlittle-endianで追加する。</summary>
        public StateFingerprintError WriteSingle(uint fieldId, float value) => WriteUInt32Record(SingleTag, fieldId, unchecked((uint)BitConverter.SingleToInt32Bits(value)));

        /// <summary>doubleのraw IEEE 754 bit列をlittle-endianで追加する。</summary>
        public StateFingerprintError WriteDouble(uint fieldId, double value) => WriteUInt64Record(DoubleTag, fieldId, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));

        /// <summary>文字列を正しいUnicode scalar列から作るBOMなしUTF-8として追加する。</summary>
        /// <remarks>nullまたは不正なsurrogate列は拒否し、builderを変更しない。</remarks>
        public StateFingerprintError WriteString(uint fieldId, string value)
        {
            if (_disposed) return StateFingerprintError.Disposed;
            if (value == null) return StateFingerprintError.InvalidInput;

            int byteCount;
            try
            {
                byteCount = StrictUtf8.GetByteCount(value);
            }
            catch (EncoderFallbackException)
            {
                return StateFingerprintError.InvalidInput;
            }

            if (!CanAppendPayload(byteCount)) return StateFingerprintError.CapacityExceeded;
            var bytes = StrictUtf8.GetBytes(value);
            return WritePayloadRecord(StringTag, fieldId, bytes);
        }

        /// <summary>byte列のcopyを長さ付きfieldとして追加する。</summary>
        /// <remarks>nullは拒否し、空配列は有効な値として扱う。</remarks>
        public StateFingerprintError WriteBytes(uint fieldId, byte[] value)
        {
            if (_disposed) return StateFingerprintError.Disposed;
            if (value == null) return StateFingerprintError.InvalidInput;
            return WritePayloadRecord(BytesTag, fieldId, value);
        }

        /// <summary>現在のcanonical byte列からfingerprintを作る。builderの位置は変更しない。</summary>
        public bool TryBuild(out StateFingerprintValue fingerprint, out StateFingerprintError error)
        {
            fingerprint = default;
            if (_disposed)
            {
                error = StateFingerprintError.Disposed;
                return false;
            }

            using var sha256 = SHA256.Create();
            var digest = sha256.ComputeHash(_buffer, 0, _byteCount);
            fingerprint = StateFingerprintValue.FromDigest(digest);
            error = StateFingerprintError.None;
            return true;
        }

        /// <summary>同じ上限の空builderへ戻す。</summary>
        public StateFingerprintError Reset()
        {
            if (_disposed) return StateFingerprintError.Disposed;
            InitializeHeader();
            return StateFingerprintError.None;
        }

        /// <summary>内部bufferを切り離す。複数回呼んでも結果は変わらない。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _buffer = Array.Empty<byte>();
            _byteCount = 0;
            _operationCount = 0;
        }

        private StateFingerprintError WriteEmptyRecord(byte tag, uint fieldId) => BeginRecord(tag, fieldId, 0, out _);

        private StateFingerprintError WriteUInt32Record(byte tag, uint fieldId, uint value)
        {
            var error = BeginRecord(tag, fieldId, 4, out var payloadOffset);
            if (error != StateFingerprintError.None) return error;
            WriteUInt32LittleEndian(_buffer, payloadOffset, value);
            return StateFingerprintError.None;
        }

        private StateFingerprintError WriteUInt64Record(byte tag, uint fieldId, ulong value)
        {
            var error = BeginRecord(tag, fieldId, 8, out var payloadOffset);
            if (error != StateFingerprintError.None) return error;
            WriteUInt64LittleEndian(_buffer, payloadOffset, value);
            return StateFingerprintError.None;
        }

        private StateFingerprintError WritePayloadRecord(byte tag, uint fieldId, byte[] value)
        {
            var error = BeginRecord(tag, fieldId, value.Length, out var payloadOffset);
            if (error != StateFingerprintError.None) return error;
            if (value.Length > 0) Buffer.BlockCopy(value, 0, _buffer, payloadOffset, value.Length);
            return StateFingerprintError.None;
        }

        private StateFingerprintError BeginRecord(byte tag, uint fieldId, int payloadByteCount, out int payloadOffset)
        {
            payloadOffset = 0;
            if (_disposed) return StateFingerprintError.Disposed;
            if (!CanAppendPayload(payloadByteCount)) return StateFingerprintError.CapacityExceeded;

            var recordByteCount = RecordHeaderByteCount + payloadByteCount;
            var recordOffset = _byteCount;
            payloadOffset = recordOffset + RecordHeaderByteCount;
            EnsureBufferCapacity(_byteCount + recordByteCount);
            _buffer[recordOffset] = tag;
            WriteUInt32LittleEndian(_buffer, recordOffset + 1, fieldId);
            WriteUInt32LittleEndian(_buffer, recordOffset + 5, (uint)payloadByteCount);
            _byteCount += recordByteCount;
            _operationCount++;
            return StateFingerprintError.None;
        }

        private bool CanAppendPayload(int payloadByteCount)
        {
            if (payloadByteCount < 0 || payloadByteCount > _maximumByteCount - RecordHeaderByteCount) return false;
            return _byteCount <= _maximumByteCount - RecordHeaderByteCount - payloadByteCount;
        }

        private void InitializeHeader()
        {
            _buffer[0] = (byte)'S';
            _buffer[1] = (byte)'F';
            _buffer[2] = (byte)'P';
            _buffer[3] = StateFingerprintValue.FormatVersion;
            _byteCount = HeaderByteCount;
            _operationCount = 0;
        }

        private void EnsureBufferCapacity(int required)
        {
            if (_buffer.Length >= required) return;
            var capacity = _buffer.Length;
            while (capacity < required) capacity = Math.Min(_maximumByteCount, capacity * 2);
            Array.Resize(ref _buffer, capacity);
        }

        private static void WriteUInt32LittleEndian(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64LittleEndian(byte[] bytes, int offset, ulong value)
        {
            for (var i = 0; i < 8; i++)
            {
                bytes[offset + i] = (byte)value;
                value >>= 8;
            }
        }
    }
}
