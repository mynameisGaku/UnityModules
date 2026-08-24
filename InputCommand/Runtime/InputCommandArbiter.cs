using System.Collections.Generic;

namespace InputArbitration
{
    /// <summary>ordered command候補から最大priorityと先頭tie-breakで1件を選ぶEngine非依存arbiter。</summary>
    public static class InputCommandArbiter
    {
        /// <summary>1回の仲裁へ渡せる候補数の上限。</summary>
        public const int MaximumCandidateCount = 64;

        /// <summary>全候補を検証し、eligible候補からpriority最大の1件を選ぶ。</summary>
        /// <param name="candidates">正の一意command id、priority、eligible状態を持つordered list。</param>
        /// <returns>選択内容、eligible数、明示error。priority同値では小さい入力indexが勝つ。</returns>
        public static InputCommandArbitrationResult Select(IReadOnlyList<InputCommandCandidate> candidates)
        {
            if (candidates == null) return InputCommandArbitrationResult.Failure(InputCommandArbitrationError.NullCandidates);
            if (candidates.Count > MaximumCandidateCount) return InputCommandArbitrationResult.Failure(InputCommandArbitrationError.TooManyCandidates);

            for (var index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].CommandId <= 0) return InputCommandArbitrationResult.Failure(InputCommandArbitrationError.InvalidCommandId);
                for (var previous = 0; previous < index; previous++)
                {
                    if (candidates[previous].CommandId == candidates[index].CommandId) return InputCommandArbitrationResult.Failure(InputCommandArbitrationError.DuplicateCommandId);
                }
            }

            var hasSelection = false;
            var selectedIndex = -1;
            var selectedCommandId = 0;
            var selectedPriority = 0;
            var eligibleCount = 0;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!candidate.IsEligible) continue;
                eligibleCount++;
                if (hasSelection && candidate.Priority <= selectedPriority) continue;
                hasSelection = true;
                selectedIndex = index;
                selectedCommandId = candidate.CommandId;
                selectedPriority = candidate.Priority;
            }

            return hasSelection
                ? InputCommandArbitrationResult.Selection(selectedIndex, selectedCommandId, selectedPriority, eligibleCount)
                : InputCommandArbitrationResult.NoSelection(eligibleCount);
        }
    }
}
