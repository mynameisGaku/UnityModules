using System;

namespace StateFingerprint
{
    /// <summary>canonical field列から作ったversion 1の256-bit SHA-256 fingerprint。</summary>
    public readonly struct StateFingerprintValue : IEquatable<StateFingerprintValue>
    {
        /// <summary>fingerprintのbyte数。</summary>
        public const int Length = 32;

        /// <summary>canonical形式とhash algorithmのversion。</summary>
        public const int FormatVersion = 1;

        /// <summary>digest先頭の64-bit word。</summary>
        public ulong Word0 { get; }

        /// <summary>digestの2番目の64-bit word。</summary>
        public ulong Word1 { get; }

        /// <summary>digestの3番目の64-bit word。</summary>
        public ulong Word2 { get; }

        /// <summary>digest末尾の64-bit word。</summary>
        public ulong Word3 { get; }

        private StateFingerprintValue(ulong word0, ulong word1, ulong word2, ulong word3)
        {
            Word0 = word0;
            Word1 = word1;
            Word2 = word2;
            Word3 = word3;
        }

        /// <summary>fingerprintをnetwork byte orderの32-byte配列として返す。</summary>
        public byte[] ToByteArray()
        {
            var bytes = new byte[Length];
            WriteUInt64BigEndian(bytes, 0, Word0);
            WriteUInt64BigEndian(bytes, 8, Word1);
            WriteUInt64BigEndian(bytes, 16, Word2);
            WriteUInt64BigEndian(bytes, 24, Word3);
            return bytes;
        }

        /// <summary>64桁の大文字・小文字hex文字列をfingerprintへ変換する。</summary>
        /// <param name="text">変換する64桁hex文字列。</param>
        /// <param name="value">成功時のfingerprint。</param>
        /// <returns>形式が正しい場合にtrue。</returns>
        public static bool TryParse(string text, out StateFingerprintValue value)
        {
            value = default;
            if (text == null || text.Length != Length * 2) return false;

            var bytes = new byte[Length];
            for (var i = 0; i < bytes.Length; i++)
            {
                var high = ParseNibble(text[i * 2]);
                var low = ParseNibble(text[(i * 2) + 1]);
                if (high < 0 || low < 0) return false;
                bytes[i] = (byte)((high << 4) | low);
            }

            value = FromDigest(bytes);
            return true;
        }

        /// <summary>fingerprintを小文字64桁hex文字列として返す。</summary>
        public override string ToString()
        {
            const string hex = "0123456789abcdef";
            var bytes = ToByteArray();
            var characters = new char[bytes.Length * 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                characters[i * 2] = hex[bytes[i] >> 4];
                characters[(i * 2) + 1] = hex[bytes[i] & 0x0f];
            }

            return new string(characters);
        }

        /// <summary>4つのwordがすべて一致するかを返す。</summary>
        public bool Equals(StateFingerprintValue other) =>
            Word0 == other.Word0 && Word1 == other.Word1 && Word2 == other.Word2 && Word3 == other.Word3;

        /// <summary>指定objectが同じfingerprintかを返す。</summary>
        public override bool Equals(object obj) => obj is StateFingerprintValue other && Equals(other);

        /// <summary>4つのwordからprocess内比較用hash codeを作る。</summary>
        public override int GetHashCode() => HashCode.Combine(Word0, Word1, Word2, Word3);

        /// <summary>2つのfingerprintが一致するかを返す。</summary>
        public static bool operator ==(StateFingerprintValue left, StateFingerprintValue right) => left.Equals(right);

        /// <summary>2つのfingerprintが異なるかを返す。</summary>
        public static bool operator !=(StateFingerprintValue left, StateFingerprintValue right) => !left.Equals(right);

        /// <summary>SHA-256の32-byte digestを値へ変換する。</summary>
        internal static StateFingerprintValue FromDigest(byte[] digest)
        {
            if (digest == null || digest.Length != Length) throw new ArgumentException("SHA-256 digestは32 bytesである必要があります。", nameof(digest));
            return new StateFingerprintValue(
                ReadUInt64BigEndian(digest, 0),
                ReadUInt64BigEndian(digest, 8),
                ReadUInt64BigEndian(digest, 16),
                ReadUInt64BigEndian(digest, 24));
        }

        private static ulong ReadUInt64BigEndian(byte[] bytes, int offset)
        {
            ulong value = 0;
            for (var i = 0; i < 8; i++) value = (value << 8) | bytes[offset + i];
            return value;
        }

        private static void WriteUInt64BigEndian(byte[] bytes, int offset, ulong value)
        {
            for (var i = 7; i >= 0; i--)
            {
                bytes[offset + i] = (byte)value;
                value >>= 8;
            }
        }

        private static int ParseNibble(char value)
        {
            if (value >= '0' && value <= '9') return value - '0';
            if (value >= 'a' && value <= 'f') return value - 'a' + 10;
            if (value >= 'A' && value <= 'F') return value - 'A' + 10;
            return -1;
        }
    }
}
