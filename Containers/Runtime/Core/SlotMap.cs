using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Containers
{
    /// <summary>
    /// <see cref="SlotMap{T}"/> への参照。位置に加えて世代番号を持つので、
    /// 削除済みの要素を指すハンドルを「たまたま同じ位置に入った別の要素」と取り違えない。
    /// </summary>
    public readonly struct SlotHandle : IEquatable<SlotHandle>
    {
        /// <summary>どこも指していないハンドル。</summary>
        public static readonly SlotHandle None = default;

        /// <summary>スロットの位置。</summary>
        public readonly int Index;

        /// <summary>発行時点の世代番号。</summary>
        public readonly uint Generation;

        internal SlotHandle(int index, uint generation)
        {
            Index = index;
            Generation = generation;
        }

        /// <summary><see cref="None"/> でないこと。生きているかどうかとは別。</summary>
        public bool IsValid => Generation != 0;

        /// <summary>位置と世代の両方が一致するか。</summary>
        /// <param name="other">比較対象。</param>
        public bool Equals(SlotHandle other) => Index == other.Index && Generation == other.Generation;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SlotHandle other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => unchecked((Index * 397) ^ (int)Generation);

        /// <inheritdoc/>
        public override string ToString() => IsValid ? $"Slot({Index}:{Generation})" : "Slot(none)";

        /// <summary>等値比較。</summary>
        public static bool operator ==(SlotHandle a, SlotHandle b) => a.Equals(b);

        /// <summary>非等値比較。</summary>
        public static bool operator !=(SlotHandle a, SlotHandle b) => !a.Equals(b);
    }

    /// <summary>
    /// 安定したハンドルで参照できる格納庫。要素を自由に出し入れしてもスロットは再利用され、
    /// <b>削除をまたいだハンドルは「もう無い」と正しく答える</b>。
    /// <para>
    /// 添字だけを配って回すと、削除して詰めた瞬間に全ての添字が別物を指す。世代番号を添えると、
    /// 古いハンドルは <see cref="IsAlive"/> で弾かれ、静かな取り違えが起きなくなる。
    /// プールしたオブジェクト、エンティティ、UI 要素への長命な参照はこの形が正しい。
    /// 追加・削除・参照はすべて O(1)。
    /// </para>
    /// </summary>
    public sealed class SlotMap<T>
    {
        private struct Slot
        {
            public T Value;

            /// <summary>奇数なら使用中、偶数なら空き。削除のたびに 1 増える。</summary>
            public uint Generation;

            public int NextFree;
        }

        private const int NoFreeSlot = -1;

        private Slot[] _slots;
        private int _firstFree = NoFreeSlot;
        private int _count;
        private int _highWater;

        /// <summary>容量を指定して作る。足りなくなれば自動で伸びる。</summary>
        /// <param name="capacity">最初に確保するスロット数。</param>
        public SlotMap(int capacity = 8)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _slots = new Slot[Math.Max(capacity, 1)];
        }

        /// <summary>生きている要素の数。</summary>
        public int Count => _count;

        /// <summary>空かどうか。</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>確保済みのスロット数。</summary>
        public int Capacity => _slots.Length;

        /// <summary>要素への参照。ハンドルが死んでいれば例外。</summary>
        public ref T this[SlotHandle handle]
        {
            get
            {
                if (!IsAlive(handle)) throw new KeyNotFoundException($"{handle} は既に無効。");
                return ref _slots[handle.Index].Value;
            }
        }

        /// <summary>ハンドルが、発行されたときの要素をまだ指しているか。</summary>
        /// <param name="handle">確認するハンドル。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAlive(SlotHandle handle)
        {
            if (!handle.IsValid || (uint)handle.Index >= (uint)_slots.Length) return false;
            return _slots[handle.Index].Generation == handle.Generation;
        }

        /// <summary>要素を追加し、それを指すハンドルを返す。</summary>
        /// <param name="value">追加する値。</param>
        public SlotHandle Add(in T value)
        {
            int index;

            if (_firstFree != NoFreeSlot)
            {
                index = _firstFree;
                _firstFree = _slots[index].NextFree;
            }
            else
            {
                if (_highWater == _slots.Length) Array.Resize(ref _slots, _slots.Length * 2);
                index = _highWater++;
            }

            ref var slot = ref _slots[index];
            slot.Value = value;
            slot.Generation |= 1;           // 偶数（空き）→ 奇数（使用中）
            if (slot.Generation == 0) slot.Generation = 1;
            slot.NextFree = NoFreeSlot;

            _count++;
            return new SlotHandle(index, slot.Generation);
        }

        /// <summary>要素を削除する。既に無効なハンドルを渡しても安全。</summary>
        /// <param name="handle">削除する要素のハンドル。</param>
        public bool Remove(SlotHandle handle)
        {
            if (!IsAlive(handle)) return false;

            ref var slot = ref _slots[handle.Index];
            slot.Value = default;
            slot.Generation++;              // 奇数（使用中）→ 偶数（空き）。これで既存ハンドルが無効になる。
            slot.NextFree = _firstFree;
            _firstFree = handle.Index;

            _count--;
            return true;
        }

        /// <summary>値を読む。ハンドルが死んでいれば false。</summary>
        /// <param name="handle">対象のハンドル。</param>
        /// <param name="value">読み取った値。</param>
        public bool TryGetValue(SlotHandle handle, out T value)
        {
            if (!IsAlive(handle))
            {
                value = default;
                return false;
            }

            value = _slots[handle.Index].Value;
            return true;
        }

        /// <summary>
        /// 全て捨てる。世代番号は 0 に戻さず進めるので、
        /// 既存のハンドルは全て無効になる。
        /// </summary>
        public void Clear()
        {
            for (var i = 0; i < _highWater; i++)
            {
                ref var slot = ref _slots[i];
                if ((slot.Generation & 1) == 1) slot.Generation++;
                slot.Value = default;
                slot.NextFree = i + 1 < _highWater ? i + 1 : NoFreeSlot;
            }

            _firstFree = _highWater > 0 ? 0 : NoFreeSlot;
            _count = 0;
        }

        /// <summary>生きている要素だけを走査する構造体列挙子を返す。</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>空きスロットを飛ばして走査する構造体列挙子。確保は起きない。</summary>
        public struct Enumerator
        {
            private readonly SlotMap<T> _map;
            private int _index;

            internal Enumerator(SlotMap<T> map)
            {
                _map = map;
                _index = -1;
            }

            /// <summary>今の要素を指すハンドル。</summary>
            public SlotHandle CurrentHandle => new SlotHandle(_index, _map._slots[_index].Generation);

            /// <summary>今の値への参照。走査しながら書き換えられる。</summary>
            public ref T CurrentValue => ref _map._slots[_index].Value;

            /// <summary>今の (ハンドル, 値) の組。</summary>
            public (SlotHandle Handle, T Value) Current => (CurrentHandle, _map._slots[_index].Value);

            /// <summary>次の生きている要素へ進む。</summary>
            public bool MoveNext()
            {
                while (++_index < _map._highWater)
                {
                    if ((_map._slots[_index].Generation & 1) == 1) return true;
                }

                return false;
            }

            /// <summary>先頭に戻す。</summary>
            public void Reset() => _index = -1;
        }
    }
}
