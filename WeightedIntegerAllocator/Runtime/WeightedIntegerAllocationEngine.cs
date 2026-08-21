namespace GameplayAllocation
{
    /// <summary>検証済みentryへ整数総量をlargest remainder方式で配分する内部engineです。</summary>
    internal static class WeightedIntegerAllocationEngine
    {
        internal static WeightedIntegerAllocation Allocate(WeightedIntegerEntry[] entries, int totalUnits, long totalWeight)
        {
            var count = entries.Length;
            var baseUnits = new int[count];
            var remainders = new long[count];
            var receivedRemainder = new bool[count];
            var baseTotal = 0L;
            var positiveCount = 0;

            if (totalWeight > 0)
            {
                for (var index = 0; index < count; index++)
                {
                    var entry = entries[index];
                    if (entry.Weight > 0) positiveCount++;
                    var product = (long)totalUnits * entry.Weight;
                    baseUnits[index] = (int)(product / totalWeight);
                    remainders[index] = product % totalWeight;
                    baseTotal += baseUnits[index];
                }
            }

            var remainderUnitCount = totalUnits - (int)baseTotal;
            for (var unit = 0; unit < remainderUnitCount; unit++)
            {
                var bestIndex = -1;
                for (var index = 0; index < count; index++)
                {
                    if (receivedRemainder[index]) continue;
                    if (bestIndex < 0 || remainders[index] > remainders[bestIndex]) bestIndex = index;
                }

                receivedRemainder[bestIndex] = true;
            }

            var lines = new WeightedIntegerAllocationLine[count];
            for (var index = 0; index < count; index++)
                lines[index] = new WeightedIntegerAllocationLine(entries[index].Identifier, index, entries[index].Weight, baseUnits[index], remainders[index], receivedRemainder[index]);
            return new WeightedIntegerAllocation(totalUnits, totalWeight, positiveCount, remainderUnitCount, lines);
        }
    }
}
