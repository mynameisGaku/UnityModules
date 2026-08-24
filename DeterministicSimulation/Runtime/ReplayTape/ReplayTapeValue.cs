using System;

namespace ReplayTape
{
    /// <summary>完全検証済みversion 1 canonical Replay Tape。</summary>
    public readonly struct ReplayTapeValue : IEquatable<ReplayTapeValue>
    {
        /// <summary>canonical形式version。</summary>
        public const int FormatVersion = ReplayTapeFormat.Version;

        private readonly byte[] _bytes;

        /// <summary>tapeが検証済みbyte列を持つか。</summary>
        public bool IsValid => _bytes != null;

        /// <summary>格納entry数。無効値では0。</summary>
        public int EntryCount { get; }

        /// <summary>headerを含むbyte数。無効値では0。</summary>
        public int ByteCount => _bytes?.Length ?? 0;

        private ReplayTapeValue(byte[] bytes, int entryCount)
        {
            _bytes = bytes;
            EntryCount = entryCount;
        }

        /// <summary>canonical byte列を完全検証し、入力から独立したtapeを作る。</summary>
        /// <param name="bytes">headerを含むversion 1 byte列。</param>
        /// <param name="value">成功時のtape。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>検証できた場合にtrue。</returns>
        public static bool TryParse(ReadOnlySpan<byte> bytes, out ReplayTapeValue value, out ReplayTapeError error)
        {
            value = default;
            error = ReplayTapeFormat.Validate(bytes, out var entryCount);
            if (error != ReplayTapeError.None) return false;
            value = new ReplayTapeValue(bytes.ToArray(), entryCount);
            return true;
        }

        /// <summary>先頭entryから読む独立readerを作る。</summary>
        /// <param name="reader">成功時のreader。</param>
        /// <param name="error">無効値の場合の理由。</param>
        /// <returns>readerを作れた場合にtrue。</returns>
        public bool TryCreateReader(out ReplayTapeReader reader, out ReplayTapeError error)
        {
            reader = null;
            if (_bytes == null)
            {
                error = ReplayTapeError.InvalidHeader;
                return false;
            }

            reader = new ReplayTapeReader(_bytes, EntryCount);
            error = ReplayTapeError.None;
            return true;
        }

        /// <summary>canonical byte列のcopyを返す。無効値では空配列。</summary>
        /// <returns>callerが所有するbyte配列。</returns>
        public byte[] ToByteArray()
        {
            if (_bytes == null) return Array.Empty<byte>();
            var copy = new byte[_bytes.Length];
            Buffer.BlockCopy(_bytes, 0, copy, 0, copy.Length);
            return copy;
        }

        /// <summary>entry数と全canonical byteが一致するかを返す。</summary>
        /// <param name="other">比較するtape。</param>
        /// <returns>内容が一致する場合にtrue。</returns>
        public bool Equals(ReplayTapeValue other)
        {
            if (EntryCount != other.EntryCount) return false;
            if (_bytes == null || other._bytes == null) return _bytes == other._bytes;
            return _bytes.AsSpan().SequenceEqual(other._bytes);
        }

        /// <summary>指定objectが同じtapeかを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じtapeの場合にtrue。</returns>
        public override bool Equals(object obj) => obj is ReplayTapeValue other && Equals(other);

        /// <summary>tape内容からhash codeを返す。</summary>
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

        /// <summary>2つのtapeが一致するかを返す。</summary>
        /// <param name="left">左辺tape。</param>
        /// <param name="right">右辺tape。</param>
        /// <returns>一致する場合にtrue。</returns>
        public static bool operator ==(ReplayTapeValue left, ReplayTapeValue right) => left.Equals(right);

        /// <summary>2つのtapeが異なるかを返す。</summary>
        /// <param name="left">左辺tape。</param>
        /// <param name="right">右辺tape。</param>
        /// <returns>異なる場合にtrue。</returns>
        public static bool operator !=(ReplayTapeValue left, ReplayTapeValue right) => !left.Equals(right);

        internal static ReplayTapeValue FromValidatedBytes(byte[] bytes, int entryCount) => new ReplayTapeValue(bytes, entryCount);
    }
}
