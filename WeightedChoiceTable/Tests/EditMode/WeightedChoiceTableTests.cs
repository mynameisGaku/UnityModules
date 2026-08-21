using System;
using System.Linq;
using NUnit.Framework;

namespace GameplaySelection.Tests
{
    [TestFixture]
    public sealed class WeightedChoiceTableTests
    {
        [Test]
        public void Constructor_CreatesEmptyFiniteTable()
        {
            var table = new WeightedChoiceTable();

            Assert.That(table.EntryCount, Is.Zero);
            Assert.That(table.TotalWeight, Is.Zero);
        }

        [Test]
        public void Add_StoresEntryAndReportsBeforeAfterState()
        {
            var table = new WeightedChoiceTable();

            var result = table.Add(10, 6d);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Error, Is.EqualTo(WeightedChoiceError.None));
            Assert.That(result.AffectedIdentifier, Is.EqualTo(10));
            Assert.That(result.PreviousWeight, Is.Zero);
            Assert.That(result.CurrentWeight, Is.EqualTo(6d));
            Assert.That(result.PreviousTotalWeight, Is.Zero);
            Assert.That(result.CurrentTotalWeight, Is.EqualTo(6d));
            Assert.That(result.PreviousEntryCount, Is.Zero);
            Assert.That(result.CurrentEntryCount, Is.EqualTo(1));
        }

        [Test]
        public void Add_MaintainsIdentifierOrder()
        {
            var table = new WeightedChoiceTable();
            table.Add(30, 1d);
            table.Add(10, 6d);
            table.Add(20, 3d);

            AssertEntry(table, 0, 10, 6d);
            AssertEntry(table, 1, 20, 3d);
            AssertEntry(table, 2, 30, 1d);
            Assert.That(table.TotalWeight, Is.EqualTo(10d));
        }

        [Test]
        public void Add_InsertionOrderProducesSameSelections()
        {
            var ascending = CreateExampleTable(10, 20, 30);
            var descending = CreateExampleTable(30, 20, 10);

            foreach (var sample in new[] { 0d, 0.2d, 0.599999999d, 0.6d, 0.8d, 0.9d, 0.9999999999999999d })
                Assert.That(descending.Select(sample), Is.EqualTo(ascending.Select(sample)));
        }

        [Test]
        public void Add_DuplicateIdentifierIsRejectedWithoutMutation()
        {
            var table = new WeightedChoiceTable();
            table.Add(10, 6d);

            var result = table.Add(10, 99d);

            AssertFailureUnchanged(result, WeightedChoiceError.DuplicateIdentifier, 6d, 1);
            AssertEntry(table, 0, 10, 6d);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Add_InvalidIdentifierIsRejected(int identifier)
        {
            var table = new WeightedChoiceTable();

            var result = table.Add(identifier, 1d);

            AssertFailureUnchanged(result, WeightedChoiceError.InvalidIdentifier, 0d, 0);
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Add_InvalidWeightIsRejected(double weight)
        {
            var table = new WeightedChoiceTable();

            var result = table.Add(1, weight);

            AssertFailureUnchanged(result, WeightedChoiceError.InvalidWeight, 0d, 0);
        }

        [Test]
        public void Add_CapacityIsFixedAtThirtyTwo()
        {
            var table = new WeightedChoiceTable();
            for (var identifier = 1; identifier <= WeightedChoiceTable.MaximumEntryCount; identifier++)
                Assert.That(table.Add(identifier, 1d).Succeeded, Is.True);

            var rejected = table.Add(100, 1d);

            AssertFailureUnchanged(rejected, WeightedChoiceError.CapacityReached, 32d, 32);
        }

        [Test]
        public void Add_TotalOverflowRollsBack()
        {
            var table = new WeightedChoiceTable();
            table.Add(1, double.MaxValue);

            var rejected = table.Add(2, double.MaxValue);

            AssertFailureUnchanged(rejected, WeightedChoiceError.NumericOverflow, double.MaxValue, 1);
            Assert.That(table.TryGetEntry(2, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(WeightedChoiceError.EntryNotFound));
        }

        [Test]
        public void Update_ChangesWeightAndReportsTotals()
        {
            var table = CreateExampleTable(10, 20, 30);

            var result = table.Update(20, 5d);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.PreviousWeight, Is.EqualTo(3d));
            Assert.That(result.CurrentWeight, Is.EqualTo(5d));
            Assert.That(result.PreviousTotalWeight, Is.EqualTo(10d));
            Assert.That(result.CurrentTotalWeight, Is.EqualTo(12d));
            AssertEntry(table, 1, 20, 5d);
        }

        [Test]
        public void Update_SameWeightSucceedsWithoutChange()
        {
            var table = CreateExampleTable(10, 20, 30);

            var result = table.Update(20, 3d);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Changed, Is.False);
            Assert.That(result.PreviousTotalWeight, Is.EqualTo(10d));
            Assert.That(result.CurrentTotalWeight, Is.EqualTo(10d));
        }

        [Test]
        public void Update_MissingIdentifierIsRejected()
        {
            var table = CreateExampleTable(10, 20, 30);

            var result = table.Update(99, 2d);

            AssertFailureUnchanged(result, WeightedChoiceError.EntryNotFound, 10d, 3);
        }

        [Test]
        public void Update_InvalidWeightRollsBack()
        {
            var table = CreateExampleTable(10, 20, 30);

            var result = table.Update(20, double.NaN);

            AssertFailureUnchanged(result, WeightedChoiceError.InvalidWeight, 10d, 3);
            AssertEntry(table, 1, 20, 3d);
        }

        [Test]
        public void Update_TotalOverflowRollsBack()
        {
            var table = new WeightedChoiceTable();
            table.Add(1, double.MaxValue / 2d);
            table.Add(2, double.MaxValue / 2d);
            var previousTotal = table.TotalWeight;

            var result = table.Update(2, double.MaxValue);

            AssertFailureUnchanged(result, WeightedChoiceError.NumericOverflow, previousTotal, 2);
            AssertEntry(table, 1, 2, double.MaxValue / 2d);
        }

        [Test]
        public void Remove_CompactsEntriesAndReportsRemovedWeight()
        {
            var table = CreateExampleTable(10, 20, 30);

            var result = table.Remove(20);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.PreviousWeight, Is.EqualTo(3d));
            Assert.That(result.CurrentWeight, Is.Zero);
            Assert.That(result.PreviousTotalWeight, Is.EqualTo(10d));
            Assert.That(result.CurrentTotalWeight, Is.EqualTo(7d));
            Assert.That(result.CurrentEntryCount, Is.EqualTo(2));
            AssertEntry(table, 0, 10, 6d);
            AssertEntry(table, 1, 30, 1d);
        }

        [Test]
        public void Remove_LastEntryRestoresEmptyState()
        {
            var table = new WeightedChoiceTable();
            table.Add(1, double.Epsilon);

            var result = table.Remove(1);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(table.EntryCount, Is.Zero);
            Assert.That(table.TotalWeight, Is.Zero);
        }

        [Test]
        public void Remove_InvalidOrMissingIdentifierDoesNotMutate()
        {
            var table = CreateExampleTable(10, 20, 30);

            AssertFailureUnchanged(table.Remove(0), WeightedChoiceError.InvalidIdentifier, 10d, 3);
            AssertFailureUnchanged(table.Remove(99), WeightedChoiceError.EntryNotFound, 10d, 3);
        }

        [Test]
        public void Clear_RemovesAllEntriesAndSecondClearIsNoChange()
        {
            var table = CreateExampleTable(10, 20, 30);

            var first = table.Clear();
            var second = table.Clear();

            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.Changed, Is.True);
            Assert.That(first.PreviousTotalWeight, Is.EqualTo(10d));
            Assert.That(first.CurrentTotalWeight, Is.Zero);
            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.Changed, Is.False);
        }

        [Test]
        public void TryGetEntryAt_ReportsBoundsAndSnapshots()
        {
            var table = CreateExampleTable(10, 20, 30);

            Assert.That(table.TryGetEntryAt(1, out var entry, out var success), Is.True);
            Assert.That(entry.Identifier, Is.EqualTo(20));
            Assert.That(entry.Weight, Is.EqualTo(3d));
            Assert.That(success, Is.EqualTo(WeightedChoiceError.None));
            Assert.That(table.TryGetEntryAt(-1, out _, out var below), Is.False);
            Assert.That(below, Is.EqualTo(WeightedChoiceError.IndexOutOfRange));
            Assert.That(table.TryGetEntryAt(3, out _, out var above), Is.False);
            Assert.That(above, Is.EqualTo(WeightedChoiceError.IndexOutOfRange));
        }

        [Test]
        public void TryGetEntry_ReportsInvalidAndMissingIdentifiers()
        {
            var table = CreateExampleTable(10, 20, 30);

            Assert.That(table.TryGetEntry(30, out var found, out var success), Is.True);
            Assert.That(found.Weight, Is.EqualTo(1d));
            Assert.That(success, Is.EqualTo(WeightedChoiceError.None));
            Assert.That(table.TryGetEntry(0, out _, out var invalid), Is.False);
            Assert.That(invalid, Is.EqualTo(WeightedChoiceError.InvalidIdentifier));
            Assert.That(table.TryGetEntry(99, out _, out var missing), Is.False);
            Assert.That(missing, Is.EqualTo(WeightedChoiceError.EntryNotFound));
        }

        [Test]
        public void Select_EmptyTableReturnsExplicitError()
        {
            var result = new WeightedChoiceTable().Select(0.5d);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(WeightedChoiceError.EmptyTable));
            Assert.That(result.SelectedIndex, Is.EqualTo(-1));
            Assert.That(result.NormalizedSample, Is.EqualTo(0.5d));
        }

        [TestCase(-0.001d)]
        [TestCase(1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Select_InvalidSampleReturnsExplicitError(double sample)
        {
            var table = CreateExampleTable(10, 20, 30);

            var result = table.Select(sample);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(WeightedChoiceError.InvalidSample));
            Assert.That(result.SelectedIndex, Is.EqualTo(-1));
            Assert.That(table.TotalWeight, Is.EqualTo(10d));
        }

        [TestCase(0d, 10, 0)]
        [TestCase(0.599999999d, 10, 0)]
        [TestCase(0.6d, 20, 1)]
        [TestCase(0.899999999d, 20, 1)]
        [TestCase(0.9d, 30, 2)]
        [TestCase(0.9999999999999999d, 30, 2)]
        public void Select_UsesHalfOpenCumulativeIntervals(double sample, int expectedIdentifier, int expectedIndex)
        {
            var table = CreateExampleTable(10, 20, 30);

            var result = table.Select(sample);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Error, Is.EqualTo(WeightedChoiceError.None));
            Assert.That(result.SelectedIdentifier, Is.EqualTo(expectedIdentifier));
            Assert.That(result.SelectedIndex, Is.EqualTo(expectedIndex));
            Assert.That(result.IntervalStart, Is.LessThanOrEqualTo(result.Ticket));
            Assert.That(result.Ticket, Is.LessThan(result.IntervalEnd));
            Assert.That(result.TotalWeight, Is.EqualTo(10d));
        }

        [Test]
        public void Select_ExampleSamplesExposeExpectedIntervals()
        {
            var table = CreateExampleTable(10, 20, 30);

            var rare = table.Select(0.65d);
            var epic = table.Select(0.95d);

            AssertSelection(rare, 20, 1, 3d, 6d, 9d, 10d, 6.5d);
            AssertSelection(epic, 30, 2, 1d, 9d, 10d, 10d, 9.5d);
        }

        [Test]
        public void Select_MaximumFiniteWeightAndLargestSampleSelectsLastEntry()
        {
            var table = new WeightedChoiceTable();
            table.Add(1, double.MaxValue);

            var result = table.Select(0.9999999999999999d);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.SelectedIdentifier, Is.EqualTo(1));
            Assert.That(result.Ticket, Is.LessThan(result.TotalWeight));
        }

        [Test]
        public void Select_SubnormalWeightsKeepBoundaryOrder()
        {
            var table = new WeightedChoiceTable();
            table.Add(1, double.Epsilon);
            table.Add(2, double.Epsilon);

            Assert.That(table.Select(0d).SelectedIdentifier, Is.EqualTo(1));
            Assert.That(table.Select(0.5d).SelectedIdentifier, Is.EqualTo(2));
        }

        [Test]
        public void Select_DoesNotMutateTable()
        {
            var table = CreateExampleTable(10, 20, 30);

            var first = table.Select(0.65d);
            var second = table.Select(0.65d);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(table.EntryCount, Is.EqualTo(3));
            Assert.That(table.TotalWeight, Is.EqualTo(10d));
        }

        [Test]
        public void Update_ReweightsFutureSelection()
        {
            var table = CreateExampleTable(10, 20, 30);
            Assert.That(table.Select(0.65d).SelectedIdentifier, Is.EqualTo(20));

            table.Update(10, 9d);

            var result = table.Select(0.65d);
            Assert.That(result.SelectedIdentifier, Is.EqualTo(10));
            Assert.That(result.TotalWeight, Is.EqualTo(13d));
        }

        [Test]
        public void Remove_RebuildsIntervalsFromRemainingEntries()
        {
            var table = CreateExampleTable(10, 20, 30);
            table.Remove(10);

            var result = table.Select(0.75d);

            Assert.That(result.SelectedIdentifier, Is.EqualTo(30));
            Assert.That(result.IntervalStart, Is.EqualTo(3d));
            Assert.That(result.IntervalEnd, Is.EqualTo(4d));
        }

        [Test]
        public void Add_MaximumIdentifierIsValid()
        {
            var table = new WeightedChoiceTable();

            var result = table.Add(int.MaxValue, 1d);

            Assert.That(result.Succeeded, Is.True);
            AssertEntry(table, 0, int.MaxValue, 1d);
        }

        [Test]
        public void Select_NegativeZeroIsValidFirstBoundary()
        {
            var table = CreateExampleTable(10, 20, 30);

            var result = table.Select(-0d);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.SelectedIdentifier, Is.EqualTo(10));
        }

        [Test]
        public void PublicRuntimeSurface_ContainsOnlyFiveTypes()
        {
            var exported = typeof(WeightedChoiceTable).Assembly.GetExportedTypes().OrderBy(type => type.FullName).Select(type => type.FullName).ToArray();

            Assert.That(exported, Is.EqualTo(new[]
            {
                "GameplaySelection.WeightedChoiceChangeResult",
                "GameplaySelection.WeightedChoiceEntry",
                "GameplaySelection.WeightedChoiceError",
                "GameplaySelection.WeightedChoiceSelectionResult",
                "GameplaySelection.WeightedChoiceTable"
            }));
        }

        [Test]
        public void ValueResults_ImplementStableEquality()
        {
            var firstTable = CreateExampleTable(10, 20, 30);
            var secondTable = CreateExampleTable(10, 20, 30);
            var firstSelection = firstTable.Select(0.65d);
            var secondSelection = secondTable.Select(0.65d);
            var firstChange = firstTable.Update(20, 5d);
            var secondChange = secondTable.Update(20, 5d);
            Assert.That(firstTable.TryGetEntry(10, out var firstEntry, out _), Is.True);
            Assert.That(secondTable.TryGetEntry(10, out var secondEntry, out _), Is.True);

            Assert.That(firstSelection, Is.EqualTo(secondSelection));
            Assert.That(firstSelection.GetHashCode(), Is.EqualTo(secondSelection.GetHashCode()));
            Assert.That(firstChange, Is.EqualTo(secondChange));
            Assert.That(firstChange.GetHashCode(), Is.EqualTo(secondChange.GetHashCode()));
            Assert.That(firstEntry, Is.EqualTo(secondEntry));
        }

        private static WeightedChoiceTable CreateExampleTable(params int[] order)
        {
            var table = new WeightedChoiceTable();
            foreach (var identifier in order)
            {
                var weight = identifier == 10 ? 6d : identifier == 20 ? 3d : 1d;
                Assert.That(table.Add(identifier, weight).Succeeded, Is.True);
            }
            return table;
        }

        private static void AssertEntry(WeightedChoiceTable table, int index, int identifier, double weight)
        {
            Assert.That(table.TryGetEntryAt(index, out var entry, out var error), Is.True);
            Assert.That(error, Is.EqualTo(WeightedChoiceError.None));
            Assert.That(entry.Identifier, Is.EqualTo(identifier));
            Assert.That(entry.Weight, Is.EqualTo(weight));
        }

        private static void AssertFailureUnchanged(WeightedChoiceChangeResult result, WeightedChoiceError error, double totalWeight, int entryCount)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Changed, Is.False);
            Assert.That(result.Error, Is.EqualTo(error));
            Assert.That(result.PreviousTotalWeight, Is.EqualTo(totalWeight));
            Assert.That(result.CurrentTotalWeight, Is.EqualTo(totalWeight));
            Assert.That(result.PreviousEntryCount, Is.EqualTo(entryCount));
            Assert.That(result.CurrentEntryCount, Is.EqualTo(entryCount));
        }

        private static void AssertSelection(WeightedChoiceSelectionResult result, int identifier, int index, double weight, double start, double end, double total, double ticket)
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.SelectedIdentifier, Is.EqualTo(identifier));
            Assert.That(result.SelectedIndex, Is.EqualTo(index));
            Assert.That(result.SelectedWeight, Is.EqualTo(weight));
            Assert.That(result.IntervalStart, Is.EqualTo(start));
            Assert.That(result.IntervalEnd, Is.EqualTo(end));
            Assert.That(result.TotalWeight, Is.EqualTo(total));
            Assert.That(result.Ticket, Is.EqualTo(ticket).Within(1e-12d));
        }
    }
}
