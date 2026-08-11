using System;
using System.Collections;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// 多重集合 —— 何がいくつあるか。所持アイテムのスタック、資源の総量、
    /// ダメージの集計、ドロップの統計。
    /// <para>
    /// 個数は負にもできるが、<see cref="TryTake"/> は持っている以上を消費しない ——
    /// インベントリが本当に欲しいのはこちらの挙動。
    /// </para>
    /// </summary>
    public sealed class Counter<T> : IEnumerable<KeyValuePair<T, int>>
    {
        private readonly Dictionary<T, int> _counts;

        /// <summary>要素の比較方法を指定して作る。</summary>
        /// <param name="comparer">比較方法。</param>
        public Counter(IEqualityComparer<T> comparer = null) => _counts = new Dictionary<T, int>(comparer);

        /// <summary>持っている種類の数。個数の合計ではない。</summary>
        public int DistinctCount => _counts.Count;

        /// <summary>持っている種類の一覧。</summary>
        public Dictionary<T, int>.KeyCollection Items => _counts.Keys;

        /// <summary>個数。持っていなければ 0。0 を設定するとエントリごと消える。</summary>
        public int this[T item]
        {
            get => _counts.TryGetValue(item, out var count) ? count : 0;
            set
            {
                if (value == 0) _counts.Remove(item);
                else _counts[item] = value;
            }
        }

        /// <summary>個数を足して、足したあとの総数を返す。</summary>
        /// <param name="item">対象。</param>
        /// <param name="amount">足す数。負なら減る。</param>
        public int Add(T item, int amount = 1)
        {
            if (amount == 0) return this[item];

            var total = this[item] + amount;
            this[item] = total;
            return total;
        }

        /// <summary>
        /// <paramref name="amount"/> だけ消費する。足りなければ<b>何も変更せず</b> false。
        /// 資源を使う処理はこちらを通す。
        /// </summary>
        /// <param name="item">対象。</param>
        /// <param name="amount">消費する数。</param>
        public bool TryTake(T item, int amount = 1)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return true;

            var held = this[item];
            if (held < amount) return false;

            this[item] = held - amount;
            return true;
        }

        /// <summary>1 つ以上持っているか。</summary>
        /// <param name="item">対象。</param>
        public bool Contains(T item) => _counts.ContainsKey(item);

        /// <summary>種類ごと取り除く。</summary>
        /// <param name="item">対象。</param>
        public bool Remove(T item) => _counts.Remove(item);

        /// <summary>全て捨てる。</summary>
        public void Clear() => _counts.Clear();

        /// <summary>個数の合計。種類数に比例するコストがかかる。</summary>
        public int Total()
        {
            var total = 0;
            foreach (var pair in _counts) total += pair.Value;
            return total;
        }

        /// <summary>最も多く持っているもの。空なら <c>default</c>。</summary>
        /// <param name="count">その個数。</param>
        public T MostCommon(out int count)
        {
            var best = default(T);
            count = int.MinValue;

            foreach (var pair in _counts)
            {
                if (pair.Value <= count) continue;
                best = pair.Key;
                count = pair.Value;
            }

            if (_counts.Count == 0) count = 0;
            return best;
        }

        /// <summary>別の集計を足し込む。</summary>
        /// <param name="other">足し込む集計。</param>
        public void UnionWith(Counter<T> other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            foreach (var pair in other._counts) Add(pair.Key, pair.Value);
        }

        /// <summary>(対象, 個数) を走査する構造体列挙子を返す。</summary>
        public Dictionary<T, int>.Enumerator GetEnumerator() => _counts.GetEnumerator();

        IEnumerator<KeyValuePair<T, int>> IEnumerable<KeyValuePair<T, int>>.GetEnumerator() => _counts.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _counts.GetEnumerator();
    }
}
