using NUnit.Framework;

namespace GameplayTiming.Tests
{
    [TestFixture]
    public sealed class PeriodicTickPlannerTests
    {
        [Test]
        public void CompletedState_ProducesEmptyCompletedPlan()
        {
            AssertPlan(PeriodicTickState.Completed, 100, 10, 0, 0, -1, -1, out var plan);
            Assert.That(plan.IsCompleted, Is.True);
            Assert.That(plan.HasEmissions, Is.False);
            Assert.That(plan.WasLimited, Is.False);
        }

        [Test]
        public void ThroughBeforeNextTick_ProducesNoEmission()
        {
            var state = State(10, 4, 3);
            AssertPlan(state, 9, 10, 0, 0, -1, -1, out var plan);
            AssertState(plan.NextState, 10, 4, 3);
        }

        [Test]
        public void ExactNextTick_EmitsOne()
        {
            AssertPlan(State(10, 4, 3), 10, 10, 1, 1, 10, 10, out var plan);
            AssertState(plan.NextState, 14, 4, 2);
        }

        [Test]
        public void InclusiveBoundary_EmitsAllArrivedTicks()
        {
            AssertPlan(State(10, 4, 5), 22, 10, 4, 4, 10, 22, out var plan);
            AssertState(plan.NextState, 26, 4, 1);
        }

        [Test]
        public void BetweenBoundaries_DoesNotEmitFutureTick()
        {
            AssertPlan(State(10, 4, 5), 21, 10, 3, 3, 10, 18, out var plan);
            AssertState(plan.NextState, 22, 4, 2);
        }

        [Test]
        public void RemainingCount_LimitsDueCountAndCompletes()
        {
            AssertPlan(State(10, 4, 2), 100, 10, 2, 2, 10, 14, out var plan);
            AssertState(plan.NextState, 0, 1, 0);
            Assert.That(plan.IsCompleted, Is.True);
        }

        [Test]
        public void MaximumEmissionCount_SplitsCatchUpWork()
        {
            var state = State(10, 2, 10);
            AssertPlan(state, 100, 3, 10, 3, 10, 14, out var first);
            Assert.That(first.WasLimited, Is.True);
            AssertState(first.NextState, 16, 2, 7);
            AssertPlan(first.NextState, 100, 3, 7, 3, 16, 20, out var second);
            Assert.That(second.WasLimited, Is.True);
            AssertState(second.NextState, 22, 2, 4);
        }

        [Test]
        public void ExactEmissionLimit_IsNotReportedAsLimited()
        {
            AssertPlan(State(5, 5, 3), 15, 3, 3, 3, 5, 15, out var plan);
            Assert.That(plan.WasLimited, Is.False);
        }

        [Test]
        public void LargeTickJump_UsesArithmeticCatchUp()
        {
            AssertPlan(State(0, 1, PeriodicTickPlanner.MaximumScheduleValue), long.MaxValue, PeriodicTickPlanner.MaximumEmissionCount, PeriodicTickPlanner.MaximumScheduleValue, PeriodicTickPlanner.MaximumEmissionCount, 0, 999999, out var plan);
            AssertState(plan.NextState, 1000000, 1, 999000000);
            Assert.That(plan.WasLimited, Is.True);
        }

        [Test]
        public void NearLongMaximum_ValidScheduleCompletesSafely()
        {
            AssertPlan(State(long.MaxValue - 6, 3, 3), long.MaxValue, 3, 3, 3, long.MaxValue - 6, long.MaxValue, out var plan);
            Assert.That(plan.IsCompleted, Is.True);
        }

        [Test]
        public void Plan_PreservesPreviousStateAndInputs()
        {
            var state = State(20, 5, 4);
            Assert.That(PeriodicTickPlanner.TryPlan(state, 30, 2, out var plan, out _), Is.True);
            AssertState(state, 20, 5, 4);
            AssertState(plan.PreviousState, 20, 5, 4);
            Assert.That(plan.ThroughTick, Is.EqualTo(30));
            Assert.That(plan.MaximumEmissionCount, Is.EqualTo(2));
        }

        [Test]
        public void RepeatedCalls_AreDeterministic()
        {
            var state = State(7, 3, 20);
            Assert.That(PeriodicTickPlanner.TryPlan(state, 40, 5, out var first, out _), Is.True);
            for (var index = 0; index < 100; index++)
            {
                Assert.That(PeriodicTickPlanner.TryPlan(state, 40, 5, out var next, out _), Is.True);
                Assert.That(next.DueCount, Is.EqualTo(first.DueCount));
                Assert.That(next.EmittedCount, Is.EqualTo(first.EmittedCount));
                Assert.That(next.FirstEmittedTick, Is.EqualTo(first.FirstEmittedTick));
                Assert.That(next.LastEmittedTick, Is.EqualTo(first.LastEmittedTick));
                AssertState(next.NextState, first.NextState.NextTick, first.NextState.IntervalTicks, first.NextState.RemainingCount);
            }
        }

        [Test]
        public void NegativeNextTick_ReturnsExpectedError() => AssertFailure(State(-1, 1, 1), 0, 1, PeriodicTickError.InvalidNextTick);

        [TestCase(0)]
        [TestCase(1000000001)]
        public void InvalidInterval_ReturnsExpectedError(int interval) => AssertFailure(State(0, interval, 1), 0, 1, PeriodicTickError.InvalidIntervalTicks);

        [TestCase(-1)]
        [TestCase(1000000001)]
        public void InvalidRemainingCount_ReturnsExpectedError(int count) => AssertFailure(State(0, 1, count), 0, 1, PeriodicTickError.InvalidRemainingCount);

        [TestCase(1, 1)]
        [TestCase(0, 2)]
        public void NonCanonicalCompletedState_ReturnsExpectedError(long nextTick, int interval) => AssertFailure(State(nextTick, interval, 0), 0, 1, PeriodicTickError.InvalidCompletedState);

        [Test]
        public void OverflowingSchedule_ReturnsExpectedError() => AssertFailure(State(long.MaxValue - 1, 2, 2), long.MaxValue, 1, PeriodicTickError.ScheduleOverflow);

        [Test]
        public void NegativeThroughTick_ReturnsExpectedError() => AssertFailure(State(0, 1, 1), -1, 1, PeriodicTickError.InvalidThroughTick);

        [TestCase(0)]
        [TestCase(1000001)]
        public void InvalidMaximumEmissionCount_ReturnsExpectedError(int count) => AssertFailure(State(0, 1, 1), 0, count, PeriodicTickError.InvalidMaximumEmissionCount);

        [Test]
        public void ValidationOrder_IsStateThenScheduleThenThroughThenLimit()
        {
            AssertFailure(State(-1, 0, -1), -1, 0, PeriodicTickError.InvalidNextTick);
            AssertFailure(State(0, 0, -1), -1, 0, PeriodicTickError.InvalidIntervalTicks);
            AssertFailure(State(0, 1, -1), -1, 0, PeriodicTickError.InvalidRemainingCount);
            AssertFailure(State(1, 1, 0), -1, 0, PeriodicTickError.InvalidCompletedState);
            AssertFailure(State(long.MaxValue, 2, 2), -1, 0, PeriodicTickError.ScheduleOverflow);
            AssertFailure(State(0, 1, 1), -1, 0, PeriodicTickError.InvalidThroughTick);
            AssertFailure(State(0, 1, 1), 0, 0, PeriodicTickError.InvalidMaximumEmissionCount);
        }

        [Test]
        public void Failure_ReturnsDefaultPlan()
        {
            Assert.That(PeriodicTickPlanner.TryPlan(State(0, 0, 1), 0, 1, out var plan, out var error), Is.False);
            Assert.That(error, Is.EqualTo(PeriodicTickError.InvalidIntervalTicks));
            Assert.That(plan.EmittedCount, Is.Zero);
            Assert.That(plan.FirstEmittedTick, Is.Zero);
        }

        private static PeriodicTickState State(long nextTick, int interval, int remaining) => new PeriodicTickState(nextTick, interval, remaining);

        private static void AssertPlan(PeriodicTickState state, long throughTick, int maximum, int due, int emitted, long first, long last, out PeriodicTickPlan plan)
        {
            Assert.That(PeriodicTickPlanner.TryPlan(state, throughTick, maximum, out plan, out var error), Is.True);
            Assert.That(error, Is.EqualTo(PeriodicTickError.None));
            Assert.That(plan.DueCount, Is.EqualTo(due));
            Assert.That(plan.EmittedCount, Is.EqualTo(emitted));
            Assert.That(plan.FirstEmittedTick, Is.EqualTo(first));
            Assert.That(plan.LastEmittedTick, Is.EqualTo(last));
        }

        private static void AssertFailure(PeriodicTickState state, long throughTick, int maximum, PeriodicTickError expected)
        {
            Assert.That(PeriodicTickPlanner.TryPlan(state, throughTick, maximum, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(expected));
        }

        private static void AssertState(PeriodicTickState state, long nextTick, int interval, int remaining)
        {
            Assert.That(state.NextTick, Is.EqualTo(nextTick));
            Assert.That(state.IntervalTicks, Is.EqualTo(interval));
            Assert.That(state.RemainingCount, Is.EqualTo(remaining));
        }
    }
}
