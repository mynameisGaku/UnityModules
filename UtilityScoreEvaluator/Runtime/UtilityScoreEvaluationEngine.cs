namespace GameplayDecision
{
    /// <summary>検証済み候補を入力順で評価する内部engineです。</summary>
    internal static class UtilityScoreEvaluationEngine
    {
        internal static UtilityScoreEvaluation Evaluate(UtilityScoreCandidate[] candidates)
        {
            var candidateLines = new UtilityScoreCandidateLine[candidates.Length];
            var selectedIndex = 0;
            var selectedScore = double.NegativeInfinity;

            for (var candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                var candidate = candidates[candidateIndex];
                var factors = candidate.CopyFactors();
                var factorLines = new UtilityScoreFactorLine[factors.Length];
                var totalWeight = 0d;
                var weightedUtility = 0d;

                for (var factorIndex = 0; factorIndex < factors.Length; factorIndex++)
                {
                    var factor = factors[factorIndex];
                    var contribution = factor.Utility * factor.Weight;
                    totalWeight += factor.Weight;
                    weightedUtility += contribution;
                    factorLines[factorIndex] = new UtilityScoreFactorLine(factor.Identifier, factor.Utility, factor.Weight, contribution);
                }

                var score = weightedUtility / totalWeight;
                candidateLines[candidateIndex] = new UtilityScoreCandidateLine(candidate.Identifier, candidateIndex, totalWeight, score, factorLines);
                if (score <= selectedScore) continue;
                selectedIndex = candidateIndex;
                selectedScore = score;
            }

            return new UtilityScoreEvaluation(candidates[selectedIndex].Identifier, selectedIndex, selectedScore, candidateLines);
        }
    }
}
