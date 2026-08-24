using System;

namespace SimulationClock
{
    /// <summary>固定step時間と1回に返す最大step数を表す不変値。</summary>
    public readonly struct FixedStepClockSettings : IEquatable<FixedStepClockSettings>
    {
        /// <summary>設定値を作る。妥当性はFixedStepClock.TryCreateで検証する。</summary>
        /// <param name="stepDurationTicks">1stepのTimeSpan tick数。1tickは100ns。</param>
        /// <param name="maximumStepsPerAdvance">1回のAdvanceで返す最大step数。</param>
        public FixedStepClockSettings(long stepDurationTicks, int maximumStepsPerAdvance)
        {
            StepDurationTicks = stepDurationTicks;
            MaximumStepsPerAdvance = maximumStepsPerAdvance;
        }

        /// <summary>1stepのTimeSpan tick数。1tickは100ns。</summary>
        public long StepDurationTicks { get; }

        /// <summary>1回のAdvanceで返す最大step数。</summary>
        public int MaximumStepsPerAdvance { get; }

        /// <summary>両設定値が等しい場合にtrue。</summary>
        public bool Equals(FixedStepClockSettings other) => StepDurationTicks == other.StepDurationTicks && MaximumStepsPerAdvance == other.MaximumStepsPerAdvance;

        /// <summary>同じ型で両設定値が等しい場合にtrue。</summary>
        public override bool Equals(object obj) => obj is FixedStepClockSettings other && Equals(other);

        /// <summary>両設定値からhash codeを作る。</summary>
        public override int GetHashCode() => HashCode.Combine(StepDurationTicks, MaximumStepsPerAdvance);

        /// <summary>両設定値が等しい場合にtrue。</summary>
        public static bool operator ==(FixedStepClockSettings left, FixedStepClockSettings right) => left.Equals(right);

        /// <summary>いずれかの設定値が異なる場合にtrue。</summary>
        public static bool operator !=(FixedStepClockSettings left, FixedStepClockSettings right) => !left.Equals(right);
    }
}
