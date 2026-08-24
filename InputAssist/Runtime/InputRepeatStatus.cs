using System;

namespace InputRepeating
{
    /// <summary>1 pressed sample処理後のedgeと発行すべきtrigger数を表すimmutable snapshot。</summary>
    public readonly struct InputRepeatStatus : IEquatable<InputRepeatStatus>
    {
        /// <summary>処理後のsimulation tick。</summary>
        public ulong CurrentTick { get; }

        /// <summary>処理後に押下中か。</summary>
        public bool IsPressed { get; }

        /// <summary>このsampleで押下edgeの初回triggerを発行したか。</summary>
        public bool InitialTriggered { get; }

        /// <summary>このsampleまでに新しく期限へ到達したrepeat trigger数。</summary>
        public ulong RepeatTriggerCount { get; }

        /// <summary>このsampleで発行すべき初回とrepeatを合わせたtrigger数。</summary>
        public ulong TriggerCount { get; }

        /// <summary>このsampleで解放edgeを検出したか。</summary>
        public bool Released { get; }

        /// <summary>このsampleで1個以上のtriggerを発行すべきか。</summary>
        public bool Triggered => TriggerCount > 0;

        internal InputRepeatStatus(ulong currentTick, bool isPressed, bool initialTriggered, ulong repeatTriggerCount, ulong triggerCount, bool released)
        {
            CurrentTick = currentTick;
            IsPressed = isPressed;
            InitialTriggered = initialTriggered;
            RepeatTriggerCount = repeatTriggerCount;
            TriggerCount = triggerCount;
            Released = released;
        }

        /// <summary>全fieldが同じかを返す。</summary>
        /// <param name="other">比較するstatus。</param>
        /// <returns>全fieldが同じ場合true。</returns>
        public bool Equals(InputRepeatStatus other) => CurrentTick == other.CurrentTick && IsPressed == other.IsPressed && InitialTriggered == other.InitialTriggered && RepeatTriggerCount == other.RepeatTriggerCount && TriggerCount == other.TriggerCount && Released == other.Released;

        /// <summary>指定objectが同じstatusかを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じstatusの場合true。</returns>
        public override bool Equals(object obj) => obj is InputRepeatStatus other && Equals(other);

        /// <summary>全fieldからhash codeを返す。</summary>
        /// <returns>全fieldを反映したhash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CurrentTick.GetHashCode();
                hash = (hash * 397) ^ (IsPressed ? 1 : 0);
                hash = (hash * 397) ^ (InitialTriggered ? 1 : 0);
                hash = (hash * 397) ^ RepeatTriggerCount.GetHashCode();
                hash = (hash * 397) ^ TriggerCount.GetHashCode();
                return (hash * 397) ^ (Released ? 1 : 0);
            }
        }

        /// <summary>2つのstatusが同じかを返す。</summary>
        /// <param name="left">左辺のstatus。</param>
        /// <param name="right">右辺のstatus。</param>
        /// <returns>同じ場合true。</returns>
        public static bool operator ==(InputRepeatStatus left, InputRepeatStatus right) => left.Equals(right);

        /// <summary>2つのstatusが異なるかを返す。</summary>
        /// <param name="left">左辺のstatus。</param>
        /// <param name="right">右辺のstatus。</param>
        /// <returns>異なる場合true。</returns>
        public static bool operator !=(InputRepeatStatus left, InputRepeatStatus right) => !left.Equals(right);
    }
}
