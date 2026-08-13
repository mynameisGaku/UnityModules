using System;
using System.Runtime.CompilerServices;

namespace Containers
{
    /// <summary>
    /// 64 個までのフラグを <c>ulong</c> 1 本に詰めた構造体。ヒープ確保が一切起きないので、
    /// 他の構造体のフィールドにも、タイトなループの中にも置ける。
    /// </summary>
    public struct FixedBitSet64 : IEquatable<FixedBitSet64>
    {
        /// <summary>扱えるビット数。</summary>
        public const int Capacity = 64;

        /// <summary>生のビット列。</summary>
        public ulong Bits;

        /// <summary>ビット列を指定して作る。</summary>
        /// <param name="bits">初期値。</param>
        public FixedBitSet64(ulong bits) => Bits = bits;

        /// <summary>全ビットが 0。</summary>
        public static FixedBitSet64 Empty => default;

        /// <summary>全ビットが 1。</summary>
        public static FixedBitSet64 All => new FixedBitSet64(ulong.MaxValue);

        /// <summary>1 つも立っていないか。</summary>
        public bool IsEmpty => Bits == 0;

        /// <summary>ビットの読み書き。</summary>
        public bool this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Get(index);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value) Set(index);
                else Unset(index);
            }
        }

        /// <summary>ビットが立っているか。</summary>
        /// <param name="index">ビット位置。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Get(int index)
        {
            if ((uint)index >= Capacity) throw new ArgumentOutOfRangeException(nameof(index));
            return (Bits & (1UL << index)) != 0;
        }

        /// <summary>ビットを立てる。</summary>
        /// <param name="index">ビット位置。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index)
        {
            if ((uint)index >= Capacity) throw new ArgumentOutOfRangeException(nameof(index));
            Bits |= 1UL << index;
        }

        /// <summary>ビットを落とす。</summary>
        /// <param name="index">ビット位置。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unset(int index)
        {
            if ((uint)index >= Capacity) throw new ArgumentOutOfRangeException(nameof(index));
            Bits &= ~(1UL << index);
        }

        /// <summary>ビットを反転する。</summary>
        /// <param name="index">ビット位置。</param>
        public void Toggle(int index)
        {
            if ((uint)index >= Capacity) throw new ArgumentOutOfRangeException(nameof(index));
            Bits ^= 1UL << index;
        }

        /// <summary>全ビットを 0 にする。</summary>
        public void Clear() => Bits = 0;

        /// <summary>立っているビットの数。</summary>
        public int PopCount() => BitUtility.PopCount(Bits);

        /// <summary>最も下位で立っているビットの位置。1 つも無ければ -1。</summary>
        public int LowestSetBit() => Bits == 0 ? -1 : BitUtility.TrailingZeroCount(Bits);

        /// <summary>共通のビットがあるか。</summary>
        /// <param name="other">比較対象。</param>
        public bool Overlaps(FixedBitSet64 other) => (Bits & other.Bits) != 0;

        /// <summary>相手のビットを全て含んでいるか。</summary>
        /// <param name="other">比較対象。</param>
        public bool ContainsAll(FixedBitSet64 other) => (Bits & other.Bits) == other.Bits;

        /// <summary>積。</summary>
        public static FixedBitSet64 operator &(FixedBitSet64 a, FixedBitSet64 b) => new FixedBitSet64(a.Bits & b.Bits);

        /// <summary>和。</summary>
        public static FixedBitSet64 operator |(FixedBitSet64 a, FixedBitSet64 b) => new FixedBitSet64(a.Bits | b.Bits);

        /// <summary>排他的論理和。</summary>
        public static FixedBitSet64 operator ^(FixedBitSet64 a, FixedBitSet64 b) => new FixedBitSet64(a.Bits ^ b.Bits);

        /// <summary>反転。</summary>
        public static FixedBitSet64 operator ~(FixedBitSet64 a) => new FixedBitSet64(~a.Bits);

        /// <summary>ビット列が一致するか。</summary>
        /// <param name="other">比較対象。</param>
        public bool Equals(FixedBitSet64 other) => Bits == other.Bits;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is FixedBitSet64 other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Bits.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => Convert.ToString((long)Bits, 2).PadLeft(64, '0');

        /// <summary>等値比較。</summary>
        public static bool operator ==(FixedBitSet64 a, FixedBitSet64 b) => a.Equals(b);

        /// <summary>非等値比較。</summary>
        public static bool operator !=(FixedBitSet64 a, FixedBitSet64 b) => !a.Equals(b);

        /// <summary>立っているビットの位置を下位から列挙する。</summary>
        public SetBitEnumerator GetEnumerator() => new SetBitEnumerator(Bits);

        /// <summary>立っているビットの位置を列挙する構造体列挙子。</summary>
        public struct SetBitEnumerator
        {
            private ulong _remaining;

            internal SetBitEnumerator(ulong bits)
            {
                _remaining = bits;
                Current = -1;
            }

            /// <summary>今のビット位置。</summary>
            public int Current { get; private set; }

            /// <summary>次に立っているビットへ進む。</summary>
            public bool MoveNext()
            {
                if (_remaining == 0) return false;

                Current = BitUtility.TrailingZeroCount(_remaining);
                _remaining &= _remaining - 1;   // 最下位の立っているビットを落とす
                return true;
            }
        }
    }

    /// <summary>
    /// <c>ulong</c> の配列で持つ可変長のビット集合。
    /// 密な整数の所属判定なら <c>HashSet&lt;int&gt;</c> より安い ——
    /// 訪問済みフラグ、ダーティマスク、レイヤーの絞り込み。
    /// </summary>
    public sealed class BitSet
    {
        private const int BitsPerWord = 64;

        private ulong[] _words;
        private int _capacity;

        /// <summary>容量を指定して作る。<see cref="Set"/> で自動的に伸びる。</summary>
        /// <param name="capacity">最初に扱えるビット数。</param>
        public BitSet(int capacity = 64)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _words = new ulong[WordCount(capacity)];
        }

        /// <summary>扱える最大の位置 ＋ 1。</summary>
        public int Capacity => _capacity;

        /// <summary>ビットの読み書き。</summary>
        public bool this[int index]
        {
            get => Get(index);
            set
            {
                if (value) Set(index);
                else Unset(index);
            }
        }

        /// <summary>ビットが立っているか。範囲外は false。</summary>
        /// <param name="index">ビット位置。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Get(int index)
        {
            if ((uint)index >= (uint)_capacity) return false;
            return (_words[index >> 6] & (1UL << (index & 63))) != 0;
        }

        /// <summary>ビットを立てる。必要なら容量が伸びる。</summary>
        /// <param name="index">ビット位置。</param>
        public void Set(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            EnsureCapacity(index + 1);
            _words[index >> 6] |= 1UL << (index & 63);
        }

        /// <summary>ビットを落とす。範囲外は何もしない。</summary>
        /// <param name="index">ビット位置。</param>
        public void Unset(int index)
        {
            if ((uint)index >= (uint)_capacity) return;
            _words[index >> 6] &= ~(1UL << (index & 63));
        }

        /// <summary>ビットを反転する。必要なら容量が伸びる。</summary>
        /// <param name="index">ビット位置。</param>
        public void Toggle(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            EnsureCapacity(index + 1);
            _words[index >> 6] ^= 1UL << (index & 63);
        }

        /// <summary>全ビットを 0 にする。</summary>
        public void Clear() => Array.Clear(_words, 0, _words.Length);

        /// <summary>容量の範囲の全ビットを 1 にする。</summary>
        public void SetAll()
        {
            for (var i = 0; i < _words.Length; i++) _words[i] = ulong.MaxValue;
            TrimTailBits();
        }

        /// <summary>立っているビットの数。</summary>
        public int PopCount()
        {
            var total = 0;
            for (var i = 0; i < _words.Length; i++) total += BitUtility.PopCount(_words[i]);
            return total;
        }

        /// <summary>1 つも立っていないか。</summary>
        public bool IsEmpty
        {
            get
            {
                for (var i = 0; i < _words.Length; i++)
                {
                    if (_words[i] != 0) return false;
                }

                return true;
            }
        }

        /// <summary>和集合を取る。</summary>
        /// <param name="other">合流させる集合。</param>
        public void UnionWith(BitSet other)
        {
            EnsureCapacity(other._capacity);

            // 走査の上限は両者の「配列長」の小さい方。EnsureCapacity が保証するのは
            // ビット数であって配列長ではなく、しかも 2 倍で多めに確保するため、
            // other._words.Length が自分の配列長を超えていることがある。
            var shared = Math.Min(_words.Length, other._words.Length);
            for (var i = 0; i < shared; i++) _words[i] |= other._words[i];
        }

        /// <summary>積集合を取る。</summary>
        /// <param name="other">交差させる集合。</param>
        public void IntersectWith(BitSet other)
        {
            for (var i = 0; i < _words.Length; i++)
            {
                _words[i] &= i < other._words.Length ? other._words[i] : 0UL;
            }
        }

        /// <summary>差集合を取る。</summary>
        /// <param name="other">引く集合。</param>
        public void ExceptWith(BitSet other)
        {
            var shared = Math.Min(_words.Length, other._words.Length);
            for (var i = 0; i < shared; i++) _words[i] &= ~other._words[i];
        }

        /// <summary>共通のビットがあるか。</summary>
        /// <param name="other">比較対象。</param>
        public bool Overlaps(BitSet other)
        {
            var shared = Math.Min(_words.Length, other._words.Length);
            for (var i = 0; i < shared; i++)
            {
                if ((_words[i] & other._words[i]) != 0) return true;
            }

            return false;
        }

        /// <summary>少なくとも指定のビット数を扱えるようにする。</summary>
        /// <param name="capacity">必要なビット数。</param>
        public void EnsureCapacity(int capacity)
        {
            if (capacity <= _capacity) return;

            var words = WordCount(capacity);
            if (words > _words.Length) Array.Resize(ref _words, Math.Max(words, _words.Length * 2));
            _capacity = capacity;
        }

        /// <summary>
        /// 容量を超えた位置のビットを落とす。
        /// <para>
        /// 最終ワードだけを削る実装は誤り。配列は容量より多めに確保されるので、
        /// 容量の外に丸ごと余るワードが存在しうる。さらに <c>ulong</c> のシフト量は
        /// 下位 6 ビットに丸められるため、64 以上ずらしたつもりが実際にはほとんど削れない。
        /// </para>
        /// </summary>
        private void TrimTailBits()
        {
            if (_capacity == 0)
            {
                Array.Clear(_words, 0, _words.Length);
                return;
            }

            var lastWord = (_capacity - 1) >> 6;

            // 容量の外に丸ごと出ているワードは全て 0 にする。
            for (var i = lastWord + 1; i < _words.Length; i++) _words[i] = 0UL;

            // 最終ワードの、容量を超えた分だけを削る。ここでの spare は必ず 0〜63。
            var spare = ((lastWord + 1) << 6) - _capacity;
            if (spare > 0) _words[lastWord] &= ulong.MaxValue >> spare;
        }

        private static int WordCount(int capacity) => (capacity + BitsPerWord - 1) / BitsPerWord;

        /// <summary>立っているビットの位置を昇順に列挙する。確保は起きない。</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>立っているビットの位置を列挙する構造体列挙子。</summary>
        public struct Enumerator
        {
            private readonly BitSet _set;
            private int _wordIndex;
            private ulong _remaining;

            internal Enumerator(BitSet set)
            {
                _set = set;
                _wordIndex = -1;
                _remaining = 0;
                Current = -1;
            }

            /// <summary>今のビット位置。</summary>
            public int Current { get; private set; }

            /// <summary>次に立っているビットへ進む。</summary>
            public bool MoveNext()
            {
                while (_remaining == 0)
                {
                    if (++_wordIndex >= _set._words.Length) return false;
                    _remaining = _set._words[_wordIndex];
                }

                var bit = BitUtility.TrailingZeroCount(_remaining);
                _remaining &= _remaining - 1;
                Current = (_wordIndex << 6) + bit;
                return Current < _set._capacity;
            }
        }
    }

    /// <summary>
    /// ビット演算の下請け。netstandard2.1 には <c>System.Numerics.BitOperations</c> が無いので、
    /// 定番のソフトウェア実装を持っている。
    /// </summary>
    internal static class BitUtility
    {
        private static readonly int[] DeBruijnPositions =
        {
            0, 1, 56, 2, 57, 49, 28, 3, 61, 58, 42, 50, 38, 29, 17, 4,
            62, 47, 59, 36, 45, 43, 51, 22, 53, 39, 33, 30, 24, 18, 12, 5,
            63, 55, 48, 27, 60, 41, 37, 16, 46, 35, 44, 21, 52, 32, 23, 11,
            54, 26, 40, 15, 34, 20, 31, 10, 25, 14, 19, 9, 13, 8, 7, 6
        };

        /// <summary>最下位から連続する 0 の数。0 なら 64。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(ulong value)
        {
            if (value == 0) return 64;
            return DeBruijnPositions[unchecked((value & (ulong)(-(long)value)) * 0x03F79D71B4CA8B09UL) >> 58];
        }

        /// <summary>立っているビットの数。分割統治で数える。</summary>
        public static int PopCount(ulong value)
        {
            value -= (value >> 1) & 0x5555555555555555UL;
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((value * 0x0101010101010101UL) >> 56);
        }
    }
}
