using System;

namespace GameplayAnalysis
{
    internal static class SampleStatisticsMath
    {
        internal static bool TryCalculate(double[] samples, int startIndex, int count, out SampleStatisticsResult result)
        {
            var minimum = samples[startIndex];
            var maximum = minimum;
            var mean = 0d;
            var squaredDeviationTotal = 0d;

            for (var offset = 0; offset < count; offset++)
            {
                var sample = samples[startIndex + offset];
                if (sample < minimum) minimum = sample;
                if (sample > maximum) maximum = sample;

                var nextCount = offset + 1;
                var delta = sample - mean;
                mean += delta / nextCount;
                var deltaAfterMean = sample - mean;
                squaredDeviationTotal += delta * deltaAfterMean;
                if (!IsFinite(mean) || !IsFinite(squaredDeviationTotal))
                {
                    result = default;
                    return false;
                }
            }

            var range = maximum - minimum;
            var populationVariance = squaredDeviationTotal / count;
            if (populationVariance < 0d && populationVariance > -1e-12d) populationVariance = 0d;
            var populationStandardDeviation = Math.Sqrt(populationVariance);
            if (!IsFinite(range) || !IsFinite(populationVariance) || populationVariance < 0d || !IsFinite(populationStandardDeviation))
            {
                result = default;
                return false;
            }

            result = new SampleStatisticsResult(count, minimum, maximum, mean, range, populationVariance, populationStandardDeviation);
            return true;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
