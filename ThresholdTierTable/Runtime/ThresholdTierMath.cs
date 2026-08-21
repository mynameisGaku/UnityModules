namespace GameplayProgression
{
    internal static class ThresholdTierMath
    {
        internal static double InverseLerp(double lower, double upper, double value)
        {
            var difference = upper - lower;
            double progress;
            if (double.IsInfinity(difference))
            {
                progress = ((value * 0.5d) - (lower * 0.5d)) / ((upper * 0.5d) - (lower * 0.5d));
            }
            else
            {
                progress = (value - lower) / difference;
            }

            if (progress <= 0d) return 0d;
            if (progress >= 1d) return 1d;
            return progress;
        }
    }
}
