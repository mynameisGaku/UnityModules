using System;

namespace InputArbitration
{
    /// <summary>1 simulation stepで仲裁するcommand id、priority、選択対象状態を表すimmutable入力。</summary>
    public readonly struct InputCommandCandidate : IEquatable<InputCommandCandidate>
    {
        /// <summary>利用側が定義した正のcommand id。</summary>
        public int CommandId { get; }

        /// <summary>大きいほど優先する整数priority。</summary>
        public int Priority { get; }

        /// <summary>今回の仲裁で選択対象にするか。</summary>
        public bool IsEligible { get; }

        /// <summary>command候補の明示値を作る。</summary>
        public InputCommandCandidate(int commandId, int priority, bool isEligible)
        {
            CommandId = commandId;
            Priority = priority;
            IsEligible = isEligible;
        }

        /// <summary>command id、priority、eligible状態が同じかを返す。</summary>
        public bool Equals(InputCommandCandidate other) => CommandId == other.CommandId && Priority == other.Priority && IsEligible == other.IsEligible;

        /// <summary>指定objectが同じ候補かを返す。</summary>
        public override bool Equals(object obj) => obj is InputCommandCandidate other && Equals(other);

        /// <summary>候補のhash codeを返す。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CommandId;
                hash = (hash * 397) ^ Priority;
                return (hash * 397) ^ (IsEligible ? 1 : 0);
            }
        }

        /// <summary>2つの候補が同じかを返す。</summary>
        public static bool operator ==(InputCommandCandidate left, InputCommandCandidate right) => left.Equals(right);

        /// <summary>2つの候補が異なるかを返す。</summary>
        public static bool operator !=(InputCommandCandidate left, InputCommandCandidate right) => !left.Equals(right);
    }
}
