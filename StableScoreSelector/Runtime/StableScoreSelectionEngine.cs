namespace GameplayDecision
{
    /// <summary>検証済み候補からcurrent維持または切替を入力順で決める内部engineです。</summary>
    internal static class StableScoreSelectionEngine
    {
        internal static StableScoreSelection Select(StableScoreCandidate[] candidates, int currentIdentifier, double minimumAdvantage)
        {
            var bestIndex = 0;
            var currentIndex = -1;
            for (var index = 0; index < candidates.Length; index++)
            {
                if (candidates[index].Score > candidates[bestIndex].Score) bestIndex = index;
                if (candidates[index].Identifier == currentIdentifier) currentIndex = index;
            }

            if (currentIndex < 0)
            {
                var reason = currentIdentifier == 0
                    ? StableScoreDecisionReason.SelectedWithoutCurrent
                    : StableScoreDecisionReason.ReplacedMissingCurrent;
                return Create(candidates, currentIdentifier, currentIndex, bestIndex, -1, bestIndex, minimumAdvantage, reason);
            }

            var challengerIndex = FindBestChallenger(candidates, currentIndex);
            if (challengerIndex < 0)
                return Create(candidates, currentIdentifier, currentIndex, bestIndex, challengerIndex, currentIndex, minimumAdvantage, StableScoreDecisionReason.KeptOnlyCurrent);

            var currentScore = candidates[currentIndex].Score;
            var challengerScore = candidates[challengerIndex].Score;
            var advantage = challengerScore - currentScore;
            if (challengerScore <= currentScore)
                return Create(candidates, currentIdentifier, currentIndex, bestIndex, challengerIndex, currentIndex, minimumAdvantage, StableScoreDecisionReason.KeptCurrentTieOrLower);
            if (advantage < minimumAdvantage)
                return Create(candidates, currentIdentifier, currentIndex, bestIndex, challengerIndex, currentIndex, minimumAdvantage, StableScoreDecisionReason.KeptCurrentBelowMinimumAdvantage);
            return Create(candidates, currentIdentifier, currentIndex, bestIndex, challengerIndex, challengerIndex, minimumAdvantage, StableScoreDecisionReason.SwitchedByMinimumAdvantage);
        }

        private static int FindBestChallenger(StableScoreCandidate[] candidates, int currentIndex)
        {
            var challengerIndex = -1;
            for (var index = 0; index < candidates.Length; index++)
            {
                if (index == currentIndex) continue;
                if (challengerIndex < 0 || candidates[index].Score > candidates[challengerIndex].Score) challengerIndex = index;
            }

            return challengerIndex;
        }

        private static StableScoreSelection Create(
            StableScoreCandidate[] candidates,
            int currentIdentifier,
            int currentIndex,
            int bestIndex,
            int challengerIndex,
            int selectedIndex,
            double minimumAdvantage,
            StableScoreDecisionReason reason)
        {
            var lines = new StableScoreCandidateLine[candidates.Length];
            for (var index = 0; index < candidates.Length; index++)
            {
                var candidate = candidates[index];
                lines[index] = new StableScoreCandidateLine(candidate.Identifier, index, candidate.Score, index == currentIndex, index == bestIndex, index == selectedIndex);
            }

            var currentScore = currentIndex < 0 ? 0d : candidates[currentIndex].Score;
            var challengerScore = challengerIndex < 0 ? 0d : candidates[challengerIndex].Score;
            return new StableScoreSelection(
                currentIdentifier,
                currentIndex >= 0,
                currentIndex,
                currentScore,
                candidates[bestIndex].Identifier,
                bestIndex,
                candidates[bestIndex].Score,
                challengerIndex >= 0,
                challengerIndex < 0 ? 0 : candidates[challengerIndex].Identifier,
                challengerIndex,
                challengerScore,
                challengerIndex < 0 ? 0d : challengerScore - currentScore,
                minimumAdvantage,
                candidates[selectedIndex].Identifier,
                selectedIndex,
                candidates[selectedIndex].Score,
                reason,
                lines);
        }
    }
}
