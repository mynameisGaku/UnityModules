using System;
using System.Threading;

namespace Containers.Async
{
    /// <summary>
    /// 書き手 1・読み手 1 に限定した、ロックを取らない固定容量リングバッファ。
    /// <para>
    /// <b>この制約は守らないと壊れる。</b>書き手も読み手も 1 スレッドずつという前提でのみ、
    /// 相手のインデックスに触れないので排他が不要になる。
    /// 複数スレッドから積む可能性があるなら <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/> か
    /// <see cref="MainThreadQueue"/> を使うこと。
    /// </para>
    /// <para>
    /// 使いどころは、オーディオスレッドやネットワーク受信スレッドのように
    /// <b>ロック待ちが許されない 1 対 1 の受け渡し</b>に限られる。それ以外では複雑さに見合わない。
    /// </para>
    /// </summary>
    public sealed class SpscRingBuffer<T>
    {
        private readonly T[] _items;
        private readonly int _mask;

        // 書き手だけが _writeIndex を、読み手だけが _readIndex を進める。
        // 相手側は Volatile.Read で覗くだけなので、書き込みの競合が起きない。
        private int _writeIndex;
        private int _readIndex;

        /// <param name="capacityPowerOfTwo">
        /// 容量。剰余をビットマスクで済ませるため 2 の冪に切り上げられる。
        /// </param>
        public SpscRingBuffer(int capacityPowerOfTwo)
        {
            if (capacityPowerOfTwo <= 0) throw new ArgumentOutOfRangeException(nameof(capacityPowerOfTwo));

            var capacity = NextPowerOfTwo(capacityPowerOfTwo);
            _items = new T[capacity];
            _mask = capacity - 1;
        }

        public int Capacity => _items.Length;

        /// <summary>
        /// おおよその件数。読み書きが同時に進むので厳密な値ではない。
        /// <para>
        /// 添字は増え続けて <c>int</c> を一周するので、比較は必ず<b>差を取ってから</b>行う。
        /// <c>read &gt;= write</c> のような直接比較は、一周した瞬間に破綻する。
        /// </para>
        /// </summary>
        public int Count => unchecked(Volatile.Read(ref _writeIndex) - Volatile.Read(ref _readIndex));

        /// <summary>空かどうか。</summary>
        public bool IsEmpty => Count <= 0;

        /// <summary>書き手側から呼ぶ。満杯なら false を返し、要素は捨てられない。</summary>
        public bool TryEnqueue(in T item)
        {
            var write = _writeIndex;
            var read = Volatile.Read(ref _readIndex);

            if (unchecked(write - read) >= _items.Length) return false;

            _items[write & _mask] = item;

            // 要素の書き込みが先、インデックスの公開が後。順序が逆転すると
            // 読み手が未書き込みのスロットを読んでしまう。
            Volatile.Write(ref _writeIndex, unchecked(write + 1));
            return true;
        }

        /// <summary>読み手側から呼ぶ。空なら false。</summary>
        public bool TryDequeue(out T item)
        {
            var read = _readIndex;
            var write = Volatile.Read(ref _writeIndex);

            if (unchecked(write - read) <= 0)
            {
                item = default;
                return false;
            }

            var index = read & _mask;
            item = _items[index];
            _items[index] = default;   // 参照を残さない（GC に回収させるため）

            Volatile.Write(ref _readIndex, unchecked(read + 1));
            return true;
        }

        /// <summary>読み手側から呼ぶ。取り出さずに先頭を覗く。</summary>
        public bool TryPeek(out T item)
        {
            var read = _readIndex;

            if (unchecked(Volatile.Read(ref _writeIndex) - read) <= 0)
            {
                item = default;
                return false;
            }

            item = _items[read & _mask];
            return true;
        }

        /// <summary>
        /// 読み手側から呼ぶ。溜まっている分を全て捨てる。
        /// 書き手が動いている最中に呼んでも安全。
        /// </summary>
        public int DrainAll()
        {
            var drained = 0;
            while (TryDequeue(out _)) drained++;
            return drained;
        }

        private static int NextPowerOfTwo(int value)
        {
            var result = 1;
            while (result < value) result <<= 1;
            return result;
        }
    }
}
