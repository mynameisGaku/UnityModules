using System;

namespace GameplayAnalysis
{
    internal static class LinearTrendMath
    {
        internal static bool TryCalculate(double[] samples, int startIndex, int count, out LinearTrendEstimate estimate)
        {
            var maximumMagnitude = 0d;
            for (var offset = 0; offset < count; offset++)
            {
                var magnitude = Math.Abs(samples[startIndex + offset]);
                if (magnitude > maximumMagnitude) maximumMagnitude = magnitude;
            }

            if (maximumMagnitude == 0d)
            {
                estimate = new LinearTrendEstimate(count, 0d, 0d, 0d, 0d, 0d, 0d);
                return true;
            }

            var normalizedMean = 0d;
            for (var offset = 0; offset < count; offset++) normalizedMean += samples[startIndex + offset] / maximumMagnitude;
            normalizedMean /= count;

            var indexMean = (count - 1) * 0.5d;
            var covariance = 0d;
            var indexVariance = 0d;
            for (var offset = 0; offset < count; offset++)
            {
                var centeredIndex = offset - indexMean;
                var centeredSample = (samples[startIndex + offset] / maximumMagnitude) - normalizedMean;
                covariance += centeredIndex * centeredSample;
                indexVariance += centeredIndex * centeredIndex;
            }

            var normalizedSlope = covariance / indexVariance;
            var normalizedIntercept = normalizedMean - (normalizedSlope * indexMean);
            var normalizedPrediction = normalizedIntercept + (normalizedSlope * count);
            var mean = normalizedMean * maximumMagnitude;
            var slope = normalizedSlope * maximumMagnitude;
            var intercept = normalizedIntercept * maximumMagnitude;
            var prediction = normalizedPrediction * maximumMagnitude;
            if (!IsFinite(mean) || !IsFinite(slope) || !IsFinite(intercept) || !IsFinite(prediction))
            {
                estimate = default;
                return false;
            }

            estimate = new LinearTrendEstimate(count, samples[startIndex], samples[startIndex + count - 1], mean, slope, intercept, prediction);
            return true;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
