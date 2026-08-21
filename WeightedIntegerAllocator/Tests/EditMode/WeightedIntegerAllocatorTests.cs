using System;
using System.Linq;
using NUnit.Framework;

namespace GameplayAllocation.Tests
{
    public sealed class WeightedIntegerAllocatorTests
    {
        [Test]
        public void TryAllocate_NullEntries_ReturnsExplicitError()
        {
            AssertFailure(null, 1, WeightedIntegerError.NullEntries);
        }

        [Test]
        public void TryAllocate_EmptyEntries_ReturnsExplicitError()
        {
            AssertFailure(Array.Empty<WeightedIntegerEntry>(), 1, WeightedIntegerError.InvalidEntryCount);
        }

        [Test]
        public void TryAllocate_TooManyEntries_ReturnsExplicitError()
        {
            AssertFailure(new WeightedIntegerEntry[WeightedIntegerAllocator.MaximumEntryCount + 1], 1, WeightedIntegerError.InvalidEntryCount);
        }

        [TestCase(-1)]
        [TestCase(1000000001)]
        public void TryAllocate_InvalidTotalUnits_ReturnsExplicitError(int totalUnits)
        {
            AssertFailure(Entries((1, 1)), totalUnits, WeightedIntegerError.InvalidTotalUnits);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TryAllocate_InvalidIdentifier_ReturnsExplicitError(int identifier)
        {
            AssertFailure(Entries((1, 1), (identifier, 1)), 1, WeightedIntegerError.InvalidEntryIdentifier);
        }

        [Test]
        public void TryAllocate_DuplicateIdentifier_ReturnsExplicitError()
        {
            AssertFailure(Entries((7, 1), (7, 2)), 1, WeightedIntegerError.DuplicateEntryIdentifier);
        }

        [TestCase(-1)]
        [TestCase(1000000001)]
        public void TryAllocate_InvalidWeight_ReturnsExplicitError(int weight)
        {
            AssertFailure(Entries((1, weight)), 1, WeightedIntegerError.InvalidWeight);
        }

        [Test]
        public void TryAllocate_PositiveTotalWithZeroWeight_ReturnsExplicitError()
        {
            AssertFailure(Entries((1, 0), (2, 0)), 1, WeightedIntegerError.ZeroTotalWeight);
        }

        [Test]
        public void TryAllocate_ValidationChecksTotalBeforeEntries()
        {
            AssertFailure(Entries((0, -1)), -1, WeightedIntegerError.InvalidTotalUnits);
        }

        [Test]
        public void TryAllocate_ValidationChecksIdentifierBeforeWeight()
        {
            AssertFailure(Entries((0, -1)), 1, WeightedIntegerError.InvalidEntryIdentifier);
        }

        [Test]
        public void EqualWeights_DistributeRemainderToFirstInput()
        {
            var result = Allocate(Entries((10, 1), (20, 1), (30, 1)), 10);
            Assert.That(result.TotalWeight, Is.EqualTo(3));
            Assert.That(result.RemainderUnitCount, Is.EqualTo(1));
            AssertLine(result, 0, 10, 1, 3, 1, true, 4);
            AssertLine(result, 1, 20, 1, 3, 1, false, 3);
            AssertLine(result, 2, 30, 1, 3, 1, false, 3);
        }

        [Test]
        public void ExactWeightedRatio_NeedsNoRemainderUnits()
        {
            var result = Allocate(Entries((1, 1), (2, 2), (3, 3)), 12);
            Assert.That(result.RemainderUnitCount, Is.Zero);
            AssertLine(result, 0, 1, 1, 2, 0, false, 2);
            AssertLine(result, 1, 2, 2, 4, 0, false, 4);
            AssertLine(result, 2, 3, 3, 6, 0, false, 6);
        }

        [Test]
        public void LargestRemainder_GivesUnitToHighestFraction()
        {
            var result = Allocate(Entries((1, 5), (2, 3), (3, 2)), 8);
            Assert.That(result.RemainderUnitCount, Is.EqualTo(1));
            AssertLine(result, 0, 1, 5, 4, 0, false, 4);
            AssertLine(result, 1, 2, 3, 2, 4, false, 2);
            AssertLine(result, 2, 3, 2, 1, 6, true, 2);
        }

        [Test]
        public void EqualRemainders_UseStableInputOrder()
        {
            var result = Allocate(Entries((30, 1), (20, 1), (10, 1)), 1);
            AssertLine(result, 0, 30, 1, 0, 1, true, 1);
            AssertLine(result, 1, 20, 1, 0, 1, false, 0);
            AssertLine(result, 2, 10, 1, 0, 1, false, 0);
        }

        [Test]
        public void MultipleRemainderUnits_UseNextStableEntries()
        {
            var result = Allocate(Entries((1, 1), (2, 1), (3, 1)), 2);
            Assert.That(result.RemainderUnitCount, Is.EqualTo(2));
            AssertLine(result, 0, 1, 1, 0, 2, true, 1);
            AssertLine(result, 1, 2, 1, 0, 2, true, 1);
            AssertLine(result, 2, 3, 1, 0, 2, false, 0);
        }

        [Test]
        public void ZeroWeightEntry_ReceivesNoUnits()
        {
            var result = Allocate(Entries((1, 0), (2, 4)), 5);
            Assert.That(result.PositiveWeightEntryCount, Is.EqualTo(1));
            AssertLine(result, 0, 1, 0, 0, 0, false, 0);
            AssertLine(result, 1, 2, 4, 5, 0, false, 5);
        }

        [Test]
        public void ZeroTotalWithAllZeroWeights_Succeeds()
        {
            var result = Allocate(Entries((1, 0), (2, 0)), 0);
            Assert.That(result.TotalUnits, Is.Zero);
            Assert.That(result.TotalAllocatedUnits, Is.Zero);
            Assert.That(result.TotalWeight, Is.Zero);
            Assert.That(result.PositiveWeightEntryCount, Is.Zero);
            Assert.That(result.RemainderUnitCount, Is.Zero);
            AssertLine(result, 0, 1, 0, 0, 0, false, 0);
            AssertLine(result, 1, 2, 0, 0, 0, false, 0);
        }

        [Test]
        public void ZeroTotalStillReportsPositiveWeightMetadata()
        {
            var result = Allocate(Entries((1, 4), (2, 6)), 0);
            Assert.That(result.TotalWeight, Is.EqualTo(10));
            Assert.That(result.PositiveWeightEntryCount, Is.EqualTo(2));
            AssertLine(result, 0, 1, 4, 0, 0, false, 0);
            AssertLine(result, 1, 2, 6, 0, 0, false, 0);
        }

        [Test]
        public void MaximumBoundaries_DoNotOverflow()
        {
            var result = Allocate(Entries((1, WeightedIntegerAllocator.MaximumWeight)), WeightedIntegerAllocator.MaximumTotalUnits);
            Assert.That(result.TotalWeight, Is.EqualTo((long)WeightedIntegerAllocator.MaximumWeight));
            AssertLine(result, 0, 1, WeightedIntegerAllocator.MaximumWeight, WeightedIntegerAllocator.MaximumTotalUnits, 0, false, WeightedIntegerAllocator.MaximumTotalUnits);
        }

        [Test]
        public void MaximumEntryCount_SumsWeightsInLong()
        {
            var entries = Enumerable.Range(1, WeightedIntegerAllocator.MaximumEntryCount)
                .Select(index => new WeightedIntegerEntry(index, WeightedIntegerAllocator.MaximumWeight))
                .ToArray();
            var result = Allocate(entries, 32);
            Assert.That(result.EntryCount, Is.EqualTo(32));
            Assert.That(result.TotalWeight, Is.EqualTo(32_000_000_000L));
            Assert.That(Sum(result), Is.EqualTo(32));
            for (var index = 0; index < 32; index++) AssertLine(result, index, index + 1, WeightedIntegerAllocator.MaximumWeight, 1, 0, false, 1);
        }

        [Test]
        public void TotalSmallerThanEntryCount_UsesStableFirstRecipients()
        {
            var result = Allocate(Entries((1, 1), (2, 1), (3, 1), (4, 1)), 2);
            Assert.That(Get(result, 0).AllocatedUnits, Is.EqualTo(1));
            Assert.That(Get(result, 1).AllocatedUnits, Is.EqualTo(1));
            Assert.That(Get(result, 2).AllocatedUnits, Is.Zero);
            Assert.That(Get(result, 3).AllocatedUnits, Is.Zero);
        }

        [Test]
        public void ScalingAllWeightsPreservesAllocation()
        {
            var first = Allocate(Entries((1, 1), (2, 2), (3, 3)), 17);
            var second = Allocate(Entries((1, 10), (2, 20), (3, 30)), 17);
            for (var index = 0; index < 3; index++)
                Assert.That(Get(second, index).AllocatedUnits, Is.EqualTo(Get(first, index).AllocatedUnits));
        }

        [Test]
        public void AllocationAlwaysSumsToRequestedTotal()
        {
            var scenarios = new[]
            {
                (Entries((1, 7), (2, 5), (3, 3), (4, 1)), 17),
                (Entries((1, 999), (2, 1)), 777),
                (Entries((1, 0), (2, 2), (3, 5)), 13),
                (Entries((1, 4), (2, 4), (3, 4), (4, 4), (5, 4)), 3)
            };
            foreach (var scenario in scenarios)
            {
                var result = Allocate(scenario.Item1, scenario.Item2);
                Assert.That(Sum(result), Is.EqualTo(scenario.Item2));
                Assert.That(result.TotalAllocatedUnits, Is.EqualTo(scenario.Item2));
            }
        }

        [Test]
        public void LinesPreserveInputOrder()
        {
            var result = Allocate(Entries((90, 3), (10, 5), (50, 2)), 8);
            Assert.That(Get(result, 0).EntryIdentifier, Is.EqualTo(90));
            Assert.That(Get(result, 1).EntryIdentifier, Is.EqualTo(10));
            Assert.That(Get(result, 2).EntryIdentifier, Is.EqualTo(50));
            Assert.That(Get(result, 0).InputIndex, Is.Zero);
            Assert.That(Get(result, 2).InputIndex, Is.EqualTo(2));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void TryGetLine_InvalidIndexReturnsFalse(int index)
        {
            var result = Allocate(Entries((1, 1), (2, 1)), 1);
            Assert.That(result.TryGetLine(index, out var line), Is.False);
            Assert.That(line, Is.EqualTo(default(WeightedIntegerAllocationLine)));
        }

        [Test]
        public void AllocationDoesNotChangeWhenInputArrayChanges()
        {
            var entries = Entries((1, 1), (2, 2));
            var result = Allocate(entries, 5);
            entries[0] = new WeightedIntegerEntry(99, 1_000_000_000);
            entries[1] = new WeightedIntegerEntry(100, 0);
            AssertLine(result, 0, 1, 1, 1, 2, true, 2);
            AssertLine(result, 1, 2, 2, 3, 1, false, 3);
        }

        [Test]
        public void RepeatedAllocationIsDeterministic()
        {
            var entries = Entries((1, 5), (2, 3), (3, 2));
            var first = Allocate(entries, 23);
            var second = Allocate(entries, 23);
            Assert.That(second.TotalWeight, Is.EqualTo(first.TotalWeight));
            Assert.That(second.RemainderUnitCount, Is.EqualTo(first.RemainderUnitCount));
            for (var index = 0; index < first.EntryCount; index++)
            {
                var a = Get(first, index);
                var b = Get(second, index);
                Assert.That(b.EntryIdentifier, Is.EqualTo(a.EntryIdentifier));
                Assert.That(b.BaseUnits, Is.EqualTo(a.BaseUnits));
                Assert.That(b.RemainderNumerator, Is.EqualTo(a.RemainderNumerator));
                Assert.That(b.ReceivedRemainderUnit, Is.EqualTo(a.ReceivedRemainderUnit));
            }
        }

        [Test]
        public void RuntimeAssemblyExportsOnlyDocumentedPublicTypes()
        {
            var names = typeof(WeightedIntegerAllocator).Assembly.GetExportedTypes().Select(type => type.FullName).OrderBy(value => value).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "GameplayAllocation.WeightedIntegerAllocation",
                "GameplayAllocation.WeightedIntegerAllocationLine",
                "GameplayAllocation.WeightedIntegerAllocator",
                "GameplayAllocation.WeightedIntegerEntry",
                "GameplayAllocation.WeightedIntegerError"
            }, names);
        }

        private static WeightedIntegerEntry[] Entries(params (int identifier, int weight)[] values)
        {
            return values.Select(value => new WeightedIntegerEntry(value.identifier, value.weight)).ToArray();
        }

        private static WeightedIntegerAllocation Allocate(WeightedIntegerEntry[] entries, int totalUnits)
        {
            Assert.That(WeightedIntegerAllocator.TryAllocate(entries, totalUnits, out var allocation, out var error), Is.True);
            Assert.That(error, Is.EqualTo(WeightedIntegerError.None));
            Assert.That(allocation, Is.Not.Null);
            return allocation;
        }

        private static void AssertFailure(WeightedIntegerEntry[] entries, int totalUnits, WeightedIntegerError expected)
        {
            Assert.That(WeightedIntegerAllocator.TryAllocate(entries, totalUnits, out var allocation, out var error), Is.False);
            Assert.That(allocation, Is.Null);
            Assert.That(error, Is.EqualTo(expected));
        }

        private static WeightedIntegerAllocationLine Get(WeightedIntegerAllocation allocation, int index)
        {
            Assert.That(allocation.TryGetLine(index, out var line), Is.True);
            return line;
        }

        private static int Sum(WeightedIntegerAllocation allocation)
        {
            var total = 0;
            for (var index = 0; index < allocation.EntryCount; index++) total += Get(allocation, index).AllocatedUnits;
            return total;
        }

        private static void AssertLine(WeightedIntegerAllocation allocation, int index, int identifier, int weight, int baseUnits, long remainder, bool bonus, int allocated)
        {
            var line = Get(allocation, index);
            Assert.That(line.EntryIdentifier, Is.EqualTo(identifier));
            Assert.That(line.InputIndex, Is.EqualTo(index));
            Assert.That(line.Weight, Is.EqualTo(weight));
            Assert.That(line.BaseUnits, Is.EqualTo(baseUnits));
            Assert.That(line.RemainderNumerator, Is.EqualTo(remainder));
            Assert.That(line.ReceivedRemainderUnit, Is.EqualTo(bonus));
            Assert.That(line.AllocatedUnits, Is.EqualTo(allocated));
        }
    }
}
