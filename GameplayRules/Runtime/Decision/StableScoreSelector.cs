namespace GameplayDecision
{
    /// <summary>現在候補を小さなscore差では維持し、十分に高いchallengerへだけ切り替える純粋関数を提供します。</summary>
    public static class StableScoreSelector
    {
        /// <summary>1回に評価できる最大候補数です。</summary>
        public const int MaximumCandidateCount = 32;

        /// <summary>候補scoreと最小優位差に使用できる最大値です。</summary>
        public const double MaximumNormalizedScore = 1d;

        /// <summary>候補列、現在識別値、切替に必要な最小優位差を検証して最終選択と全明細を構築します。</summary>
        public static bool TrySelect(
            StableScoreCandidate[] candidates,
            int currentIdentifier,
            double minimumAdvantage,
            out StableScoreSelection selection,
            out StableScoreError error)
        {
            selection = null;
            if (candidates == null)
            {
                error = StableScoreError.NullCandidates;
                return false;
            }

            if (candidates.Length < 1 || candidates.Length > MaximumCandidateCount)
            {
                error = StableScoreError.InvalidCandidateCount;
                return false;
            }

            if (currentIdentifier < 0)
            {
                error = StableScoreError.InvalidCurrentIdentifier;
                return false;
            }

            if (!IsFinite(minimumAdvantage) || minimumAdvantage < 0d || minimumAdvantage > MaximumNormalizedScore)
            {
                error = StableScoreError.InvalidMinimumAdvantage;
                return false;
            }

            for (var candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                var candidate = candidates[candidateIndex];
                if (candidate.Identifier <= 0)
                {
                    error = StableScoreError.InvalidCandidateIdentifier;
                    return false;
                }

                for (var previous = 0; previous < candidateIndex; previous++)
                {
                    if (candidates[previous].Identifier != candidate.Identifier) continue;
                    error = StableScoreError.DuplicateCandidateIdentifier;
                    return false;
                }

                if (!IsFinite(candidate.Score) || candidate.Score < 0d || candidate.Score > MaximumNormalizedScore)
                {
                    error = StableScoreError.InvalidScore;
                    return false;
                }
            }

            selection = StableScoreSelectionEngine.Select(candidates, currentIdentifier, minimumAdvantage);
            error = StableScoreError.None;
            return true;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
