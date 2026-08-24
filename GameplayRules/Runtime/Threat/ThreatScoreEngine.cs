using System.Collections.Generic;

namespace GameplayThreat
{
    internal static class ThreatScoreEngine
    {
        internal static bool TryResolve(
            IReadOnlyList<ThreatScoreEntry> entries,
            IReadOnlyList<ThreatScoreAdjustment> adjustments,
            out ThreatScoreResolution resolution,
            out ThreatScoreError error,
            out int failureIndex)
        {
            resolution = null;
            failureIndex = -1;

            if (!TryValidateEntries(entries, out error, out failureIndex)
                || !TryValidateAdjustments(entries, adjustments, out error, out failureIndex))
            {
                return false;
            }

            var finalEntries = new ThreatScoreEntry[entries.Count];
            for (var index = 0; index < entries.Count; index++) finalEntries[index] = entries[index];
            var steps = new ThreatScoreStep[adjustments.Count];

            for (var adjustmentIndex = 0; adjustmentIndex < adjustments.Count; adjustmentIndex++)
            {
                var adjustment = adjustments[adjustmentIndex];
                var entryIndex = FindEntryIndex(finalEntries, adjustment.TargetId);
                var input = finalEntries[entryIndex].Score;
                var requested = adjustment.Delta;
                double output;
                var clamped = requested < -input;

                if (clamped)
                {
                    output = 0d;
                }
                else
                {
                    output = input + requested;
                    if (!IsFinite(output))
                    {
                        error = ThreatScoreError.ScoreOverflow;
                        failureIndex = adjustmentIndex;
                        return false;
                    }
                }

                var applied = output - input;
                finalEntries[entryIndex] = new ThreatScoreEntry(adjustment.TargetId, output);
                steps[adjustmentIndex] = new ThreatScoreStep(adjustmentIndex, adjustment.TargetId, input, requested, applied, output, clamped);
            }

            var leaderId = finalEntries[0].TargetId;
            var leaderScore = finalEntries[0].Score;
            for (var index = 1; index < finalEntries.Length; index++)
            {
                var candidate = finalEntries[index];
                if (candidate.Score > leaderScore || (candidate.Score.Equals(leaderScore) && candidate.TargetId < leaderId))
                {
                    leaderId = candidate.TargetId;
                    leaderScore = candidate.Score;
                }
            }

            resolution = new ThreatScoreResolution(finalEntries, steps, leaderId, leaderScore);
            error = ThreatScoreError.None;
            failureIndex = -1;
            return true;
        }

        private static bool TryValidateEntries(IReadOnlyList<ThreatScoreEntry> entries, out ThreatScoreError error, out int failureIndex)
        {
            failureIndex = -1;
            if (entries == null)
            {
                error = ThreatScoreError.NullEntries;
                return false;
            }

            if (entries.Count < 1 || entries.Count > ThreatScoreResolver.MaximumEntryCount)
            {
                error = ThreatScoreError.EntryCountOutOfRange;
                return false;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry.TargetId <= 0)
                {
                    error = ThreatScoreError.InvalidTargetId;
                    failureIndex = index;
                    return false;
                }

                if (!IsFinite(entry.Score) || entry.Score < 0d)
                {
                    error = ThreatScoreError.InvalidInitialScore;
                    failureIndex = index;
                    return false;
                }

                for (var previous = 0; previous < index; previous++)
                {
                    if (entries[previous].TargetId != entry.TargetId) continue;
                    error = ThreatScoreError.DuplicateTargetId;
                    failureIndex = index;
                    return false;
                }
            }

            error = ThreatScoreError.None;
            return true;
        }

        private static bool TryValidateAdjustments(
            IReadOnlyList<ThreatScoreEntry> entries,
            IReadOnlyList<ThreatScoreAdjustment> adjustments,
            out ThreatScoreError error,
            out int failureIndex)
        {
            failureIndex = -1;
            if (adjustments == null)
            {
                error = ThreatScoreError.NullAdjustments;
                return false;
            }

            if (adjustments.Count > ThreatScoreResolver.MaximumAdjustmentCount)
            {
                error = ThreatScoreError.AdjustmentCountOutOfRange;
                return false;
            }

            for (var index = 0; index < adjustments.Count; index++)
            {
                var adjustment = adjustments[index];
                if (!IsFinite(adjustment.Delta))
                {
                    error = ThreatScoreError.InvalidAdjustmentDelta;
                    failureIndex = index;
                    return false;
                }

                var found = false;
                for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++) found |= entries[entryIndex].TargetId == adjustment.TargetId;
                if (found) continue;
                error = ThreatScoreError.UnknownTargetId;
                failureIndex = index;
                return false;
            }

            error = ThreatScoreError.None;
            return true;
        }

        private static int FindEntryIndex(ThreatScoreEntry[] entries, int targetId)
        {
            for (var index = 0; index < entries.Length; index++)
                if (entries[index].TargetId == targetId) return index;
            return -1;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
