using System;
using System.Collections.Generic;

namespace Containers.Gameplay
{
    /// <summary>
    /// 「引いたら戻さない」抽選。全部引き切ったら中身を戻してシャッフルし直す。
    /// <para>
    /// 純粋な乱数より<b>プレイヤーの体感が明確に良くなる</b>のがこのコンテナの存在理由。
    /// 一様乱数は同じ結果を 3 回続けて出すことがあり、人はそれを「壊れている」と感じる。
    /// 袋から引く方式なら偏りに上限が付き、それでいて順序は毎回変わる。
    /// </para>
    /// <para>
    /// 敵のセリフ、ヒット時のサウンド、ランダムな出現パターン、テトリスの 7-bag のような
    /// <b>「同じものが続くと嫌」な用途</b>全般に効く。実装は短いが効果は大きい。
    /// </para>
    /// </summary>
    public sealed class ShuffleBag<T>
    {
        private readonly List<T> _contents = new List<T>();
        private readonly List<T> _remaining = new List<T>();
        private readonly Random _random;

        /// <param name="seed">固定するとリプレイやテストで同じ順序を再現できる。</param>
        public ShuffleBag(int? seed = null) =>
            _random = seed.HasValue ? new Random(seed.Value) : new Random();

        /// <summary>袋に入っている種類の総数（引いた分を含む）。</summary>
        public int Count => _contents.Count;

        /// <summary>今の周回で残っている数。0 なら次の <see cref="Next"/> で補充される。</summary>
        public int Remaining => _remaining.Count;

        public bool IsEmpty => _contents.Count == 0;

        /// <summary>
        /// 要素を追加する。<paramref name="copies"/> を増やすとその要素が出やすくなる ——
        /// 重み付けはコピー数で表現する。
        /// </summary>
        public void Add(T item, int copies = 1)
        {
            if (copies <= 0) throw new ArgumentOutOfRangeException(nameof(copies));

            for (var i = 0; i < copies; i++)
            {
                _contents.Add(item);
                _remaining.Add(item);
            }
        }

        public bool Remove(T item)
        {
            if (!_contents.Remove(item)) return false;

            _remaining.Remove(item);
            return true;
        }

        /// <summary>1 つ引く。残りが尽きていれば補充してから引く。</summary>
        public T Next()
        {
            if (_contents.Count == 0) throw new InvalidOperationException("空の ShuffleBag からは引けない。");

            if (_remaining.Count == 0) Refill();

            var index = _random.Next(_remaining.Count);
            var item = _remaining[index];

            // 末尾と入れ替えて末尾を削る。順序は保つ必要が無いので O(1)。
            _remaining[index] = _remaining[_remaining.Count - 1];
            _remaining.RemoveAt(_remaining.Count - 1);
            return item;
        }

        /// <summary>
        /// 直前に引いたものが先頭に来ないように補充する。
        /// 周回の境目で同じものが 2 回続くのを防ぎたいときに使う。
        /// </summary>
        public T NextAvoidingRepeat(T lastDrawn)
        {
            if (_contents.Count <= 1) return Next();

            var comparer = EqualityComparer<T>.Default;

            for (var attempt = 0; attempt < 4; attempt++)
            {
                var candidate = Next();
                if (!comparer.Equals(candidate, lastDrawn)) return candidate;

                // 引いてしまったものは戻して引き直す。
                _remaining.Add(candidate);
            }

            return Next();
        }

        /// <summary>今の周回を打ち切り、袋を満杯に戻す。</summary>
        public void Refill()
        {
            _remaining.Clear();
            _remaining.AddRange(_contents);
        }

        public void Clear()
        {
            _contents.Clear();
            _remaining.Clear();
        }
    }
}
