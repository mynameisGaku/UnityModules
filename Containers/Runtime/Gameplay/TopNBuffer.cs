using System;
using System.Collections.Generic;

namespace Containers.Gameplay
{
    /// <summary>
    /// 上位 N 件だけを保持するコンテナ。
    /// <para>
    /// 全件を並べてから先頭を取る（O(n log n)）のではなく、<b>N 件の最小ヒープに流し込む</b>ので
    /// O(n log N) で済み、メモリも N 件分しか要らない。
    /// 母数が大きく N が小さいほど差が開く —— 1000 体の敵から最寄り 5 体、
    /// 全プレイヤーから上位 10 人、1 フレームの全処理から重い順 3 件、といった形。
    /// </para>
    /// <para>
    /// 内部は「今保持している中で最も弱い要素」が根に来る最小ヒープ。
    /// 新しい要素はその根とだけ比較すればよいので、大半の要素は 1 回の比較で捨てられる。
    /// </para>
    /// </summary>
    public sealed class TopNBuffer<TElement, TScore>
    {
        private readonly (TElement Element, TScore Score)[] _heap;
        private readonly IComparer<TScore> _comparer;
        private int _count;

        /// <param name="capacity">保持する件数 N。</param>
        /// <param name="comparer">
        /// 既定は昇順比較で、<b>大きいスコアほど上位</b>として扱う。
        /// 小さい方を上位にしたいなら反転した比較子を渡す。
        /// </param>
        public TopNBuffer(int capacity, IComparer<TScore> comparer = null)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            Capacity = capacity;
            _heap = new (TElement, TScore)[capacity];
            _comparer = comparer ?? Comparer<TScore>.Default;
        }

        public int Capacity { get; }
        public int Count => _count;
        public bool IsFull => _count == Capacity;

        /// <summary>保持中で最も低いスコア。満杯ならこれ以下の要素は無視される。</summary>
        public bool TryPeekThreshold(out TScore score)
        {
            if (_count == 0)
            {
                score = default;
                return false;
            }

            score = _heap[0].Score;
            return true;
        }

        /// <summary>
        /// 候補を 1 件流し込む。上位 N に入らなければ捨てられる。
        /// 戻り値は採用されたかどうか。
        /// </summary>
        public bool Offer(TElement element, TScore score)
        {
            if (_count < Capacity)
            {
                _heap[_count] = (element, score);
                SiftUp(_count++);
                return true;
            }

            // 最弱より弱ければ即棄却。ここが O(1) なので全体が速い。
            if (_comparer.Compare(score, _heap[0].Score) <= 0) return false;

            _heap[0] = (element, score);
            SiftDown(0);
            return true;
        }

        /// <summary>
        /// 上位から順に取り出して <paramref name="results"/> に詰める。
        /// バッファの中身は変わらないので、続けて <see cref="Offer"/> できる。
        /// </summary>
        public void CopySorted(FastList<TElement> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (_count == 0) return;

            var ordered = new (TElement Element, TScore Score)[_count];
            Array.Copy(_heap, ordered, _count);

            // 降順（上位が先頭）に並べる。
            Array.Sort(ordered, (a, b) => _comparer.Compare(b.Score, a.Score));

            for (var i = 0; i < ordered.Length; i++) results.Add(ordered[i].Element);
        }

        /// <summary>並び順を気にせず中身を見る。ランキング表示以外ならこれで足りる。</summary>
        public ReadOnlySpan<(TElement Element, TScore Score)> AsSpan() =>
            new ReadOnlySpan<(TElement, TScore)>(_heap, 0, _count);

        public void Clear()
        {
            Array.Clear(_heap, 0, _count);
            _count = 0;
        }

        private void SiftUp(int index)
        {
            var node = _heap[index];

            while (index > 0)
            {
                var parent = (index - 1) >> 1;
                if (_comparer.Compare(node.Score, _heap[parent].Score) >= 0) break;

                _heap[index] = _heap[parent];
                index = parent;
            }

            _heap[index] = node;
        }

        private void SiftDown(int index)
        {
            var node = _heap[index];
            var half = _count >> 1;

            while (index < half)
            {
                var child = (index << 1) + 1;
                var right = child + 1;

                if (right < _count && _comparer.Compare(_heap[right].Score, _heap[child].Score) < 0) child = right;
                if (_comparer.Compare(_heap[child].Score, node.Score) >= 0) break;

                _heap[index] = _heap[child];
                index = child;
            }

            _heap[index] = node;
        }
    }
}
