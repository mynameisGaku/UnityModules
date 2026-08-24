using System;

namespace InputMixing
{
    /// <summary>weighted averageの成分、入力件数、失敗位置を再構築可能に表すimmutableな結果。</summary>
    public readonly struct InputVectorMixResult : IEquatable<InputVectorMixResult>
    {
        private readonly bool _hasValue;

        /// <summary>成功時の合成horizontal成分。失敗時は0。</summary>
        public double Horizontal { get; }

        /// <summary>成功時の合成vertical成分。失敗時は0。</summary>
        public double Vertical { get; }

        /// <summary>成功時の入力weight合計。失敗時は0。</summary>
        public double TotalWeight { get; }

        /// <summary>検証対象だったcontribution総数。</summary>
        public int ContributionCount { get; }

        /// <summary>成功時に0より大きいweightを持ったcontribution数。失敗時は0。</summary>
        public int ActiveContributionCount { get; }

        /// <summary>浮動小数点の丸めで範囲を越えた出力を-1以上1以下へ戻したか。</summary>
        public bool WasNumericallyClamped { get; }

        /// <summary>失敗したcontribution index。配列全体の失敗または成功時は-1。</summary>
        public int InvalidContributionIndex { get; }

        /// <summary>成功時None、失敗時は具体的な理由。</summary>
        public InputVectorWeightedMixerError Error { get; }

        /// <summary>有効な合成結果を保持するか。</summary>
        public bool Succeeded => _hasValue && Error == InputVectorWeightedMixerError.None;

        private InputVectorMixResult(double horizontal, double vertical, double totalWeight, int contributionCount, int activeContributionCount, bool wasNumericallyClamped, int invalidContributionIndex, InputVectorWeightedMixerError error, bool hasValue)
        {
            Horizontal = horizontal;
            Vertical = vertical;
            TotalWeight = totalWeight;
            ContributionCount = contributionCount;
            ActiveContributionCount = activeContributionCount;
            WasNumericallyClamped = wasNumericallyClamped;
            InvalidContributionIndex = invalidContributionIndex;
            Error = error;
            _hasValue = hasValue;
        }

        internal static InputVectorMixResult Success(double horizontal, double vertical, double totalWeight, int contributionCount, int activeContributionCount, bool wasNumericallyClamped) => new InputVectorMixResult(horizontal, vertical, totalWeight, contributionCount, activeContributionCount, wasNumericallyClamped, -1, InputVectorWeightedMixerError.None, true);

        internal static InputVectorMixResult Failure(InputVectorWeightedMixerError error, int contributionCount, int invalidContributionIndex) => new InputVectorMixResult(0d, 0d, 0d, contributionCount, 0, false, invalidContributionIndex, error, false);

        /// <summary>全出力と成功状態が同じかを返す。</summary>
        /// <param name="other">比較する結果。</param>
        /// <returns>同じ結果の場合true。</returns>
        public bool Equals(InputVectorMixResult other) => Horizontal.Equals(other.Horizontal) && Vertical.Equals(other.Vertical) && TotalWeight.Equals(other.TotalWeight) && ContributionCount == other.ContributionCount && ActiveContributionCount == other.ActiveContributionCount && WasNumericallyClamped == other.WasNumericallyClamped && InvalidContributionIndex == other.InvalidContributionIndex && Error == other.Error && _hasValue == other._hasValue;

        /// <summary>指定objectが同じ結果かを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ場合true。</returns>
        public override bool Equals(object obj) => obj is InputVectorMixResult other && Equals(other);

        /// <summary>全出力と成功状態からhash codeを返す。</summary>
        /// <returns>結果に対応するhash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Horizontal.GetHashCode();
                hash = (hash * 397) ^ Vertical.GetHashCode();
                hash = (hash * 397) ^ TotalWeight.GetHashCode();
                hash = (hash * 397) ^ ContributionCount;
                hash = (hash * 397) ^ ActiveContributionCount;
                hash = (hash * 397) ^ (WasNumericallyClamped ? 1 : 0);
                hash = (hash * 397) ^ InvalidContributionIndex;
                hash = (hash * 397) ^ (int)Error;
                return (hash * 397) ^ (_hasValue ? 1 : 0);
            }
        }

        /// <summary>2つの結果が同じかを返す。</summary>
        /// <param name="left">左辺の結果。</param>
        /// <param name="right">右辺の結果。</param>
        /// <returns>同じ場合true。</returns>
        public static bool operator ==(InputVectorMixResult left, InputVectorMixResult right) => left.Equals(right);

        /// <summary>2つの結果が異なるかを返す。</summary>
        /// <param name="left">左辺の結果。</param>
        /// <param name="right">右辺の結果。</param>
        /// <returns>異なる場合true。</returns>
        public static bool operator !=(InputVectorMixResult left, InputVectorMixResult right) => !left.Equals(right);
    }
}
