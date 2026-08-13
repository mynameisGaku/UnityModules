using System;
using System.Globalization;
using UnityEngine;

namespace Containers
{
    /// <summary>
    /// Unity がシリアライズできる 128 ビットの識別子。int 4 本で持つので値型のままで、
    /// 文字列の確保も比較のたびのパースも起きない。
    /// <para>
    /// 名前の変更・並び替え・シーンの再読み込みをまたいで同一性を保つ必要があるものに使う ——
    /// セーブデータから配置済みオブジェクトを指す、クエストやアイテムの ID、通信のハンドシェイク。
    /// </para>
    /// </summary>
    [Serializable]
    public struct SerializableGuid : IEquatable<SerializableGuid>, IComparable<SerializableGuid>
    {
        /// <summary>未設定を表す値。</summary>
        public static readonly SerializableGuid Empty = default;

        [SerializeField] private int _a;
        [SerializeField] private int _b;
        [SerializeField] private int _c;
        [SerializeField] private int _d;

        /// <summary><see cref="Guid"/> から作る。</summary>
        /// <param name="guid">元にする GUID。</param>
        public SerializableGuid(Guid guid)
        {
            var bytes = guid.ToByteArray();
            _a = BitConverter.ToInt32(bytes, 0);
            _b = BitConverter.ToInt32(bytes, 4);
            _c = BitConverter.ToInt32(bytes, 8);
            _d = BitConverter.ToInt32(bytes, 12);
        }

        /// <summary>4 つの int から直接作る。</summary>
        /// <param name="a">1 番目。</param>
        /// <param name="b">2 番目。</param>
        /// <param name="c">3 番目。</param>
        /// <param name="d">4 番目。</param>
        public SerializableGuid(int a, int b, int c, int d)
        {
            _a = a;
            _b = b;
            _c = c;
            _d = d;
        }

        /// <summary>未設定かどうか。</summary>
        public bool IsEmpty => (_a | _b | _c | _d) == 0;

        /// <summary>新しい ID を生成する。</summary>
        public static SerializableGuid NewGuid() => new SerializableGuid(Guid.NewGuid());

        /// <summary><see cref="Guid"/> に変換する。</summary>
        public Guid ToGuid()
        {
            var bytes = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes(_a), 0, bytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(_b), 0, bytes, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(_c), 0, bytes, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(_d), 0, bytes, 12, 4);
            return new Guid(bytes);
        }

        /// <summary>文字列から解釈する。</summary>
        /// <param name="text">GUID の文字列表現。</param>
        /// <param name="guid">解釈できた値。</param>
        public static bool TryParse(string text, out SerializableGuid guid)
        {
            if (Guid.TryParse(text, out var parsed))
            {
                guid = new SerializableGuid(parsed);
                return true;
            }

            guid = Empty;
            return false;
        }

        /// <summary>Unity のアセット GUID と同じ 32 文字の 16 進表記。</summary>
        public string ToHexString() => ToGuid().ToString("N", CultureInfo.InvariantCulture);

        /// <summary>4 つの int が全て一致するか。</summary>
        /// <param name="other">比較対象。</param>
        public bool Equals(SerializableGuid other) => _a == other._a && _b == other._b && _c == other._c && _d == other._d;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SerializableGuid other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = _a;
                hash = (hash * 397) ^ _b;
                hash = (hash * 397) ^ _c;
                hash = (hash * 397) ^ _d;
                return hash;
            }
        }

        /// <summary>安定した順序付け。辞書のキーや整列に使える。</summary>
        /// <param name="other">比較対象。</param>
        public int CompareTo(SerializableGuid other)
        {
            var order = _a.CompareTo(other._a);
            if (order != 0) return order;
            order = _b.CompareTo(other._b);
            if (order != 0) return order;
            order = _c.CompareTo(other._c);
            return order != 0 ? order : _d.CompareTo(other._d);
        }

        /// <inheritdoc/>
        public override string ToString() => IsEmpty ? "<empty>" : ToGuid().ToString("D", CultureInfo.InvariantCulture);

        /// <summary>等値比較。</summary>
        public static bool operator ==(SerializableGuid a, SerializableGuid b) => a.Equals(b);

        /// <summary>非等値比較。</summary>
        public static bool operator !=(SerializableGuid a, SerializableGuid b) => !a.Equals(b);

        /// <summary><see cref="Guid"/> への暗黙変換。</summary>
        public static implicit operator Guid(SerializableGuid guid) => guid.ToGuid();

        /// <summary><see cref="Guid"/> からの暗黙変換。</summary>
        public static implicit operator SerializableGuid(Guid guid) => new SerializableGuid(guid);
    }
}
