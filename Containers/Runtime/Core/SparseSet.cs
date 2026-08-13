using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Containers
{
    /// <summary>
    /// 小さい非負整数の id を値に対応づけるコンテナ。走査用の密配列と、
    /// 引き当て用の疎配列を組み合わせて、追加・削除・存在判定を全て O(1) にする。
    /// <para>
    /// int をキーにした <see cref="Dictionary{TKey,TValue}"/> との違いは、ハッシュもバケットも
    /// 無いことと、走査が連続したメモリを舐めること。代わりに<b>最大 id に比例したメモリ</b>を使うので、
    /// エンティティ id やプールのスロット番号には向き、任意の整数には向かない。
    /// </para>
    /// </summary>
    public sealed class SparseSet<T>
    {
        private const int NotPresent = -1;

        private int[] _sparse;      // id → 密配列の位置
        private int[] _ids;         // 密配列の位置 → id
        private T[] _values;        // 密配列の位置 → 値
        private int _count;

        /// <summary>容量を指定して作る。どちらも足りなくなれば自動で伸びる。</summary>
        /// <param name="idCapacity">扱う id の想定上限。</param>
        /// <param name="valueCapacity">同時に保持する値の想定数。</param>
        public SparseSet(int idCapacity = 16, int valueCapacity = 16)
        {
            if (idCapacity < 0) throw new ArgumentOutOfRangeException(nameof(idCapacity));
            if (valueCapacity < 0) throw new ArgumentOutOfRangeException(nameof(valueCapacity));

            _sparse = new int[Math.Max(idCapacity, 1)];
            _ids = new int[Math.Max(valueCapacity, 1)];
            _values = new T[Math.Max(valueCapacity, 1)];
            _sparse.AsSpan().Fill(NotPresent);
        }

        /// <summary>保持している要素数。</summary>
        public int Count => _count;

        /// <summary>空かどうか。</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>生きている値の連続領域。並び順は不定で、削除のたびに変わる。</summary>
        public Span<T> Values => new Span<T>(_values, 0, _count);

        /// <summary><see cref="Values"/> と同じ並びの id。<c>Ids[i]</c> が <c>Values[i]</c> の持ち主。</summary>
        public ReadOnlySpan<int> Ids => new ReadOnlySpan<int>(_ids, 0, _count);

        /// <summary>id に対応する値への参照。無ければ例外。</summary>
        public ref T this[int id]
        {
            get
            {
                var dense = DenseIndexOf(id);
                if (dense < 0) throw new KeyNotFoundException($"id {id} は登録されていない。");
                return ref _values[dense];
            }
        }

        /// <summary>id が登録されているか。</summary>
        /// <param name="id">確認する id。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int id) => DenseIndexOf(id) >= 0;

        /// <summary>値を設定する。既にあれば上書きする。</summary>
        /// <param name="id">キーとなる非負整数。</param>
        /// <param name="value">設定する値。</param>
        public void Set(int id, in T value)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id));

            var dense = DenseIndexOf(id);
            if (dense >= 0)
            {
                _values[dense] = value;
                return;
            }

            EnsureSparse(id);
            EnsureDense(_count + 1);

            _sparse[id] = _count;
            _ids[_count] = id;
            _values[_count] = value;
            _count++;
        }

        /// <summary>値を追加する。id が既にあれば例外。</summary>
        /// <param name="id">キーとなる非負整数。</param>
        /// <param name="value">追加する値。</param>
        public void Add(int id, in T value)
        {
            if (Contains(id)) throw new ArgumentException($"id {id} は既に登録済み。", nameof(id));
            Set(id, value);
        }

        /// <summary>値を読む。無ければ false。</summary>
        /// <param name="id">キー。</param>
        /// <param name="value">読み取った値。</param>
        public bool TryGetValue(int id, out T value)
        {
            var dense = DenseIndexOf(id);
            if (dense < 0)
            {
                value = default;
                return false;
            }

            value = _values[dense];
            return true;
        }

        /// <summary>
        /// 削除する。末尾の要素を穴に移すので O(1) だが、<see cref="Values"/> の並びが変わる。
        /// </summary>
        /// <param name="id">削除する id。</param>
        public bool Remove(int id)
        {
            var dense = DenseIndexOf(id);
            if (dense < 0) return false;

            _count--;
            var movedId = _ids[_count];

            _values[dense] = _values[_count];
            _ids[dense] = movedId;
            _sparse[movedId] = dense;

            _values[_count] = default;
            _sparse[id] = NotPresent;
            return true;
        }

        /// <summary>全て捨てる。</summary>
        public void Clear()
        {
            for (var i = 0; i < _count; i++) _sparse[_ids[i]] = NotPresent;
            Array.Clear(_values, 0, _count);
            _count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int DenseIndexOf(int id)
        {
            if ((uint)id >= (uint)_sparse.Length) return NotPresent;

            var dense = _sparse[id];
            // 疎配列には古い値が残っていることがあるので、密配列側と往復して裏を取る。
            return dense >= 0 && dense < _count && _ids[dense] == id ? dense : NotPresent;
        }

        private void EnsureSparse(int id)
        {
            if (id < _sparse.Length) return;

            var capacity = Math.Max(_sparse.Length * 2, id + 1);
            var grown = new int[capacity];
            Array.Copy(_sparse, grown, _sparse.Length);
            grown.AsSpan(_sparse.Length).Fill(NotPresent);
            _sparse = grown;
        }

        private void EnsureDense(int required)
        {
            if (required <= _values.Length) return;

            var capacity = Math.Max(_values.Length * 2, required);
            Array.Resize(ref _values, capacity);
            Array.Resize(ref _ids, capacity);
        }

        /// <summary>(id, 値) を密配列の順に走査する構造体列挙子を返す。</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>(id, 値) を走査する構造体列挙子。確保は起きない。</summary>
        public struct Enumerator
        {
            private readonly SparseSet<T> _set;
            private int _index;

            internal Enumerator(SparseSet<T> set)
            {
                _set = set;
                _index = -1;
            }

            /// <summary>今の id。</summary>
            public int CurrentId => _set._ids[_index];

            /// <summary>今の値への参照。走査しながら書き換えられる。</summary>
            public ref T CurrentValue => ref _set._values[_index];

            /// <summary>今の (id, 値) の組。</summary>
            public (int Id, T Value) Current => (_set._ids[_index], _set._values[_index]);

            /// <summary>次の要素へ進む。</summary>
            public bool MoveNext() => ++_index < _set._count;

            /// <summary>先頭に戻す。</summary>
            public void Reset() => _index = -1;
        }
    }

    /// <summary>
    /// 値を持たない版。整数 id の集合を O(1) で出し入れでき、走査は連続メモリになる。
    /// 「訪問済み」「今フレーム処理済み」のような一時的な集合に向く。
    /// </summary>
    public sealed class SparseIntSet : IReadOnlyCollection<int>
    {
        private const int NotPresent = -1;

        private int[] _sparse;
        private int[] _dense;
        private int _count;

        /// <summary>容量を指定して作る。</summary>
        /// <param name="idCapacity">扱う id の想定上限。</param>
        public SparseIntSet(int idCapacity = 16)
        {
            _sparse = new int[Math.Max(idCapacity, 1)];
            _dense = new int[Math.Max(idCapacity, 1)];
            _sparse.AsSpan().Fill(NotPresent);
        }

        /// <summary>要素数。</summary>
        public int Count => _count;

        /// <summary>含まれている id の連続領域。並び順は不定。</summary>
        public ReadOnlySpan<int> AsSpan() => new ReadOnlySpan<int>(_dense, 0, _count);

        /// <summary>id が含まれているか。</summary>
        /// <param name="id">確認する id。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int id)
        {
            if ((uint)id >= (uint)_sparse.Length) return false;
            var dense = _sparse[id];
            return dense >= 0 && dense < _count && _dense[dense] == id;
        }

        /// <summary>id を追加する。既にあれば false。</summary>
        /// <param name="id">追加する非負整数。</param>
        public bool Add(int id)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (Contains(id)) return false;

            if (id >= _sparse.Length)
            {
                var capacity = Math.Max(_sparse.Length * 2, id + 1);
                var grown = new int[capacity];
                Array.Copy(_sparse, grown, _sparse.Length);
                grown.AsSpan(_sparse.Length).Fill(NotPresent);
                _sparse = grown;
            }

            if (_count == _dense.Length) Array.Resize(ref _dense, _dense.Length * 2);

            _sparse[id] = _count;
            _dense[_count++] = id;
            return true;
        }

        /// <summary>id を取り除く。無ければ false。</summary>
        /// <param name="id">取り除く id。</param>
        public bool Remove(int id)
        {
            if (!Contains(id)) return false;

            var dense = _sparse[id];
            _count--;

            var moved = _dense[_count];
            _dense[dense] = moved;
            _sparse[moved] = dense;
            _sparse[id] = NotPresent;
            return true;
        }

        /// <summary>全て捨てる。</summary>
        public void Clear()
        {
            for (var i = 0; i < _count; i++) _sparse[_dense[i]] = NotPresent;
            _count = 0;
        }

        /// <summary>確保無しで走査する構造体列挙子を返す。</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>id を走査する構造体列挙子。</summary>
        public struct Enumerator : IEnumerator<int>
        {
            private readonly SparseIntSet _set;
            private int _index;

            internal Enumerator(SparseIntSet set)
            {
                _set = set;
                _index = -1;
            }

            /// <summary>今の id。</summary>
            public int Current => _set._dense[_index];

            object IEnumerator.Current => Current;

            /// <summary>次へ進む。</summary>
            public bool MoveNext() => ++_index < _set._count;

            /// <summary>先頭に戻す。</summary>
            public void Reset() => _index = -1;

            /// <summary>何もしない。</summary>
            public void Dispose() { }
        }
    }
}
