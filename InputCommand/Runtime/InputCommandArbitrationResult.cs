using System;

namespace InputArbitration
{
    /// <summary>候補検証とpriority仲裁の結果を表すimmutable出力。</summary>
    public readonly struct InputCommandArbitrationResult : IEquatable<InputCommandArbitrationResult>
    {
        private readonly bool _hasValue;

        /// <summary>入力を検証して仲裁を完了できたか。</summary>
        public bool Succeeded => _hasValue && Error == InputCommandArbitrationError.None;

        /// <summary>eligible候補から1件を選択したか。</summary>
        public bool HasSelection { get; }

        /// <summary>選択候補の入力index。未選択または失敗時は-1。</summary>
        public int SelectedIndex { get; }

        /// <summary>選択した正のcommand id。未選択または失敗時は0。</summary>
        public int CommandId { get; }

        /// <summary>選択したpriority。未選択または失敗時は0。</summary>
        public int Priority { get; }

        /// <summary>成功時に入力へ含まれていたeligible候補数。</summary>
        public int EligibleCandidateCount { get; }

        /// <summary>成功時None、失敗時は具体的な理由。</summary>
        public InputCommandArbitrationError Error { get; }

        private InputCommandArbitrationResult(bool hasSelection, int selectedIndex, int commandId, int priority, int eligibleCandidateCount, InputCommandArbitrationError error, bool hasValue)
        {
            HasSelection = hasSelection;
            SelectedIndex = selectedIndex;
            CommandId = commandId;
            Priority = priority;
            EligibleCandidateCount = eligibleCandidateCount;
            Error = error;
            _hasValue = hasValue;
        }

        internal static InputCommandArbitrationResult NoSelection(int eligibleCandidateCount) => new InputCommandArbitrationResult(false, -1, 0, 0, eligibleCandidateCount, InputCommandArbitrationError.None, true);

        internal static InputCommandArbitrationResult Selection(int selectedIndex, int commandId, int priority, int eligibleCandidateCount) => new InputCommandArbitrationResult(true, selectedIndex, commandId, priority, eligibleCandidateCount, InputCommandArbitrationError.None, true);

        internal static InputCommandArbitrationResult Failure(InputCommandArbitrationError error) => new InputCommandArbitrationResult(false, -1, 0, 0, 0, error, true);

        /// <summary>選択内容、候補数、error、結果保持状態が同じかを返す。</summary>
        public bool Equals(InputCommandArbitrationResult other) => HasSelection == other.HasSelection && SelectedIndex == other.SelectedIndex && CommandId == other.CommandId && Priority == other.Priority && EligibleCandidateCount == other.EligibleCandidateCount && Error == other.Error && _hasValue == other._hasValue;

        /// <summary>指定objectが同じ結果かを返す。</summary>
        public override bool Equals(object obj) => obj is InputCommandArbitrationResult other && Equals(other);

        /// <summary>結果のhash codeを返す。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = HasSelection ? 1 : 0;
                hash = (hash * 397) ^ SelectedIndex;
                hash = (hash * 397) ^ CommandId;
                hash = (hash * 397) ^ Priority;
                hash = (hash * 397) ^ EligibleCandidateCount;
                hash = (hash * 397) ^ (int)Error;
                return (hash * 397) ^ (_hasValue ? 1 : 0);
            }
        }

        /// <summary>2つの結果が同じかを返す。</summary>
        public static bool operator ==(InputCommandArbitrationResult left, InputCommandArbitrationResult right) => left.Equals(right);

        /// <summary>2つの結果が異なるかを返す。</summary>
        public static bool operator !=(InputCommandArbitrationResult left, InputCommandArbitrationResult right) => !left.Equals(right);
    }
}
