using System;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// 文字列を一度だけ整数に畳み込み、以後は整数として比較・ハッシュする識別子。
    /// <para>
    /// 文字列キーは書くのは楽で使うのは高い —— 辞書を引くたびに文字列全体をハッシュし、
    /// 比較のたびに走査する。読み込み時に変換してしまえば
    /// （<c>Animator.StringToHash</c> と同じ手口）、書きやすさは保ったままコストが消える。
    /// 元の文字列はログと Inspector 表示のために覚えておく。
    /// </para>
    /// </summary>
    public readonly struct StringId : IEquatable<StringId>, IComparable<StringId>
    {
        /// <summary>無効な識別子。</summary>
        public static readonly StringId None = default;

        /// <summary>割り当てられた整数値。</summary>
        public readonly int Value;

        private StringId(int value) => Value = value;

        /// <summary>有効な識別子か。</summary>
        public bool IsValid => Value != 0;

        /// <summary>文字列を登録して識別子を得る。同じ文字列なら常に同じ識別子。</summary>
        /// <param name="text">元になる文字列。</param>
        public static StringId Get(string text) => new StringId(StringIdRegistry.Intern(text));

        /// <summary>登録済みのものだけを探す。未登録なら <see cref="None"/>。</summary>
        /// <param name="text">探す文字列。</param>
        public static StringId Find(string text) => new StringId(StringIdRegistry.Find(text));

        /// <summary>元になった文字列。<see cref="None"/> なら null。</summary>
        public string Text => StringIdRegistry.TextOf(Value);

        /// <summary>整数値が一致するか。</summary>
        /// <param name="other">比較対象。</param>
        public bool Equals(StringId other) => Value == other.Value;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is StringId other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Value;

        /// <summary>登録順による安定した順序付け。文字列の辞書順ではない。</summary>
        /// <param name="other">比較対象。</param>
        public int CompareTo(StringId other) => Value.CompareTo(other.Value);

        /// <inheritdoc/>
        public override string ToString() => Text ?? "<none>";

        /// <summary>等値比較。</summary>
        public static bool operator ==(StringId a, StringId b) => a.Value == b.Value;

        /// <summary>非等値比較。</summary>
        public static bool operator !=(StringId a, StringId b) => a.Value != b.Value;

        /// <summary>文字列からの暗黙変換。登録も行う。</summary>
        public static implicit operator StringId(string text) => Get(text);
    }

    /// <summary>
    /// <see cref="StringId"/> の対応表。
    /// <para>
    /// 識別子はプロセスが生きている間は安定だが<b>実行をまたぐと変わる</b>ので、
    /// 保存するときは識別子ではなく文字列の方を保存すること。
    /// </para>
    /// </summary>
    public static class StringIdRegistry
    {
        private static readonly Dictionary<string, int> Ids = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly List<string> Texts = new List<string> { null };
        private static readonly object Gate = new object();

        /// <summary>登録されている文字列の数。</summary>
        public static int Count
        {
            get
            {
                lock (Gate) return Texts.Count - 1;
            }
        }

        internal static int Intern(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            lock (Gate)
            {
                if (Ids.TryGetValue(text, out var id)) return id;

                id = Texts.Count;
                Texts.Add(text);
                Ids.Add(text, id);
                return id;
            }
        }

        internal static int Find(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            lock (Gate) return Ids.TryGetValue(text, out var id) ? id : 0;
        }

        internal static string TextOf(int id)
        {
            lock (Gate) return (uint)id < (uint)Texts.Count ? Texts[id] : null;
        }
    }

    /// <summary>
    /// 登録済み文字列をキーにする辞書。書くときは文字列、引くときは整数。
    /// </summary>
    public sealed class StringKeyMap<TValue>
    {
        private readonly Dictionary<int, TValue> _map = new Dictionary<int, TValue>();

        /// <summary>要素数。</summary>
        public int Count => _map.Count;

        /// <summary>識別子で値を読み書きする。</summary>
        public TValue this[StringId key]
        {
            get => _map[key.Value];
            set => _map[key.Value] = value;
        }

        /// <summary>文字列で値を読み書きする。書き込み時に文字列が登録される。</summary>
        public TValue this[string key]
        {
            get => _map[StringId.Get(key).Value];
            set => _map[StringId.Get(key).Value] = value;
        }

        /// <summary>文字列をキーに追加する。</summary>
        /// <param name="key">キーとなる文字列。</param>
        /// <param name="value">値。</param>
        public void Add(string key, TValue value) => _map.Add(StringId.Get(key).Value, value);

        /// <summary>識別子をキーに追加する。</summary>
        /// <param name="key">キーとなる識別子。</param>
        /// <param name="value">値。</param>
        public void Add(StringId key, TValue value) => _map.Add(key.Value, value);

        /// <summary>識別子で値を読む。</summary>
        /// <param name="key">キー。</param>
        /// <param name="value">読み取った値。</param>
        public bool TryGetValue(StringId key, out TValue value) => _map.TryGetValue(key.Value, out value);

        /// <summary>
        /// 文字列で値を読む。見つからない場合でも<b>その文字列を登録しない</b>ので、
        /// 未知の文字列で探り続けても対応表が膨らまない。
        /// </summary>
        /// <param name="key">探す文字列。</param>
        /// <param name="value">読み取った値。</param>
        public bool TryGetValue(string key, out TValue value)
        {
            var id = StringId.Find(key);
            if (!id.IsValid)
            {
                value = default;
                return false;
            }

            return _map.TryGetValue(id.Value, out value);
        }

        /// <summary>キーが登録されているか。</summary>
        /// <param name="key">キー。</param>
        public bool ContainsKey(StringId key) => _map.ContainsKey(key.Value);

        /// <summary>エントリを削除する。</summary>
        /// <param name="key">キー。</param>
        public bool Remove(StringId key) => _map.Remove(key.Value);

        /// <summary>全て捨てる。</summary>
        public void Clear() => _map.Clear();

        /// <summary>(識別子, 値) の組を列挙する。</summary>
        public IEnumerable<KeyValuePair<StringId, TValue>> Entries()
        {
            foreach (var pair in _map)
            {
                yield return new KeyValuePair<StringId, TValue>(StringId.Find(StringIdRegistry.TextOf(pair.Key)), pair.Value);
            }
        }
    }
}
