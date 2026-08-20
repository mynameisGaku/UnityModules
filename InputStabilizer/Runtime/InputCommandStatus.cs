using System;

namespace InputStabilization
{
    /// <summary>1 sample処理後の確定commandと候補進捗を表すimmutable snapshot。</summary>
    public readonly struct InputCommandStatus : IEquatable<InputCommandStatus>
    {
        /// <summary>現在確定しているcommand。</summary>
        public short CurrentCommand { get; }

        /// <summary>確定待ちのcommand。待機中でない場合は現在値。</summary>
        public short CandidateCommand { get; }

        /// <summary>候補が連続したsample数。待機中でない場合は0。</summary>
        public int CandidateSampleCount { get; }

        /// <summary>確定に必要な連続sample数。</summary>
        public int RequiredConsecutiveSamples { get; }

        /// <summary>この処理で確定commandが変わったか。</summary>
        public bool Changed { get; }

        /// <summary>現在値とは異なる候補を待機しているか。</summary>
        public bool IsPending => CandidateSampleCount > 0;

        internal InputCommandStatus(short currentCommand, short candidateCommand, int candidateSampleCount, int requiredConsecutiveSamples, bool changed)
        {
            CurrentCommand = currentCommand;
            CandidateCommand = candidateCommand;
            CandidateSampleCount = candidateSampleCount;
            RequiredConsecutiveSamples = requiredConsecutiveSamples;
            Changed = changed;
        }

        /// <summary>全fieldが同じかを返す。</summary>
        /// <param name="other">比較するstatus。</param>
        /// <returns>同じ場合true。</returns>
        public bool Equals(InputCommandStatus other) => CurrentCommand == other.CurrentCommand && CandidateCommand == other.CandidateCommand && CandidateSampleCount == other.CandidateSampleCount && RequiredConsecutiveSamples == other.RequiredConsecutiveSamples && Changed == other.Changed;

        /// <summary>指定objectが同じstatusかを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ場合true。</returns>
        public override bool Equals(object obj) => obj is InputCommandStatus other && Equals(other);

        /// <summary>全fieldからhash codeを返す。</summary>
        /// <returns>statusのhash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CurrentCommand.GetHashCode();
                hash = (hash * 397) ^ CandidateCommand.GetHashCode();
                hash = (hash * 397) ^ CandidateSampleCount;
                hash = (hash * 397) ^ RequiredConsecutiveSamples;
                return (hash * 397) ^ (Changed ? 1 : 0);
            }
        }

        /// <summary>2つのstatusが同じかを返す。</summary>
        /// <param name="left">左辺。</param>
        /// <param name="right">右辺。</param>
        /// <returns>同じ場合true。</returns>
        public static bool operator ==(InputCommandStatus left, InputCommandStatus right) => left.Equals(right);

        /// <summary>2つのstatusが異なるかを返す。</summary>
        /// <param name="left">左辺。</param>
        /// <param name="right">右辺。</param>
        /// <returns>異なる場合true。</returns>
        public static bool operator !=(InputCommandStatus left, InputCommandStatus right) => !left.Equals(right);
    }
}
