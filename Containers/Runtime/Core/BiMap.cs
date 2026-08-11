using System;
using System.Collections;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// どちらの側からも引ける 1 対 1 の対応表。ネットワーク ID とインスタンス、
    /// 内部名と表示名、タイル種別とプレハブ。
    /// <para>
    /// どの操作も 2 本の辞書を同時に更新するので、両方向が食い違うことがない。
    /// </para>
    /// </summary>
    public sealed class BiMap<TLeft, TRight> : IEnumerable<KeyValuePair<TLeft, TRight>>
    {
        private readonly Dictionary<TLeft, TRight> _forward;
        private readonly Dictionary<TRight, TLeft> _reverse;

        /// <summary>両側の比較方法を指定して作る。</summary>
        /// <param name="leftComparer">左側の比較方法。</param>
        /// <param name="rightComparer">右側の比較方法。</param>
        public BiMap(IEqualityComparer<TLeft> leftComparer = null, IEqualityComparer<TRight> rightComparer = null)
        {
            _forward = new Dictionary<TLeft, TRight>(leftComparer);
            _reverse = new Dictionary<TRight, TLeft>(rightComparer);
        }

        /// <summary>対応の数。</summary>
        public int Count => _forward.Count;

        /// <summary>左側の一覧。</summary>
        public Dictionary<TLeft, TRight>.KeyCollection Lefts => _forward.Keys;

        /// <summary>右側の一覧。</summary>
        public Dictionary<TRight, TLeft>.KeyCollection Rights => _reverse.Keys;

        /// <summary>左から右を引く。</summary>
        public TRight this[TLeft left] => _forward[left];

        /// <summary>
        /// 対応を追加する。どちらかの側が既に使われていれば例外にする ——
        /// 黙って古い対応を捨てるのが、双方向の表が食い違い始める典型的な原因なので。
        /// </summary>
        /// <param name="left">左側の値。</param>
        /// <param name="right">右側の値。</param>
        public void Add(TLeft left, TRight right)
        {
            if (_forward.ContainsKey(left))
                throw new ArgumentException($"左の値 {left} は既に対応済み。", nameof(left));
            if (_reverse.ContainsKey(right))
                throw new ArgumentException($"右の値 {right} は既に対応済み。", nameof(right));

            _forward.Add(left, right);
            _reverse.Add(right, left);
        }

        /// <summary>対応を追加または置き換える。どちらの側の古い対応も外れる。</summary>
        /// <param name="left">左側の値。</param>
        /// <param name="right">右側の値。</param>
        public void Set(TLeft left, TRight right)
        {
            if (_forward.TryGetValue(left, out var oldRight)) _reverse.Remove(oldRight);
            if (_reverse.TryGetValue(right, out var oldLeft)) _forward.Remove(oldLeft);

            _forward[left] = right;
            _reverse[right] = left;
        }

        /// <summary>左から右を引く。</summary>
        /// <param name="left">左側の値。</param>
        /// <param name="right">対応する右側の値。</param>
        public bool TryGetRight(TLeft left, out TRight right) => _forward.TryGetValue(left, out right);

        /// <summary>右から左を引く。</summary>
        /// <param name="right">右側の値。</param>
        /// <param name="left">対応する左側の値。</param>
        public bool TryGetLeft(TRight right, out TLeft left) => _reverse.TryGetValue(right, out left);

        /// <summary>左側に登録があるか。</summary>
        /// <param name="left">探す値。</param>
        public bool ContainsLeft(TLeft left) => _forward.ContainsKey(left);

        /// <summary>右側に登録があるか。</summary>
        /// <param name="right">探す値。</param>
        public bool ContainsRight(TRight right) => _reverse.ContainsKey(right);

        /// <summary>左を指定して対応を外す。</summary>
        /// <param name="left">左側の値。</param>
        public bool RemoveLeft(TLeft left)
        {
            if (!_forward.TryGetValue(left, out var right)) return false;
            _forward.Remove(left);
            _reverse.Remove(right);
            return true;
        }

        /// <summary>右を指定して対応を外す。</summary>
        /// <param name="right">右側の値。</param>
        public bool RemoveRight(TRight right)
        {
            if (!_reverse.TryGetValue(right, out var left)) return false;
            _reverse.Remove(right);
            _forward.Remove(left);
            return true;
        }

        /// <summary>全て捨てる。</summary>
        public void Clear()
        {
            _forward.Clear();
            _reverse.Clear();
        }

        /// <summary>(左, 右) の組を走査する構造体列挙子を返す。</summary>
        public Dictionary<TLeft, TRight>.Enumerator GetEnumerator() => _forward.GetEnumerator();

        IEnumerator<KeyValuePair<TLeft, TRight>> IEnumerable<KeyValuePair<TLeft, TRight>>.GetEnumerator() => _forward.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _forward.GetEnumerator();
    }
}
