// SPDX-License-Identifier: MIT

using System;

namespace ObjectPool
{
    /// <summary>1つのprefab poolの振る舞いを決める変更不能な設定。全field比較による値等価を持つ。</summary>
    public sealed class PrefabPoolSettings : IEquatable<PrefabPoolSettings>
    {
        /// <summary>既定設定を作る。アクティブ上限なし、最大idle 128、preload 0、Lifo。</summary>
        public PrefabPoolSettings()
            : this(0, 128, 0, PoolReuseOrder.Lifo)
        {
        }

        /// <summary>全値を指定して設定を作る。</summary>
        /// <param name="maximumActiveCount">同時に取り出せるinstance数の上限。0は無制限。</param>
        /// <param name="maximumIdleCount">idleとして保持するinstance数の上限。超過分は返却時に破壊する。</param>
        /// <param name="initialPreloadCount">起動時にidleへ用意したい数。<see cref="PrefabPool.PreloadInitial"/>で使う。</param>
        /// <param name="reuseOrder">idleからの取出し順序。</param>
        /// <exception cref="ArgumentOutOfRangeException">count系引数が負、または<paramref name="reuseOrder"/>が未定義値。</exception>
        public PrefabPoolSettings(
            int maximumActiveCount,
            int maximumIdleCount,
            int initialPreloadCount,
            PoolReuseOrder reuseOrder)
        {
            if (maximumActiveCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumActiveCount), maximumActiveCount, "0以上にしてください。");
            }

            if (maximumIdleCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumIdleCount), maximumIdleCount, "0以上にしてください。");
            }

            if (initialPreloadCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialPreloadCount), initialPreloadCount, "0以上にしてください。");
            }

            if (reuseOrder != PoolReuseOrder.Lifo && reuseOrder != PoolReuseOrder.Fifo)
            {
                throw new ArgumentOutOfRangeException(nameof(reuseOrder), (int)reuseOrder, "定義済みのPoolReuseOrderを指定してください。");
            }

            MaximumActiveCount = maximumActiveCount;
            MaximumIdleCount = maximumIdleCount;
            InitialPreloadCount = initialPreloadCount;
            ReuseOrder = reuseOrder;
        }

        /// <summary>既定設定。アクティブ上限なし、最大idle 128、preload 0、Lifo。</summary>
        public static PrefabPoolSettings Default { get; } = new PrefabPoolSettings();

        /// <summary>同時に取り出せるinstance数の上限。0は無制限。</summary>
        public int MaximumActiveCount { get; }

        /// <summary>idleとして保持するinstance数の上限。超過分は返却時に破壊する。</summary>
        public int MaximumIdleCount { get; }

        /// <summary>起動時にidleへ用意したい数。<see cref="PrefabPool.PreloadInitial"/>で消費する。自動生成は行わない。</summary>
        public int InitialPreloadCount { get; }

        /// <summary>idleからの取出し順序。</summary>
        public PoolReuseOrder ReuseOrder { get; }

        /// <summary>全設定が等しい場合はtrueを返す。</summary>
        /// <param name="other">比較する設定。</param>
        /// <returns>全fieldが等しい場合はtrue。nullとは常にfalse。</returns>
        public bool Equals(PrefabPoolSettings other)
        {
            if (other is null) return false;
            return MaximumActiveCount == other.MaximumActiveCount &&
                   MaximumIdleCount == other.MaximumIdleCount &&
                   InitialPreloadCount == other.InitialPreloadCount &&
                   ReuseOrder == other.ReuseOrder;
        }

        /// <summary>指定objectが同じ設定ならtrueを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ設定ならtrue。</returns>
        public override bool Equals(object obj) => obj is PrefabPoolSettings other && Equals(other);

        /// <summary>全設定からhash値を返す。</summary>
        /// <returns>設定のhash値。</returns>
        public override int GetHashCode() => HashCode.Combine(MaximumActiveCount, MaximumIdleCount, InitialPreloadCount, (int)ReuseOrder);

        /// <summary>左右の設定が等しい場合はtrueを返す。全field比較で判定する。</summary>
        /// <param name="left">左側の設定。</param>
        /// <param name="right">右側の設定。</param>
        /// <returns>左右が等しい場合はtrue。双方nullの場合もtrue。</returns>
        public static bool operator ==(PrefabPoolSettings left, PrefabPoolSettings right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        /// <summary>左右の設定が異なる場合はtrueを返す。</summary>
        /// <param name="left">左側の設定。</param>
        /// <param name="right">右側の設定。</param>
        /// <returns>左右が異なる場合はtrue。</returns>
        public static bool operator !=(PrefabPoolSettings left, PrefabPoolSettings right) => !(left == right);

        /// <summary>全設定を読みやすい1行へ整形する。</summary>
        /// <returns>設定の要約文字列。</returns>
        public override string ToString()
        {
            return $"MaximumActiveCount={MaximumActiveCount}, MaximumIdleCount={MaximumIdleCount}, " +
                   $"InitialPreloadCount={InitialPreloadCount}, ReuseOrder={ReuseOrder}";
        }
    }
}
