using NUnit.Framework;

namespace GameplayEffects.Tests
{
    [TestFixture]
    public sealed class TimedStackResolverTests
    {
        [Test]
        public void Inactive_AddRefresh_UsesIncomingState()
        {
            AssertResolve(State(0, 0), State(2, 25), Policy(4, 100, TimedStackCountMode.AddClamped, TimedStackDurationMode.RefreshClamped), 2, 25, out var result);
            Assert.That(result.WasInactive, Is.True);
            Assert.That(result.StackCountChanged, Is.True);
            Assert.That(result.DurationChanged, Is.True);
            Assert.That(result.StackClamped, Is.False);
            Assert.That(result.DurationClamped, Is.False);
        }

        [TestCase(TimedStackCountMode.AddClamped)]
        [TestCase(TimedStackCountMode.ReplaceClamped)]
        [TestCase(TimedStackCountMode.MaximumClamped)]
        public void Inactive_AllStackModes_ProduceIncomingStackCount(TimedStackCountMode mode)
        {
            AssertResolve(State(0, 0), State(3, 20), Policy(5, 100, mode, TimedStackDurationMode.RefreshClamped), 3, 20, out _);
        }

        [TestCase(TimedStackDurationMode.RefreshClamped)]
        [TestCase(TimedStackDurationMode.AddClamped)]
        [TestCase(TimedStackDurationMode.MaximumClamped)]
        public void Inactive_AllDurationModes_ProduceIncomingDuration(TimedStackDurationMode mode)
        {
            AssertResolve(State(0, 0), State(1, 30), Policy(5, 100, TimedStackCountMode.AddClamped, mode), 1, 30, out _);
        }

        [Test]
        public void AddRefresh_ClampsOnlyStackCount()
        {
            AssertResolve(State(2, 50), State(2, 30), Policy(3, 100, TimedStackCountMode.AddClamped, TimedStackDurationMode.RefreshClamped), 3, 30, out var result);
            Assert.That(result.StackClamped, Is.True);
            Assert.That(result.DurationClamped, Is.False);
        }

        [Test]
        public void AddAdd_ClampsOnlyDuration()
        {
            AssertResolve(State(1, 20), State(1, 15), Policy(4, 30, TimedStackCountMode.AddClamped, TimedStackDurationMode.AddClamped), 2, 30, out var result);
            Assert.That(result.StackClamped, Is.False);
            Assert.That(result.DurationClamped, Is.True);
        }

        [Test]
        public void MaximumMaximum_SelectsEachLargerValue()
        {
            AssertResolve(State(3, 40), State(2, 60), Policy(5, 100, TimedStackCountMode.MaximumClamped, TimedStackDurationMode.MaximumClamped), 3, 60, out var result);
            Assert.That(result.StackCountChanged, Is.False);
            Assert.That(result.DurationChanged, Is.True);
        }

        [Test]
        public void ReplaceRefresh_ReplacesBothValues()
        {
            AssertResolve(State(3, 40), State(1, 10), Policy(5, 100, TimedStackCountMode.ReplaceClamped, TimedStackDurationMode.RefreshClamped), 1, 10, out var result);
            Assert.That(result.StackCountChanged, Is.True);
            Assert.That(result.DurationChanged, Is.True);
        }

        [Test]
        public void MaximumModes_CanProduceNoChange()
        {
            AssertResolve(State(4, 80), State(2, 30), Policy(5, 100, TimedStackCountMode.MaximumClamped, TimedStackDurationMode.MaximumClamped), 4, 80, out var result);
            Assert.That(result.StackCountChanged, Is.False);
            Assert.That(result.DurationChanged, Is.False);
        }

        [Test]
        public void IncomingAbovePolicy_IsAcceptedAndClamped()
        {
            AssertResolve(State(1, 10), State(9, 500), Policy(4, 100, TimedStackCountMode.ReplaceClamped, TimedStackDurationMode.RefreshClamped), 4, 100, out var result);
            Assert.That(result.StackClamped, Is.True);
            Assert.That(result.DurationClamped, Is.True);
        }

        [Test]
        public void MaximumBoundary_AdditionUsesWideIntermediate()
        {
            AssertResolve(State(TimedStackResolver.MaximumValue, TimedStackResolver.MaximumValue), State(TimedStackResolver.MaximumValue, TimedStackResolver.MaximumValue), Policy(TimedStackResolver.MaximumValue, TimedStackResolver.MaximumValue, TimedStackCountMode.AddClamped, TimedStackDurationMode.AddClamped), TimedStackResolver.MaximumValue, TimedStackResolver.MaximumValue, out var result);
            Assert.That(result.StackClamped, Is.True);
            Assert.That(result.DurationClamped, Is.True);
        }

        [Test]
        public void Resolution_PreservesExactInputsAndPolicy()
        {
            var current = State(2, 40);
            var incoming = State(3, 20);
            var policy = Policy(4, 50, TimedStackCountMode.AddClamped, TimedStackDurationMode.AddClamped);
            Assert.That(TimedStackResolver.TryResolve(current, incoming, policy, out var result, out _), Is.True);
            AssertState(result.PreviousState, 2, 40);
            AssertState(result.IncomingState, 3, 20);
            Assert.That(result.Policy.MaximumStackCount, Is.EqualTo(4));
            Assert.That(result.Policy.MaximumDurationTicks, Is.EqualTo(50));
            Assert.That(result.Policy.StackMode, Is.EqualTo(TimedStackCountMode.AddClamped));
            Assert.That(result.Policy.DurationMode, Is.EqualTo(TimedStackDurationMode.AddClamped));
        }

        [Test]
        public void RepeatedCalls_AreDeterministic()
        {
            var current = State(2, 40);
            var incoming = State(3, 20);
            var policy = Policy(4, 50, TimedStackCountMode.AddClamped, TimedStackDurationMode.AddClamped);
            Assert.That(TimedStackResolver.TryResolve(current, incoming, policy, out var first, out _), Is.True);
            for (var index = 0; index < 100; index++)
            {
                Assert.That(TimedStackResolver.TryResolve(current, incoming, policy, out var next, out _), Is.True);
                AssertState(next.ResultState, first.ResultState.StackCount, first.ResultState.RemainingTicks);
                Assert.That(next.StackClamped, Is.EqualTo(first.StackClamped));
                Assert.That(next.DurationClamped, Is.EqualTo(first.DurationClamped));
            }
        }

        [TestCase(0)]
        [TestCase(1000000001)]
        public void InvalidMaximumStackCount_ReturnsExpectedError(int value) => AssertFailure(State(0, 0), State(1, 1), Policy(value, 10, TimedStackCountMode.AddClamped, TimedStackDurationMode.RefreshClamped), TimedStackError.InvalidMaximumStackCount);

        [TestCase(0)]
        [TestCase(1000000001)]
        public void InvalidMaximumDurationTicks_ReturnsExpectedError(int value) => AssertFailure(State(0, 0), State(1, 1), Policy(10, value, TimedStackCountMode.AddClamped, TimedStackDurationMode.RefreshClamped), TimedStackError.InvalidMaximumDurationTicks);

        [Test]
        public void InvalidStackMode_ReturnsExpectedError() => AssertFailure(State(0, 0), State(1, 1), Policy(10, 10, (TimedStackCountMode)99, TimedStackDurationMode.RefreshClamped), TimedStackError.InvalidStackMode);

        [Test]
        public void InvalidDurationMode_ReturnsExpectedError() => AssertFailure(State(0, 0), State(1, 1), Policy(10, 10, TimedStackCountMode.AddClamped, (TimedStackDurationMode)(-1)), TimedStackError.InvalidDurationMode);

        [TestCase(0, 1)]
        [TestCase(1, 0)]
        [TestCase(-1, 1)]
        [TestCase(1, -1)]
        [TestCase(6, 1)]
        [TestCase(1, 11)]
        public void InvalidCurrentState_ReturnsExpectedError(int stacks, int ticks) => AssertFailure(State(stacks, ticks), State(1, 1), Policy(5, 10, TimedStackCountMode.AddClamped, TimedStackDurationMode.RefreshClamped), TimedStackError.InvalidCurrentState);

        [TestCase(0, 1)]
        [TestCase(1, 0)]
        [TestCase(-1, 1)]
        [TestCase(1, -1)]
        [TestCase(1000000001, 1)]
        [TestCase(1, 1000000001)]
        public void InvalidIncomingState_ReturnsExpectedError(int stacks, int ticks) => AssertFailure(State(0, 0), State(stacks, ticks), Policy(5, 10, TimedStackCountMode.AddClamped, TimedStackDurationMode.RefreshClamped), TimedStackError.InvalidIncomingState);

        [Test]
        public void ValidationOrder_IsPolicyThenModesThenCurrentThenIncoming()
        {
            AssertFailure(State(-1, -1), State(0, 0), Policy(0, 0, (TimedStackCountMode)99, (TimedStackDurationMode)99), TimedStackError.InvalidMaximumStackCount);
            AssertFailure(State(-1, -1), State(0, 0), Policy(1, 0, (TimedStackCountMode)99, (TimedStackDurationMode)99), TimedStackError.InvalidMaximumDurationTicks);
            AssertFailure(State(-1, -1), State(0, 0), Policy(1, 1, (TimedStackCountMode)99, (TimedStackDurationMode)99), TimedStackError.InvalidStackMode);
            AssertFailure(State(-1, -1), State(0, 0), Policy(1, 1, TimedStackCountMode.AddClamped, (TimedStackDurationMode)99), TimedStackError.InvalidDurationMode);
            AssertFailure(State(-1, -1), State(0, 0), Policy(1, 1, TimedStackCountMode.AddClamped, TimedStackDurationMode.RefreshClamped), TimedStackError.InvalidCurrentState);
            AssertFailure(State(0, 0), State(0, 0), Policy(1, 1, TimedStackCountMode.AddClamped, TimedStackDurationMode.RefreshClamped), TimedStackError.InvalidIncomingState);
        }

        [Test]
        public void Failure_ReturnsDefaultResolution()
        {
            Assert.That(TimedStackResolver.TryResolve(State(0, 0), State(0, 0), Policy(1, 1, TimedStackCountMode.AddClamped, TimedStackDurationMode.RefreshClamped), out var result, out var error), Is.False);
            Assert.That(error, Is.EqualTo(TimedStackError.InvalidIncomingState));
            AssertState(result.ResultState, 0, 0);
        }

        private static TimedStackState State(int stacks, int ticks) => new TimedStackState(stacks, ticks);

        private static TimedStackPolicy Policy(int stacks, int ticks, TimedStackCountMode stackMode, TimedStackDurationMode durationMode) => new TimedStackPolicy(stacks, ticks, stackMode, durationMode);

        private static void AssertResolve(TimedStackState current, TimedStackState incoming, TimedStackPolicy policy, int stacks, int ticks, out TimedStackResolution result)
        {
            Assert.That(TimedStackResolver.TryResolve(current, incoming, policy, out result, out var error), Is.True);
            Assert.That(error, Is.EqualTo(TimedStackError.None));
            AssertState(result.ResultState, stacks, ticks);
        }

        private static void AssertFailure(TimedStackState current, TimedStackState incoming, TimedStackPolicy policy, TimedStackError expected)
        {
            Assert.That(TimedStackResolver.TryResolve(current, incoming, policy, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo(expected));
        }

        private static void AssertState(TimedStackState state, int stacks, int ticks)
        {
            Assert.That(state.StackCount, Is.EqualTo(stacks));
            Assert.That(state.RemainingTicks, Is.EqualTo(ticks));
        }
    }
}
