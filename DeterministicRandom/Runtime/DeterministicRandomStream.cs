using System;

namespace DeterministicRandom
{
    /// <summary>
    /// 明示seedまたは保存状態から、version固定の再現可能な疑似乱数列を生成する。
    /// Unityのglobal乱数や時刻を参照せず、暗号用途には使用しない。
    /// </summary>
    public sealed class DeterministicRandomStream
    {
        /// <summary>xoshiro256**とSplitMix64 seed展開を表す現行algorithm version。</summary>
        public const int CurrentAlgorithmVersion = 1;

        private ulong _word0;
        private ulong _word1;
        private ulong _word2;
        private ulong _word3;

        private DeterministicRandomStream(DeterministicRandomState state)
        {
            Apply(state);
        }

        /// <summary>保存・比較・復元に使える現在の256-bit状態。</summary>
        public DeterministicRandomState State => new DeterministicRandomState(CurrentAlgorithmVersion, _word0, _word1, _word2, _word3);

        /// <summary>任意の64-bit seedをSplitMix64で展開し、新しいstreamを作る。seed 0も有効。</summary>
        /// <param name="seed">同じ乱数列を識別する64-bit値。</param>
        /// <returns>初期位置の乱数stream。</returns>
        public static DeterministicRandomStream Create(ulong seed)
        {
            var seedState = seed;
            var state = new DeterministicRandomState(
                CurrentAlgorithmVersion,
                NextSplitMix64(ref seedState),
                NextSplitMix64(ref seedState),
                NextSplitMix64(ref seedState),
                NextSplitMix64(ref seedState));
            return new DeterministicRandomStream(state);
        }

        /// <summary>保存状態からstreamを再構築する。version不一致または全word 0なら作成しない。</summary>
        /// <param name="state">復元するalgorithm version付き状態。</param>
        /// <param name="stream">成功時に作成したstream。</param>
        /// <param name="error">作成できなかった理由。</param>
        /// <returns>作成できた場合にtrue。</returns>
        public static bool TryCreate(DeterministicRandomState state, out DeterministicRandomStream stream, out DeterministicRandomError error)
        {
            error = ValidateState(state);
            if (error != DeterministicRandomError.None)
            {
                stream = null;
                return false;
            }

            stream = new DeterministicRandomStream(state);
            return true;
        }

        /// <summary>次の64-bit値を返し、streamを1 draw進める。</summary>
        public ulong NextUInt64()
        {
            unchecked
            {
                var result = RotateLeft(_word1 * 5UL, 7) * 9UL;
                var shifted = _word1 << 17;
                _word2 ^= _word0;
                _word3 ^= _word1;
                _word1 ^= _word2;
                _word0 ^= _word3;
                _word2 ^= shifted;
                _word3 = RotateLeft(_word3, 45);
                return result;
            }
        }

        /// <summary>次の64-bit値の上位32 bitを返し、streamを1 draw進める。</summary>
        public uint NextUInt32() => (uint)(NextUInt64() >> 32);

        /// <summary>次の64-bit値の最上位bitをboolとして返し、streamを1 draw進める。</summary>
        public bool NextBoolean() => (NextUInt64() & (1UL << 63)) != 0UL;

        /// <summary>上位53 bitから0以上1未満のdoubleを返し、streamを1 draw進める。</summary>
        public double NextDouble() => (NextUInt64() >> 11) * (1d / 9007199254740992d);

        /// <summary>上位24 bitから0以上1未満のfloatを返し、streamを1 draw進める。</summary>
        public float NextSingle() => (NextUInt64() >> 40) * (1f / 16777216f);

        /// <summary>0以上exclusiveMax未満の偏りのない64-bit値を返す。上端0なら状態を変更しない。</summary>
        /// <param name="exclusiveMax">含まない上端。1以上。</param>
        /// <param name="value">成功時に得た範囲内の値。</param>
        /// <param name="error">生成できなかった理由。</param>
        /// <returns>生成できた場合にtrue。</returns>
        public bool TryNextUInt64(ulong exclusiveMax, out ulong value, out DeterministicRandomError error)
        {
            if (exclusiveMax == 0UL)
            {
                value = 0UL;
                error = DeterministicRandomError.InvalidRange;
                return false;
            }

            value = NextBounded(exclusiveMax);
            error = DeterministicRandomError.None;
            return true;
        }

        /// <summary>minInclusive以上maxExclusive未満の偏りのないint値を返す。不正範囲なら状態を変更しない。</summary>
        /// <param name="minInclusive">含む下端。</param>
        /// <param name="maxExclusive">含まない上端。</param>
        /// <param name="value">成功時に得た範囲内の値。</param>
        /// <param name="error">生成できなかった理由。</param>
        /// <returns>生成できた場合にtrue。</returns>
        public bool TryNextInt32(int minInclusive, int maxExclusive, out int value, out DeterministicRandomError error)
        {
            if (minInclusive >= maxExclusive)
            {
                value = 0;
                error = DeterministicRandomError.InvalidRange;
                return false;
            }

            var width = (ulong)((long)maxExclusive - minInclusive);
            var offset = NextBounded(width);
            value = (int)((long)minInclusive + (long)offset);
            error = DeterministicRandomError.None;
            return true;
        }

        /// <summary>streamを指定保存状態へ戻す。不正状態なら現在状態を変更しない。</summary>
        /// <param name="state">復元するalgorithm version付き状態。</param>
        /// <returns>復元できた場合はNone。それ以外は失敗理由。</returns>
        public DeterministicRandomError Reset(DeterministicRandomState state)
        {
            var error = ValidateState(state);
            if (error == DeterministicRandomError.None) Apply(state);
            return error;
        }

        private ulong NextBounded(ulong exclusiveMax)
        {
            unchecked
            {
                var threshold = (0UL - exclusiveMax) % exclusiveMax;
                ulong candidate;
                do candidate = NextUInt64(); while (candidate < threshold);
                return candidate % exclusiveMax;
            }
        }

        private void Apply(DeterministicRandomState state)
        {
            _word0 = state.Word0;
            _word1 = state.Word1;
            _word2 = state.Word2;
            _word3 = state.Word3;
        }

        private static DeterministicRandomError ValidateState(DeterministicRandomState state)
        {
            return state.AlgorithmVersion != CurrentAlgorithmVersion || (state.Word0 | state.Word1 | state.Word2 | state.Word3) == 0UL
                ? DeterministicRandomError.InvalidState
                : DeterministicRandomError.None;
        }

        private static ulong NextSplitMix64(ref ulong state)
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                var value = state;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));
    }
}
