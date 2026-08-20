using System;

namespace CanonicalPayload
{
    /// <summary>検証済みcanonical payload byte列。</summary>
    public readonly struct CanonicalPayloadValue : IEquatable<CanonicalPayloadValue>
    {
        private readonly byte[] _bytes;

        /// <summary>valueが検証済みbyte列を持つか。</summary>
        public bool IsValid => _bytes != null;

        /// <summary>payload byte数。無効値では0。</summary>
        public int ByteCount => _bytes?.Length ?? 0;

        private CanonicalPayloadValue(byte[] bytes)
        {
            _bytes = bytes;
        }

        /// <summary>既定64 KiB上限でcaller byte列をcopyしてvalueを作る。</summary>
        /// <param name="bytes">copyするbyte列。</param>
        /// <param name="value">成功時のpayload。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>作成できた場合にtrue。</returns>
        public static bool TryCreate(ReadOnlySpan<byte> bytes, out CanonicalPayloadValue value, out CanonicalPayloadError error)
        {
            return TryCreate(bytes, CanonicalPayloadWriter.DefaultMaximumByteCount, out value, out error);
        }

        /// <summary>指定上限でcaller byte列をcopyしてvalueを作る。</summary>
        /// <param name="bytes">copyするbyte列。</param>
        /// <param name="maximumByteCount">許可する最大byte数。</param>
        /// <param name="value">成功時のpayload。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>作成できた場合にtrue。</returns>
        public static bool TryCreate(ReadOnlySpan<byte> bytes, int maximumByteCount, out CanonicalPayloadValue value, out CanonicalPayloadError error)
        {
            value = default;
            if (maximumByteCount < 0 || maximumByteCount > CanonicalPayloadWriter.MaximumSupportedByteCount)
            {
                error = CanonicalPayloadError.InvalidInput;
                return false;
            }

            if (bytes.Length > maximumByteCount)
            {
                error = CanonicalPayloadError.CapacityExceeded;
                return false;
            }

            value = new CanonicalPayloadValue(bytes.ToArray());
            error = CanonicalPayloadError.None;
            return true;
        }

        /// <summary>先頭から読む独立readerを作る。</summary>
        /// <param name="reader">成功時のreader。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>作成できた場合にtrue。</returns>
        public bool TryCreateReader(out CanonicalPayloadReader reader, out CanonicalPayloadError error)
        {
            reader = null;
            if (_bytes == null)
            {
                error = CanonicalPayloadError.InvalidInput;
                return false;
            }

            reader = new CanonicalPayloadReader(_bytes);
            error = CanonicalPayloadError.None;
            return true;
        }

        /// <summary>callerが所有するbyte列copyを返す。無効値では空配列。</summary>
        /// <returns>payload byte列のcopy。</returns>
        public byte[] ToByteArray()
        {
            if (_bytes == null) return Array.Empty<byte>();
            var copy = new byte[_bytes.Length];
            Buffer.BlockCopy(_bytes, 0, copy, 0, copy.Length);
            return copy;
        }

        /// <summary>全canonical byteが一致するかを返す。</summary>
        /// <param name="other">比較するpayload。</param>
        /// <returns>一致する場合にtrue。</returns>
        public bool Equals(CanonicalPayloadValue other)
        {
            if (_bytes == null || other._bytes == null) return _bytes == other._bytes;
            return _bytes.AsSpan().SequenceEqual(other._bytes);
        }

        /// <summary>指定objectが同じpayloadかを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じpayloadの場合にtrue。</returns>
        public override bool Equals(object obj) => obj is CanonicalPayloadValue other && Equals(other);

        /// <summary>payload内容からhash codeを返す。</summary>
        /// <returns>内容に基づくhash code。</returns>
        public override int GetHashCode()
        {
            if (_bytes == null) return 0;
            unchecked
            {
                var hash = 17;
                for (var index = 0; index < _bytes.Length; index++) hash = (hash * 31) + _bytes[index];
                return hash;
            }
        }

        /// <summary>2つのpayloadが一致するかを返す。</summary>
        /// <param name="left">左辺payload。</param>
        /// <param name="right">右辺payload。</param>
        /// <returns>一致する場合にtrue。</returns>
        public static bool operator ==(CanonicalPayloadValue left, CanonicalPayloadValue right) => left.Equals(right);

        /// <summary>2つのpayloadが異なるかを返す。</summary>
        /// <param name="left">左辺payload。</param>
        /// <param name="right">右辺payload。</param>
        /// <returns>異なる場合にtrue。</returns>
        public static bool operator !=(CanonicalPayloadValue left, CanonicalPayloadValue right) => !left.Equals(right);

        internal static CanonicalPayloadValue FromOwnedBytes(byte[] bytes) => new CanonicalPayloadValue(bytes);
    }
}
