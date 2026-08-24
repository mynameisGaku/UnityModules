namespace GameplayMetrics
{
    internal static class RollingSampleStatistics
    {
        internal static SampleWindowSnapshot CreateSnapshot(double[] samples, int start, int count)
        {
            var capacity = samples.Length;
            if (count == 0) return new SampleWindowSnapshot(capacity, 0, false, 0d, 0d, 0d, 0d, 0d);

            var oldest = samples[start];
            var minimum = oldest;
            var maximum = oldest;
            var mean = 0d;
            for (var index = 0; index < count; index++)
            {
                var sample = samples[(start + index) % capacity];
                if (sample < minimum) minimum = sample;
                if (sample > maximum) maximum = sample;
                if (index == 0)
                {
                    mean = sample;
                }
                else if (!sample.Equals(mean))
                {
                    var nextCount = index + 1d;
                    mean = mean * (index / nextCount) + sample / nextCount;
                }
            }

            var newest = samples[(start + count - 1) % capacity];
            return new SampleWindowSnapshot(capacity, count, true, minimum, maximum, mean, oldest, newest);
        }
    }
}
