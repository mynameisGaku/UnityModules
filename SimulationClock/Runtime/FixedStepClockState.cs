using System;

namespace SimulationClock
{
    /// <summary>固定step時計を同じ位置から再構築するための不変状態。</summary>
    public readonly struct FixedStepClockState : IEquatable<FixedStepClockState>
    {
        /// <summary>復元可能な状態値を作る。妥当性はFixedStepClock.TryCreateまたはResetで検証する。</summary>
        /// <param name="completedStepCount">これまで利用側へ返したstep総数。</param>
        /// <param name="remainderTicks">次stepに満たない端数tick。</param>
        /// <param name="totalDroppedTicks">catch-up上限により破棄した累積tick。</param>
        public FixedStepClockState(long completedStepCount, long remainderTicks, long totalDroppedTicks)
        {
            CompletedStepCount = completedStepCount;
            RemainderTicks = remainderTicks;
            TotalDroppedTicks = totalDroppedTicks;
        }

        /// <summary>これまで利用側へ返したstep総数。</summary>
        public long CompletedStepCount { get; }

        /// <summary>次stepに満たない端数tick。</summary>
        public long RemainderTicks { get; }

        /// <summary>catch-up上限により破棄した累積tick。</summary>
        public long TotalDroppedTicks { get; }

        /// <summary>すべての状態値が等しい場合にtrue。</summary>
        public bool Equals(FixedStepClockState other) => CompletedStepCount == other.CompletedStepCount && RemainderTicks == other.RemainderTicks && TotalDroppedTicks == other.TotalDroppedTicks;

        /// <summary>同じ型ですべての状態値が等しい場合にtrue。</summary>
        public override bool Equals(object obj) => obj is FixedStepClockState other && Equals(other);

        /// <summary>すべての状態値からhash codeを作る。</summary>
        public override int GetHashCode() => HashCode.Combine(CompletedStepCount, RemainderTicks, TotalDroppedTicks);

        /// <summary>すべての状態値が等しい場合にtrue。</summary>
        public static bool operator ==(FixedStepClockState left, FixedStepClockState right) => left.Equals(right);

        /// <summary>いずれかの状態値が異なる場合にtrue。</summary>
        public static bool operator !=(FixedStepClockState left, FixedStepClockState right) => !left.Equals(right);
    }
}
