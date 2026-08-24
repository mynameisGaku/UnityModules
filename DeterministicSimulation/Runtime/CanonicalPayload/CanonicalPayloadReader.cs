using System;

namespace CanonicalPayload
{
    /// <summary>immutable payloadを利用側schema順に読む独立cursor。</summary>
    public sealed class CanonicalPayloadReader
    {
        private readonly byte[] _bytes;
        private int _position;

        /// <summary>先頭からの現在位置。</summary>
        public int Position => _position;

        /// <summary>未読byte数。</summary>
        public int RemainingByteCount => _bytes.Length - _position;

        /// <summary>payload末尾へ到達したか。</summary>
        public bool IsAtEnd => _position == _bytes.Length;

        internal CanonicalPayloadReader(byte[] bytes)
        {
            _bytes = bytes;
        }

        /// <summary>0または1の1 byteからbooleanを読む。</summary>
        /// <param name="value">成功時の値。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>読めた場合にtrue。</returns>
        public bool TryReadBoolean(out bool value, out CanonicalPayloadError error)
        {
            value = false;
            if (!HasBytes(1))
            {
                error = CanonicalPayloadError.EndOfPayload;
                return false;
            }

            var encoded = _bytes[_position];
            if (encoded > 1)
            {
                error = CanonicalPayloadError.InvalidBoolean;
                return false;
            }

            value = encoded == 1;
            _position++;
            error = CanonicalPayloadError.None;
            return true;
        }

        /// <summary>little-endian signed 32-bit整数を読む。</summary>
        /// <param name="value">成功時の値。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>読めた場合にtrue。</returns>
        public bool TryReadInt32(out int value, out CanonicalPayloadError error)
        {
            value = 0;
            if (!TryReadUInt32(out var encoded, out error)) return false;
            value = unchecked((int)encoded);
            return true;
        }

        /// <summary>little-endian unsigned 32-bit整数を読む。</summary>
        /// <param name="value">成功時の値。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>読めた場合にtrue。</returns>
        public bool TryReadUInt32(out uint value, out CanonicalPayloadError error)
        {
            value = 0;
            if (!HasBytes(4))
            {
                error = CanonicalPayloadError.EndOfPayload;
                return false;
            }

            value = CanonicalPayloadEncoding.ReadUInt32(_bytes, _position);
            _position += 4;
            error = CanonicalPayloadError.None;
            return true;
        }

        /// <summary>little-endian signed 64-bit整数を読む。</summary>
        /// <param name="value">成功時の値。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>読めた場合にtrue。</returns>
        public bool TryReadInt64(out long value, out CanonicalPayloadError error)
        {
            value = 0;
            if (!TryReadUInt64(out var encoded, out error)) return false;
            value = unchecked((long)encoded);
            return true;
        }

        /// <summary>little-endian unsigned 64-bit整数を読む。</summary>
        /// <param name="value">成功時の値。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>読めた場合にtrue。</returns>
        public bool TryReadUInt64(out ulong value, out CanonicalPayloadError error)
        {
            value = 0;
            if (!HasBytes(8))
            {
                error = CanonicalPayloadError.EndOfPayload;
                return false;
            }

            value = CanonicalPayloadEncoding.ReadUInt64(_bytes, _position);
            _position += 8;
            error = CanonicalPayloadError.None;
            return true;
        }

        /// <summary>little-endian IEEE 754 singleを読む。</summary>
        /// <param name="value">成功時の値。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>読めた場合にtrue。</returns>
        public bool TryReadSingle(out float value, out CanonicalPayloadError error)
        {
            value = 0f;
            if (!TryReadInt32(out var bits, out error)) return false;
            value = BitConverter.Int32BitsToSingle(bits);
            return true;
        }

        /// <summary>little-endian IEEE 754 doubleを読む。</summary>
        /// <param name="value">成功時の値。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>読めた場合にtrue。</returns>
        public bool TryReadDouble(out double value, out CanonicalPayloadError error)
        {
            value = 0d;
            if (!TryReadInt64(out var bits, out error)) return false;
            value = BitConverter.Int64BitsToDouble(bits);
            return true;
        }

        /// <summary>uint32 byte長と厳格UTF-8からstringを読む。</summary>
        /// <param name="value">成功時の文字列。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>読めた場合にtrue。</returns>
        public bool TryReadString(out string value, out CanonicalPayloadError error)
        {
            value = null;
            if (!TryPeekLength(out var length, out error)) return false;
            if (!CanonicalPayloadEncoding.TryGetString(_bytes, _position + 4, length, out value))
            {
                error = CanonicalPayloadError.InvalidUtf8;
                return false;
            }

            _position += 4 + length;
            error = CanonicalPayloadError.None;
            return true;
        }

        /// <summary>uint32 byte長に続くcaller所有byte列を読む。</summary>
        /// <param name="value">成功時のbyte列copy。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>読めた場合にtrue。</returns>
        public bool TryReadBytes(out byte[] value, out CanonicalPayloadError error)
        {
            value = null;
            if (!TryPeekLength(out var length, out error)) return false;
            value = new byte[length];
            if (length > 0) Buffer.BlockCopy(_bytes, _position + 4, value, 0, length);
            _position += 4 + length;
            error = CanonicalPayloadError.None;
            return true;
        }

        private bool TryPeekLength(out int length, out CanonicalPayloadError error)
        {
            length = 0;
            if (!HasBytes(4))
            {
                error = CanonicalPayloadError.EndOfPayload;
                return false;
            }

            var encoded = CanonicalPayloadEncoding.ReadUInt32(_bytes, _position);
            if (encoded > int.MaxValue || encoded > (uint)(RemainingByteCount - 4))
            {
                error = CanonicalPayloadError.InvalidLength;
                return false;
            }

            length = (int)encoded;
            error = CanonicalPayloadError.None;
            return true;
        }

        private bool HasBytes(int count) => count >= 0 && _position <= _bytes.Length - count;
    }
}
