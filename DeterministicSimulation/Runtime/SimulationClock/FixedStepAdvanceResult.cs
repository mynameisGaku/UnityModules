using System;

namespace SimulationClock
{
    /// <summary>今回実行する連続step範囲、端数、補間率、破棄量を表す不変結果。</summary>
    public readonly struct FixedStepAdvanceResult : IEquatable<FixedStepAdvanceResult>
    {
        internal FixedStepAdvanceResult(FixedStepClockError error, long firstStepIndex, int stepCount, long stepDurationTicks, long droppedStepCount, long droppedTicks, FixedStepClockState state)
        {
            Error = error;
            FirstStepIndex = firstStepIndex;
            StepCount = stepCount;
            StepDurationTicks = stepDurationTicks;
            DroppedStepCount = droppedStepCount;
            DroppedTicks = droppedTicks;
            State = state;
        }

        /// <summary>時計を進行できた場合にtrue。</summary>
        public bool IsSuccess => Error == FixedStepClockError.None;

        /// <summary>進行できなかった理由。成功時はNone。</summary>
        public FixedStepClockError Error { get; }

        /// <summary>今回返す最初のstep番号。stepが0件でも開始時の完了件数を返す。</summary>
        public long FirstStepIndex { get; }

        /// <summary>今回、利用側が順番に実行するstep数。</summary>
        public int StepCount { get; }

        /// <summary>各stepへ渡せる固定時間のTimeSpan tick数。</summary>
        public long StepDurationTicks { get; }

        /// <summary>catch-up上限を超え、今回実行しないことを明示したstep数。</summary>
        public long DroppedStepCount { get; }

        /// <summary>catch-up上限を超え、今回破棄したTimeSpan tick数。</summary>
        public long DroppedTicks { get; }

        /// <summary>進行後の保存・復元可能な時計状態。失敗時は進行前状態。</summary>
        public FixedStepClockState State { get; }

        /// <summary>次stepまでの0以上1未満の補間率。失敗時も現在状態から求める。</summary>
        public double InterpolationAlpha => StepDurationTicks > 0 ? (double)State.RemainderTicks / StepDurationTicks : 0d;

        /// <summary>すべての結果値が等しい場合にtrue。</summary>
        public bool Equals(FixedStepAdvanceResult other) => Error == other.Error && FirstStepIndex == other.FirstStepIndex && StepCount == other.StepCount && StepDurationTicks == other.StepDurationTicks && DroppedStepCount == other.DroppedStepCount && DroppedTicks == other.DroppedTicks && State == other.State;

        /// <summary>同じ型ですべての結果値が等しい場合にtrue。</summary>
        public override bool Equals(object obj) => obj is FixedStepAdvanceResult other && Equals(other);

        /// <summary>すべての結果値からhash codeを作る。</summary>
        public override int GetHashCode() => HashCode.Combine((int)Error, FirstStepIndex, StepCount, StepDurationTicks, DroppedStepCount, DroppedTicks, State);

        /// <summary>すべての結果値が等しい場合にtrue。</summary>
        public static bool operator ==(FixedStepAdvanceResult left, FixedStepAdvanceResult right) => left.Equals(right);

        /// <summary>いずれかの結果値が異なる場合にtrue。</summary>
        public static bool operator !=(FixedStepAdvanceResult left, FixedStepAdvanceResult right) => !left.Equals(right);
    }
}
