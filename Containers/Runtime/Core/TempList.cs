using System;
using System.Buffers;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// <see cref="ArrayPool{T}"/> から借りて、スコープを抜けたら返す作業用配列。
    /// <code>
    /// using var scratch = RentedArray&lt;RaycastHit&gt;.Rent(32);
    /// var count = Physics.RaycastNonAlloc(ray, scratch.Array);
    /// </code>
    /// 借りた配列は要求より長いことがあるので、<c>Array.Length</c> ではなく
    /// 必ず <see cref="Length"/> を基準に扱うこと。
    /// </summary>
    public readonly struct RentedArray<T> : IDisposable
    {
        private readonly T[] _array;
        private readonly bool _clearOnReturn;

        private RentedArray(T[] array, int length, bool clearOnReturn)
        {
            _array = array;
            Length = length;
            _clearOnReturn = clearOnReturn;
        }

        /// <summary>要求した長さ。借りた配列の実際の長さではない。</summary>
        public int Length { get; }

        /// <summary>借りている配列そのもの。</summary>
        public T[] Array => _array;

        /// <summary>有効範囲だけを指す <see cref="Span{T}"/>。</summary>
        public Span<T> Span => new Span<T>(_array, 0, Length);

        /// <summary>要素への参照。</summary>
        public ref T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Length) throw new ArgumentOutOfRangeException(nameof(index));
                return ref _array[index];
            }
        }

        /// <summary>プールから配列を借りる。</summary>
        /// <param name="length">必要な長さ。</param>
        /// <param name="clearOnReturn">
        /// <typeparamref name="T"/> が参照を含むときは true のままにする。
        /// false にすると返却後もプールが参照を掴んだままになり、回収されない。
        /// </param>
        public static RentedArray<T> Rent(int length, bool clearOnReturn = true)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            return new RentedArray<T>(ArrayPool<T>.Shared.Rent(length), length, clearOnReturn);
        }

        /// <summary>配列をプールに返す。</summary>
        public void Dispose()
        {
            if (_array != null) ArrayPool<T>.Shared.Return(_array, _clearOnReturn);
        }

        /// <summary><see cref="Span"/> への暗黙変換。</summary>
        public static implicit operator Span<T>(RentedArray<T> rented) => rented.Span;
    }

    /// <summary>
    /// <c>using</c> スコープに紐づいた <see cref="FastList{T}"/>。
    /// 「毎フレーム一時リストを作る」という一番ありふれた確保源を潰すためのもの。
    /// <code>
    /// using var temp = TempList&lt;Enemy&gt;.Rent();
    /// foreach (var e in all) if (e.IsAlive) temp.List.Add(e);
    /// </code>
    /// リスト本体も内部配列も再利用されるので、定常状態では確保がゼロになる。
    /// プールはスレッドごとに独立しているため、ワーカースレッドから使っても競合しない。
    /// </summary>
    public readonly struct TempList<T> : IDisposable
    {
        [ThreadStatic] private static Stack<FastList<T>> _pool;

        /// <summary>スレッドごとに保持する空きリストの上限。</summary>
        private const int MaxPooled = 32;

        /// <summary>借りているリスト。借りたスコープの中でだけ有効。</summary>
        public FastList<T> List { get; }

        private TempList(FastList<T> list) => List = list;

        /// <summary>プールからリストを借りる。</summary>
        public static TempList<T> Rent()
        {
            var pool = _pool;
            if (pool != null && pool.Count > 0) return new TempList<T>(pool.Pop());
            return new TempList<T>(new FastList<T>());
        }

        /// <summary>リストを空にしてプールに返す。</summary>
        public void Dispose()
        {
            if (List == null) return;

            List.Clear();
            var pool = _pool ??= new Stack<FastList<T>>();
            if (pool.Count < MaxPooled) pool.Push(List);
        }

        /// <summary><see cref="List"/> への暗黙変換。</summary>
        public static implicit operator FastList<T>(TempList<T> temp) => temp.List;
    }
}
