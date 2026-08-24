using NUnit.Framework;

namespace GameplayInventory.Tests
{
    [TestFixture]
    public sealed class StackTransferPlannerTests
    {
        [Test]
        public void FullTransfer_ConsumesAndFillsInInputOrder()
        {
            var sources = new[] { Source(11, 5), Source(12, 5) };
            var destinations = new[] { Destination(21, 0, 5), Destination(22, 1, 6) };

            var succeeded = StackTransferPlanner.TryPlan(sources, destinations, 9, out var plan, out var error);

            Assert.That(succeeded, Is.True);
            Assert.That(error, Is.EqualTo(StackTransferError.None));
            Assert.That(plan.RequestedUnits, Is.EqualTo(9));
            Assert.That(plan.TransferredUnits, Is.EqualTo(9));
            Assert.That(plan.UnfulfilledUnits, Is.Zero);
            AssertSource(plan, 0, 11, 5, 5, 0);
            AssertSource(plan, 1, 12, 5, 4, 1);
            AssertDestination(plan, 0, 21, 0, 5, 5, 5);
            AssertDestination(plan, 1, 22, 1, 6, 4, 5);
        }

        [Test]
        public void DestinationCapacity_LimitsTransfer()
        {
            Assert.That(StackTransferPlanner.TryPlan(new[] { Source(1, 10) }, new[] { Destination(2, 4, 8) }, 8, out var plan, out _), Is.True);
            Assert.That(plan.TransferredUnits, Is.EqualTo(4));
            Assert.That(plan.UnfulfilledUnits, Is.EqualTo(4));
            AssertSource(plan, 0, 1, 10, 4, 6);
            AssertDestination(plan, 0, 2, 4, 8, 4, 8);
        }

        [Test]
        public void SourceAvailability_LimitsTransfer()
        {
            Assert.That(StackTransferPlanner.TryPlan(new[] { Source(1, 3) }, new[] { Destination(2, 0, 10) }, 8, out var plan, out _), Is.True);
            Assert.That(plan.TransferredUnits, Is.EqualTo(3));
            Assert.That(plan.UnfulfilledUnits, Is.EqualTo(5));
        }

        [Test]
        public void ZeroRequest_BuildsZeroDeltaLines()
        {
            Assert.That(StackTransferPlanner.TryPlan(new[] { Source(1, 4) }, new[] { Destination(2, 1, 5) }, 0, out var plan, out _), Is.True);
            Assert.That(plan.TransferredUnits, Is.Zero);
            AssertSource(plan, 0, 1, 4, 0, 4);
            AssertDestination(plan, 0, 2, 1, 5, 0, 1);
        }

        [Test]
        public void ZeroSourceUnits_IsValid()
        {
            Assert.That(StackTransferPlanner.TryPlan(new[] { Source(1, 0) }, new[] { Destination(2, 0, 5) }, 4, out var plan, out _), Is.True);
            Assert.That(plan.TransferredUnits, Is.Zero);
            Assert.That(plan.UnfulfilledUnits, Is.EqualTo(4));
        }

        [Test]
        public void FullDestination_IsValid()
        {
            Assert.That(StackTransferPlanner.TryPlan(new[] { Source(1, 5) }, new[] { Destination(2, 5, 5) }, 4, out var plan, out _), Is.True);
            Assert.That(plan.TransferredUnits, Is.Zero);
            Assert.That(plan.AvailableDestinationRoom, Is.Zero);
        }

        [Test]
        public void SameIdentifierAcrossContainers_IsValid()
        {
            Assert.That(StackTransferPlanner.TryPlan(new[] { Source(7, 2) }, new[] { Destination(7, 0, 2) }, 2, out var plan, out var error), Is.True);
            Assert.That(error, Is.EqualTo(StackTransferError.None));
            Assert.That(plan.TransferredUnits, Is.EqualTo(2));
        }

        [Test]
        public void SourceOrder_IsStable()
        {
            Assert.That(StackTransferPlanner.TryPlan(new[] { Source(9, 4), Source(1, 4), Source(5, 4) }, new[] { Destination(2, 0, 20) }, 6, out var plan, out _), Is.True);
            AssertSource(plan, 0, 9, 4, 4, 0);
            AssertSource(plan, 1, 1, 4, 2, 2);
            AssertSource(plan, 2, 5, 4, 0, 4);
        }

        [Test]
        public void DestinationOrder_IsStable()
        {
            Assert.That(StackTransferPlanner.TryPlan(new[] { Source(1, 8) }, new[] { Destination(9, 1, 4), Destination(2, 0, 4), Destination(5, 0, 4) }, 6, out var plan, out _), Is.True);
            AssertDestination(plan, 0, 9, 1, 4, 3, 4);
            AssertDestination(plan, 1, 2, 0, 4, 3, 3);
            AssertDestination(plan, 2, 5, 0, 4, 0, 0);
        }

        [Test]
        public void Totals_CanExceedIntWhileTransferRemainsBounded()
        {
            var sources = FilledSources(StackTransferPlanner.MaximumSourceCount, StackTransferPlanner.MaximumUnitCount);
            var destinations = FilledDestinations(StackTransferPlanner.MaximumDestinationCount, 0, StackTransferPlanner.MaximumUnitCount);
            Assert.That(StackTransferPlanner.TryPlan(sources, destinations, StackTransferPlanner.MaximumUnitCount, out var plan, out _), Is.True);
            Assert.That(plan.AvailableSourceUnits, Is.EqualTo(32_000_000_000L));
            Assert.That(plan.AvailableDestinationRoom, Is.EqualTo(32_000_000_000L));
            Assert.That(plan.TransferredUnits, Is.EqualTo(StackTransferPlanner.MaximumUnitCount));
        }

        [Test]
        public void Plan_DoesNotMutateInputs()
        {
            var sources = new[] { Source(1, 4), Source(2, 5) };
            var destinations = new[] { Destination(3, 1, 6), Destination(4, 2, 7) };
            Assert.That(StackTransferPlanner.TryPlan(sources, destinations, 7, out _, out _), Is.True);
            Assert.That(sources[0].AvailableUnits, Is.EqualTo(4));
            Assert.That(sources[1].AvailableUnits, Is.EqualTo(5));
            Assert.That(destinations[0].CurrentUnits, Is.EqualTo(1));
            Assert.That(destinations[1].CurrentUnits, Is.EqualTo(2));
        }

        [Test]
        public void TryGetLines_RejectsOutOfRange()
        {
            Assert.That(StackTransferPlanner.TryPlan(new[] { Source(1, 1) }, new[] { Destination(2, 0, 1) }, 1, out var plan, out _), Is.True);
            Assert.That(plan.TryGetSourceLine(-1, out _), Is.False);
            Assert.That(plan.TryGetSourceLine(1, out _), Is.False);
            Assert.That(plan.TryGetDestinationLine(-1, out _), Is.False);
            Assert.That(plan.TryGetDestinationLine(1, out _), Is.False);
        }

        [Test]
        public void Conservation_HoldsAcrossAllLines()
        {
            Assert.That(StackTransferPlanner.TryPlan(new[] { Source(1, 2), Source(2, 5), Source(3, 7) }, new[] { Destination(4, 1, 3), Destination(5, 5, 9), Destination(6, 0, 10) }, 11, out var plan, out _), Is.True);
            var moved = 0;
            for (var index = 0; index < plan.SourceLineCount; index++)
            {
                Assert.That(plan.TryGetSourceLine(index, out var line), Is.True);
                moved += line.MovedUnits;
                Assert.That(line.BeforeUnits, Is.EqualTo(line.MovedUnits + line.AfterUnits));
            }

            var received = 0;
            for (var index = 0; index < plan.DestinationLineCount; index++)
            {
                Assert.That(plan.TryGetDestinationLine(index, out var line), Is.True);
                received += line.ReceivedUnits;
                Assert.That(line.AfterUnits, Is.EqualTo(line.BeforeUnits + line.ReceivedUnits));
                Assert.That(line.AfterUnits, Is.LessThanOrEqualTo(line.Capacity));
            }

            Assert.That(moved, Is.EqualTo(plan.TransferredUnits));
            Assert.That(received, Is.EqualTo(plan.TransferredUnits));
        }

        [Test]
        public void NullSources_ReturnsExpectedError() => AssertFailure(null, new[] { Destination(1, 0, 1) }, 0, StackTransferError.NullSources);

        [Test]
        public void EmptySources_ReturnsExpectedError() => AssertFailure(new StackTransferSource[0], new[] { Destination(1, 0, 1) }, 0, StackTransferError.InvalidSourceCount);

        [Test]
        public void TooManySources_ReturnsExpectedError() => AssertFailure(FilledSources(33, 0), new[] { Destination(1, 0, 1) }, 0, StackTransferError.InvalidSourceCount);

        [Test]
        public void NullDestinations_ReturnsExpectedError() => AssertFailure(new[] { Source(1, 0) }, null, 0, StackTransferError.NullDestinations);

        [Test]
        public void EmptyDestinations_ReturnsExpectedError() => AssertFailure(new[] { Source(1, 0) }, new StackTransferDestination[0], 0, StackTransferError.InvalidDestinationCount);

        [Test]
        public void TooManyDestinations_ReturnsExpectedError() => AssertFailure(new[] { Source(1, 0) }, FilledDestinations(33, 0, 1), 0, StackTransferError.InvalidDestinationCount);

        [Test]
        public void NegativeRequest_ReturnsExpectedError() => AssertFailure(new[] { Source(1, 0) }, new[] { Destination(1, 0, 1) }, -1, StackTransferError.InvalidRequestedUnits);

        [Test]
        public void OversizedRequest_ReturnsExpectedError() => AssertFailure(new[] { Source(1, 0) }, new[] { Destination(1, 0, 1) }, StackTransferPlanner.MaximumUnitCount + 1, StackTransferError.InvalidRequestedUnits);

        [Test]
        public void NonPositiveSourceIdentifier_ReturnsExpectedError() => AssertFailure(new[] { Source(0, 0) }, new[] { Destination(1, 0, 1) }, 0, StackTransferError.InvalidSourceIdentifier);

        [Test]
        public void DuplicateSourceIdentifier_ReturnsExpectedError() => AssertFailure(new[] { Source(1, 0), Source(1, 1) }, new[] { Destination(1, 0, 1) }, 0, StackTransferError.DuplicateSourceIdentifier);

        [Test]
        public void NegativeSourceUnits_ReturnsExpectedError() => AssertFailure(new[] { Source(1, -1) }, new[] { Destination(1, 0, 1) }, 0, StackTransferError.InvalidSourceUnits);

        [Test]
        public void OversizedSourceUnits_ReturnsExpectedError() => AssertFailure(new[] { Source(1, StackTransferPlanner.MaximumUnitCount + 1) }, new[] { Destination(1, 0, 1) }, 0, StackTransferError.InvalidSourceUnits);

        [Test]
        public void NonPositiveDestinationIdentifier_ReturnsExpectedError() => AssertFailure(new[] { Source(1, 0) }, new[] { Destination(0, 0, 1) }, 0, StackTransferError.InvalidDestinationIdentifier);

        [Test]
        public void DuplicateDestinationIdentifier_ReturnsExpectedError() => AssertFailure(new[] { Source(1, 0) }, new[] { Destination(2, 0, 1), Destination(2, 0, 1) }, 0, StackTransferError.DuplicateDestinationIdentifier);

        [Test]
        public void ZeroDestinationCapacity_ReturnsExpectedError() => AssertFailure(new[] { Source(1, 0) }, new[] { Destination(2, 0, 0) }, 0, StackTransferError.InvalidDestinationCapacity);

        [Test]
        public void OversizedDestinationCapacity_ReturnsExpectedError() => AssertFailure(new[] { Source(1, 0) }, new[] { Destination(2, 0, StackTransferPlanner.MaximumUnitCount + 1) }, 0, StackTransferError.InvalidDestinationCapacity);

        [Test]
        public void NegativeDestinationUnits_ReturnsExpectedError() => AssertFailure(new[] { Source(1, 0) }, new[] { Destination(2, -1, 1) }, 0, StackTransferError.InvalidDestinationUnits);

        [Test]
        public void DestinationUnitsAboveCapacity_ReturnsExpectedError() => AssertFailure(new[] { Source(1, 0) }, new[] { Destination(2, 2, 1) }, 0, StackTransferError.InvalidDestinationUnits);

        [Test]
        public void ErrorPrecedence_IsSourcesThenDestinationsThenRequest()
        {
            AssertFailure(null, null, -1, StackTransferError.NullSources);
            AssertFailure(new[] { Source(1, 0) }, null, -1, StackTransferError.NullDestinations);
            AssertFailure(new[] { Source(0, 0) }, new[] { Destination(0, 0, 0) }, -1, StackTransferError.InvalidRequestedUnits);
        }

        [Test]
        public void BoundarySingleStacks_AcceptsMaximumValues()
        {
            Assert.That(StackTransferPlanner.TryPlan(new[] { Source(1, StackTransferPlanner.MaximumUnitCount) }, new[] { Destination(1, 0, StackTransferPlanner.MaximumUnitCount) }, StackTransferPlanner.MaximumUnitCount, out var plan, out var error), Is.True);
            Assert.That(error, Is.EqualTo(StackTransferError.None));
            Assert.That(plan.TransferredUnits, Is.EqualTo(StackTransferPlanner.MaximumUnitCount));
        }

        private static StackTransferSource Source(int identifier, int units) => new StackTransferSource(identifier, units);

        private static StackTransferDestination Destination(int identifier, int current, int capacity) => new StackTransferDestination(identifier, current, capacity);

        private static StackTransferSource[] FilledSources(int count, int units)
        {
            var values = new StackTransferSource[count];
            for (var index = 0; index < count; index++) values[index] = Source(index + 1, units);
            return values;
        }

        private static StackTransferDestination[] FilledDestinations(int count, int current, int capacity)
        {
            var values = new StackTransferDestination[count];
            for (var index = 0; index < count; index++) values[index] = Destination(index + 1, current, capacity);
            return values;
        }

        private static void AssertFailure(StackTransferSource[] sources, StackTransferDestination[] destinations, int requestedUnits, StackTransferError expected)
        {
            var succeeded = StackTransferPlanner.TryPlan(sources, destinations, requestedUnits, out var plan, out var error);
            Assert.That(succeeded, Is.False);
            Assert.That(plan, Is.Null);
            Assert.That(error, Is.EqualTo(expected));
        }

        private static void AssertSource(StackTransferPlan plan, int index, int identifier, int before, int moved, int after)
        {
            Assert.That(plan.TryGetSourceLine(index, out var line), Is.True);
            Assert.That(line.Index, Is.EqualTo(index));
            Assert.That(line.Identifier, Is.EqualTo(identifier));
            Assert.That(line.BeforeUnits, Is.EqualTo(before));
            Assert.That(line.MovedUnits, Is.EqualTo(moved));
            Assert.That(line.AfterUnits, Is.EqualTo(after));
        }

        private static void AssertDestination(StackTransferPlan plan, int index, int identifier, int before, int capacity, int received, int after)
        {
            Assert.That(plan.TryGetDestinationLine(index, out var line), Is.True);
            Assert.That(line.Index, Is.EqualTo(index));
            Assert.That(line.Identifier, Is.EqualTo(identifier));
            Assert.That(line.BeforeUnits, Is.EqualTo(before));
            Assert.That(line.Capacity, Is.EqualTo(capacity));
            Assert.That(line.ReceivedUnits, Is.EqualTo(received));
            Assert.That(line.AfterUnits, Is.EqualTo(after));
        }
    }
}
