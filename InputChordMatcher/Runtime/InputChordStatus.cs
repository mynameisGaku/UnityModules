using System;

namespace InputChording
{
    /// <summary>1 pressed snapshot処理後のchord状態と今回だけの判定を表すimmutable snapshot。</summary>
    public readonly struct InputChordStatus : IEquatable<InputChordStatus>
    {
        /// <summary>処理後のsimulation tick。</summary>
        public ulong CurrentTick { get; }

        /// <summary>chordに必要なcommand数。</summary>
        public int RequiredCommandCount { get; }

        /// <summary>処理後に押下中のrequired command数。</summary>
        public int PressedRequiredCommandCount { get; }

        /// <summary>処理後にrequired commandがすべて押下中か。</summary>
        public bool IsComplete { get; }

        /// <summary>今回completeへ入り、押下edge間隔が許容範囲内だったか。</summary>
        public bool Triggered { get; }

        /// <summary>今回completeへ入ったが押下edge間隔が許容範囲を超えたか。</summary>
        public bool SpanExceeded { get; }

        /// <summary>前回completeだったchordが今回incompleteへ戻ったか。</summary>
        public bool Rearmed { get; }

        /// <summary>complete時の最古と最新のrequired押下edge間のtick差。incomplete時は0。</summary>
        public ulong PressSpanTicks { get; }

        internal InputChordStatus(ulong currentTick, int requiredCommandCount, int pressedRequiredCommandCount, bool isComplete, bool triggered, bool spanExceeded, bool rearmed, ulong pressSpanTicks)
        {
            CurrentTick = currentTick;
            RequiredCommandCount = requiredCommandCount;
            PressedRequiredCommandCount = pressedRequiredCommandCount;
            IsComplete = isComplete;
            Triggered = triggered;
            SpanExceeded = spanExceeded;
            Rearmed = rearmed;
            PressSpanTicks = pressSpanTicks;
        }

        /// <summary>全fieldが同じかを返す。</summary>
        public bool Equals(InputChordStatus other) => CurrentTick == other.CurrentTick && RequiredCommandCount == other.RequiredCommandCount && PressedRequiredCommandCount == other.PressedRequiredCommandCount && IsComplete == other.IsComplete && Triggered == other.Triggered && SpanExceeded == other.SpanExceeded && Rearmed == other.Rearmed && PressSpanTicks == other.PressSpanTicks;

        /// <summary>指定objectが同じstatusかを返す。</summary>
        public override bool Equals(object obj) => obj is InputChordStatus other && Equals(other);

        /// <summary>全fieldからhash codeを返す。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CurrentTick.GetHashCode();
                hash = (hash * 397) ^ RequiredCommandCount;
                hash = (hash * 397) ^ PressedRequiredCommandCount;
                hash = (hash * 397) ^ (IsComplete ? 1 : 0);
                hash = (hash * 397) ^ (Triggered ? 1 : 0);
                hash = (hash * 397) ^ (SpanExceeded ? 1 : 0);
                hash = (hash * 397) ^ (Rearmed ? 1 : 0);
                return (hash * 397) ^ PressSpanTicks.GetHashCode();
            }
        }

        /// <summary>2つのstatusが同じかを返す。</summary>
        public static bool operator ==(InputChordStatus left, InputChordStatus right) => left.Equals(right);

        /// <summary>2つのstatusが異なるかを返す。</summary>
        public static bool operator !=(InputChordStatus left, InputChordStatus right) => !left.Equals(right);
    }
}
