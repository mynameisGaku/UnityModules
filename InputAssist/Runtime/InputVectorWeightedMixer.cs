using System;

namespace InputMixing
{
    /// <summary>順序付き2D contribution配列を非負weightの正規化加重平均へ変換する純粋processor。</summary>
    public static class InputVectorWeightedMixer
    {
        /// <summary>1回のMixで検証するcontribution上限。</summary>
        public const int MaximumContributionCount = 32;

        /// <summary>検証済みcontributionを正規化加重平均へ合成する。</summary>
        /// <param name="contributions">順序自体も再現入力となる再利用可能な配列。</param>
        /// <returns>成功時は合成成分、weight合計、有効件数。失敗時は状態を持たず失敗indexと理由。</returns>
        public static InputVectorMixResult Mix(InputVectorContribution[] contributions)
        {
            if (contributions == null) return InputVectorMixResult.Failure(InputVectorWeightedMixerError.NullInput, 0, -1);
            if (contributions.Length > MaximumContributionCount) return InputVectorMixResult.Failure(InputVectorWeightedMixerError.TooManyContributions, contributions.Length, -1);

            var maximumWeight = 0d;
            var activeContributionCount = 0;
            for (var index = 0; index < contributions.Length; index++)
            {
                var contribution = contributions[index];
                if (!IsFinite(contribution.Horizontal) || !IsFinite(contribution.Vertical)) return InputVectorMixResult.Failure(InputVectorWeightedMixerError.NonFiniteInput, contributions.Length, index);
                if (contribution.Horizontal < -1d || contribution.Horizontal > 1d || contribution.Vertical < -1d || contribution.Vertical > 1d) return InputVectorMixResult.Failure(InputVectorWeightedMixerError.InputOutOfRange, contributions.Length, index);
                if (!IsFinite(contribution.Weight)) return InputVectorMixResult.Failure(InputVectorWeightedMixerError.NonFiniteWeight, contributions.Length, index);
                if (contribution.Weight < 0d || contribution.Weight > 1d) return InputVectorMixResult.Failure(InputVectorWeightedMixerError.WeightOutOfRange, contributions.Length, index);
                if (contribution.Weight <= 0d) continue;
                activeContributionCount++;
                maximumWeight = Math.Max(maximumWeight, contribution.Weight);
            }

            if (maximumWeight == 0d) return InputVectorMixResult.Success(0d, 0d, 0d, contributions.Length, 0, false);

            var horizontalSum = 0d;
            var horizontalCorrection = 0d;
            var verticalSum = 0d;
            var verticalCorrection = 0d;
            var scaledWeightSum = 0d;
            var weightCorrection = 0d;
            for (var index = 0; index < contributions.Length; index++)
            {
                var contribution = contributions[index];
                if (contribution.Weight == 0d) continue;
                var scaledWeight = contribution.Weight / maximumWeight;
                AddCompensated(ref horizontalSum, ref horizontalCorrection, contribution.Horizontal * scaledWeight);
                AddCompensated(ref verticalSum, ref verticalCorrection, contribution.Vertical * scaledWeight);
                AddCompensated(ref scaledWeightSum, ref weightCorrection, scaledWeight);
            }

            var horizontal = horizontalSum / scaledWeightSum;
            var vertical = verticalSum / scaledWeightSum;
            var clampedHorizontal = ClampUnit(horizontal);
            var clampedVertical = ClampUnit(vertical);
            var wasNumericallyClamped = clampedHorizontal != horizontal || clampedVertical != vertical;
            return InputVectorMixResult.Success(clampedHorizontal, clampedVertical, maximumWeight * scaledWeightSum, contributions.Length, activeContributionCount, wasNumericallyClamped);
        }

        private static void AddCompensated(ref double sum, ref double correction, double value)
        {
            var adjusted = value - correction;
            var next = sum + adjusted;
            correction = (next - sum) - adjusted;
            sum = next;
        }

        private static double ClampUnit(double value) => value < -1d ? -1d : value > 1d ? 1d : value;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
