using System;

namespace InputSequencing
{
    /// <summary>1 command処理後のpattern進捗を表すimmutable snapshot。</summary>
    public readonly struct InputSequenceStatus : IEquatable<InputSequenceStatus>
    {
        /// <summary>処理後のsimulation tick。</summary>
        public ulong CurrentTick { get; }

        /// <summary>先頭から一致しているcommand数。match後は0。</summary>
        public int Progress { get; }

        /// <summary>pattern全体のcommand数。</summary>
        public int PatternLength { get; }

        /// <summary>次に期待するcommand id。</summary>
        public int ExpectedCommandId { get; }

        /// <summary>この入力でpattern全体が一致したか。</summary>
        public bool Matched { get; }

        /// <summary>この入力前に前回一致commandとのtick差が上限を超えたか。</summary>
        public bool TimedOut { get; }

        /// <summary>この入力が不一致だったため、それまでの進捗を破棄したか。</summary>
        public bool Restarted { get; }

        internal InputSequenceStatus(ulong currentTick, int progress, int patternLength, int expectedCommandId, bool matched, bool timedOut, bool restarted)
        {
            CurrentTick = currentTick;
            Progress = progress;
            PatternLength = patternLength;
            ExpectedCommandId = expectedCommandId;
            Matched = matched;
            TimedOut = timedOut;
            Restarted = restarted;
        }

        /// <summary>全fieldが同じかを返す。</summary>
        /// <param name="other">比較するstatus。</param>
        /// <returns>全fieldが同じ場合true。</returns>
        public bool Equals(InputSequenceStatus other) => CurrentTick == other.CurrentTick && Progress == other.Progress && PatternLength == other.PatternLength && ExpectedCommandId == other.ExpectedCommandId && Matched == other.Matched && TimedOut == other.TimedOut && Restarted == other.Restarted;

        /// <summary>指定objectが同じstatusかを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じstatusの場合true。</returns>
        public override bool Equals(object obj) => obj is InputSequenceStatus other && Equals(other);

        /// <summary>全fieldからhash codeを返す。</summary>
        /// <returns>全fieldを反映したhash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CurrentTick.GetHashCode();
                hash = (hash * 397) ^ Progress;
                hash = (hash * 397) ^ PatternLength;
                hash = (hash * 397) ^ ExpectedCommandId;
                hash = (hash * 397) ^ (Matched ? 1 : 0);
                hash = (hash * 397) ^ (TimedOut ? 1 : 0);
                return (hash * 397) ^ (Restarted ? 1 : 0);
            }
        }

        /// <summary>2つのstatusが同じかを返す。</summary>
        /// <param name="left">左辺のstatus。</param>
        /// <param name="right">右辺のstatus。</param>
        /// <returns>同じ場合true。</returns>
        public static bool operator ==(InputSequenceStatus left, InputSequenceStatus right) => left.Equals(right);

        /// <summary>2つのstatusが異なるかを返す。</summary>
        /// <param name="left">左辺のstatus。</param>
        /// <param name="right">右辺のstatus。</param>
        /// <returns>異なる場合true。</returns>
        public static bool operator !=(InputSequenceStatus left, InputSequenceStatus right) => !left.Equals(right);
    }
}
