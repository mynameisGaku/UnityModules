namespace GameplayDecision
{
    /// <summary>候補ごとのutility factorをstate変更なしで比較する純粋関数を提供します。</summary>
    public static class UtilityScoreEvaluator
    {
        /// <summary>1回に評価できる最大候補数です。</summary>
        public const int MaximumCandidateCount = 32;

        /// <summary>1候補に設定できる最大factor数です。</summary>
        public const int MaximumFactorCount = 16;

        /// <summary>1factorに設定できる最大weightです。</summary>
        public const double MaximumWeight = 1_000_000d;

        /// <summary>候補を入力順に検証・評価し、最高scoreの候補と全明細を構築します。</summary>
        public static bool TryEvaluate(UtilityScoreCandidate[] candidates, out UtilityScoreEvaluation evaluation, out UtilityScoreError error)
        {
            evaluation = null;
            if (candidates == null)
            {
                error = UtilityScoreError.NullCandidates;
                return false;
            }

            if (candidates.Length < 1 || candidates.Length > MaximumCandidateCount)
            {
                error = UtilityScoreError.InvalidCandidateCount;
                return false;
            }

            for (var candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                var candidate = candidates[candidateIndex];
                if (candidate.Identifier <= 0)
                {
                    error = UtilityScoreError.InvalidCandidateIdentifier;
                    return false;
                }

                for (var previous = 0; previous < candidateIndex; previous++)
                {
                    if (candidates[previous].Identifier != candidate.Identifier) continue;
                    error = UtilityScoreError.DuplicateCandidateIdentifier;
                    return false;
                }

                if (candidate.FactorCount < 1 || candidate.FactorCount > MaximumFactorCount)
                {
                    error = UtilityScoreError.InvalidFactorCount;
                    return false;
                }

                for (var factorIndex = 0; factorIndex < candidate.FactorCount; factorIndex++)
                {
                    candidate.TryGetFactor(factorIndex, out var factor);
                    if (factor.Identifier <= 0)
                    {
                        error = UtilityScoreError.InvalidFactorIdentifier;
                        return false;
                    }

                    for (var previous = 0; previous < factorIndex; previous++)
                    {
                        candidate.TryGetFactor(previous, out var previousFactor);
                        if (previousFactor.Identifier != factor.Identifier) continue;
                        error = UtilityScoreError.DuplicateFactorIdentifier;
                        return false;
                    }

                    if (!IsFinite(factor.Utility) || factor.Utility < 0d || factor.Utility > 1d)
                    {
                        error = UtilityScoreError.InvalidUtility;
                        return false;
                    }

                    if (!IsFinite(factor.Weight) || factor.Weight <= 0d || factor.Weight > MaximumWeight)
                    {
                        error = UtilityScoreError.InvalidWeight;
                        return false;
                    }
                }
            }

            evaluation = UtilityScoreEvaluationEngine.Evaluate(candidates);
            error = UtilityScoreError.None;
            return true;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
